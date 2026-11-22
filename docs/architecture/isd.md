---
owner: Скуртул Сергій
status: published
review_date: 2027-05-20
tags: [architecture, ops]
---

# Infrastructure Specification Document (ISD) — ZVYTIAHA

Що всередині системи, описано в [SDD](sdd.md). Тут — де і на чому вона працює, як збирається, розгортається, спостерігається і відновлюється.

## 1. Огляд середовищ

| Середовище | Призначення | Як піднімається | Конфігурація |
|------------|-------------|-----------------|--------------|
| **dev (локально)** | Розробка, демо | `docker compose up --build` | `.env` з `.env.example` |
| **dev (Kubernetes)** | Навчальний кластер minikube | `kubectl apply -f k8s/` або `helm install … -f values-dev.yaml` | `k8s/`, `charts/tournament-platform/values-dev.yaml` |
| **test / CI** | Збірка, тести, сканування на кожен PR | GitHub Actions | `.github/workflows/*.yml` |
| **staging (GitOps)** | Автоматичне розгортання з Git, canary-реліз | Argo CD (`bootstrap/root-app.yaml`) | `values-minikube.yaml` |
| **production (цільове)** | Прод-подібний профіль | `helm upgrade --install … -f values-prod.yaml --set-file secrets.*` | `values-prod.yaml`, секрети поза Git |

## 2. Топологія розгортання

**Локально (Docker Compose).** Контейнери `db` (PostgreSQL 15, порт 5432), `init` (міграції + seed, завершується), `app` (API, порт 5050 → 80, healthcheck `GET /version`), `client` (nginx зі SPA, порт 5173 → 80), `pgadmin` (8080).

Порядок старту (з `docker-compose.yml`; перевіряється `scripts/quality/doc_contract_check.py`):

| Залежність | Сенс |
|------------|------|
| `init -> db (service_healthy)` | міграції запускаються, коли Postgres приймає з'єднання |
| `app -> db (service_healthy)` | API стартує після готовності БД |
| `app -> init (service_completed_successfully)` | API стартує лише після успішних міграцій |
| `pgadmin -> db (service_healthy)` | адмінка чекає на БД |
| `client -> app (service_healthy)` | SPA стартує лише після того, як API пройшов healthcheck (`curl /version`, кожні 10 с, до 10 спроб) |

**Kubernetes.** Namespace `tournament-platform`: Deployment (або Argo Rollout) API з liveness `/version` і readiness `/health`; Deployment клієнта; PostgreSQL з PVC; Job/hook міграцій; Service-и `ClusterIP` / `NodePort 30173`; Ingress `tournament.local`. Деталі — [`k8s/README.md`](https://github.com/serskurtul/Tournament-Platform-System/blob/main/k8s/README.md), [`charts/tournament-platform/README.md`](https://github.com/serskurtul/Tournament-Platform-System/blob/main/charts/tournament-platform/README.md).

**GitOps.** Argo CD (App of Apps): `bootstrap/root-app.yaml` → `argocd-apps/` (застосунок, Argo Rollouts, kube-prometheus-stack, Loki, Alloy). Реліз — зміна тегу образу в `values-minikube.yaml`; canary з кроком analysis (поріг успішних запитів 95%).

## 3. Змінні середовища (без секретів)

| Змінна | Де | Приклад | Призначення |
|--------|----|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | API | `Development` / `Production` | режим ASP.NET |
| `ConnectionStrings__DefaultConnection` | API, init | складається з `POSTGRES_*` | рядок підключення до БД |
| `POSTGRES_DB`, `POSTGRES_USER` | db, API, init | `tournament_db`, `postgres` | параметри БД |
| `POSTGRES_PASSWORD` | db, API, init | **секрет** (`.env` / Kubernetes Secret) | пароль БД |
| `JWT__KEY` | API | **секрет** | ключ підпису JWT |
| `JWT__ISSUER`, `JWT__EXPIRATIONMINUTES`, `REFRESH_TOKEN_DAYS` | API | `tournament-api`, `10`, `14` | параметри токенів |
| `FRONTEND_ORIGIN` | API | `http://localhost:5173` | CORS |
| `APP_VERSION` | API | тег образу | показується в `/version` |
| `FAIL_RATE` | API | `0` | демо-інʼєкція помилок (ЛР-7 DevOps) |
| `API_UPSTREAM` | client (nginx) | `http://app:80` / `http://tp-tournament-platform-api:80` | куди nginx проксіює `/api/` |

Секрети не зберігаються в Git: `.env` у `.gitignore`, у Helm `values-prod.yaml` секрети порожні (`required`), Kubernetes-маніфести містять лише плейсхолдери.

## 4. Сховища даних

| Сховище | Вміст | Розміщення |
|---------|-------|-----------|
| PostgreSQL 15 | усі бізнес-дані | volume `pgdata` (compose) / PVC 1 Gi dev, 10 Gi prod |
| Google Cloud Storage | банери турнірів | бакет із `tournament-platform-system-secrets.json` (поза Git) |
| GHCR | образи `ghcr.io/serskurtul/tournament-platform-{server,client}` | теги SHA, semver (`vX.Y.Z`), `latest` для main |

## 5. CI/CD-конвеєр

| Workflow | Тригер | Стадії | Quality Gate |
|----------|--------|--------|--------------|
| `ci.yml` | PR, push у main, теги `v*.*.*` | server/client: lint → build → test; docker-build + Trivy; publish у GHCR (лише push) | lint блокує; Trivy блокує CRITICAL/HIGH з виправленням; publish лише після зелених джоб |
| `quality-metrics.yml` | push, PR | Lizard → churn → матриця Hotspots → артефакт `quality-report` | інформаційний |
| `sonarcloud.yml` | вручну | SonarScanner → вивантаження метрик через Web API | інформаційний |
| `sqale-audit.yml` | вручну | SonarCloud API → SQALE-аудит → `FINANCIAL_LEDGER.md` | PASS/FAIL окупності |

Відкат: Helm `helm rollback`, GitOps — `git revert` коміту з тегом, Argo Rollouts — `abort` (автоматично при провалі analysis).

## 6. Моніторинг і резервне копіювання

**Моніторинг.** Prometheus збирає `/metrics` через ServiceMonitor (`http_requests_total{method,route,status}`, `http_request_duration_seconds`). Grafana-дашборд «ZVYTIAHA: Tournament API» (RPS, частка 5xx, p95, CPU/пам'ять подів, логи). Loki + Alloy збирають JSON-логи з stdout. Правило алерту: частка 5xx > 5% протягом 1 хв.

**Алерти** — [каталог алертів](../runbooks/alerts.md) (кожен з runbook), SLO — [`docs/slo.yaml`](https://github.com/serskurtul/Tournament-Platform-System/blob/main/docs/slo.yaml).

Резервне копіювання й відновлення — [§7](#7-експлуатаційні-процедури).

## 7. Експлуатаційні процедури

### 7.1 Розгортання версії

| Середовище | Команди | Health-перевірка |
|------------|---------|------------------|
| compose | `git checkout vX.Y.Z && docker compose up -d --build` | `curl -fsS localhost:5050/health` → 200, `"databaseState":"Open"`; `curl localhost:5050/version` → `vX.Y.Z` |
| Kubernetes (Helm) | `helm upgrade --install tournament charts/tournament-platform -n tournament-platform -f values-prod.yaml --set api.image.tag=X.Y.Z --set-file secrets.postgresPassword=… --set-file secrets.jwtKey=… --wait --timeout 10m` | `helm test tournament -n tournament-platform`; `kubectl rollout status deploy/tournament-tournament-platform-api` |
| GitOps | PR зі зміною `api.image.tag` у `values-minikube.yaml` → merge → Argo CD sync; canary з analysis (≥ 95% успішних) | Argo Rollouts: `kubectl argo rollouts get rollout … --watch` |

Відкат: `helm rollback tournament <REVISION>` / `git revert` коміту з тегом / `kubectl argo rollouts abort` (автоматично при провалі analysis).

### 7.2 Оновлення з міграціями

1. Прочитати [CHANGELOG](https://github.com/serskurtul/Tournament-Platform-System/blob/main/CHANGELOG.md) між поточним і новим тегом: розділи *Changed* / *Removed* і позначки про міграції.
2. **Зробити позапланову копію**: `scripts/ops/backup_db.sh` (compose) або `kubectl create job --from=cronjob/…-db-backup pre-upgrade-<tag>`. Це точка відкату для незворотних міграцій.
3. Розгорнути (7.1). Helm виконує `pre-upgrade` hook `db-precheck` (БД доступна), потім `post-upgrade` hook `db-migrate` (`dotnet ef database update`). У compose міграції виконує контейнер `init` до старту `app`.
4. Перевірити: `kubectl logs job/…-db-migrate` закінчується `Done.`; `/health` 200; smoke: `GET /api/v1/sport`, `GET /api/v1/tournaments`.
5. Якщо міграція впала, застосунок попередньої версії лишається працювати (Job — post-upgrade). Відкат: `helm rollback`, а коли міграцію вже частково застосовано — відновлення з копії кроку 2 за [RB-001](../runbooks/RB-001.md).

### 7.3 Відновлення після відмови

Порядок підняття відповідає залежностям (§2): **БД → міграції → API → клієнт**.

1. PostgreSQL: под/контейнер `Running`, `pg_isready` OK. Якщо дані втрачено — [RB-001](../runbooks/RB-001.md).
2. Міграції: `init` / `db-migrate` завершився успішно (ідемпотентні, повторний запуск безпечний).
3. API: readiness `/health` = 200 (перевіряє з'єднання з БД). Не масштабувати API раніше, ніж БД готова: інакше лавина 5xx і хибні алерти.
4. Клієнт: `client -> app (service_healthy)` у compose; у K8s nginx віддає SPA незалежно, але без API показує помилки.
5. Підтвердження: 5xx на дашборді < 1%, алерти `resolved`, запис у тікеті інциденту.

### 7.4 RTO і RPO

| Параметр | Ціль | Розрахунок |
|----------|------|-----------|
| **RPO** — допустима втрата даних | **≤ 24 год** | Повна копія щодня о 02:15 UTC (CronJob `db-backup`). Найгірший випадок — відмова перед наступним запуском: 24 год + тривалість дампу (< 1 хв при 50 МБ). Перед кожним оновленням із міграціями — позапланова копія (RPO для релізу ≈ 0). Для турнірної платформи втрата доби даних прийнятна: дані відновлювані з боку організаторів (повторна заявка), платежів немає |
| **RTO** — допустимий час відновлення | **≤ 1 год** | виявлення: алерт `for: 2m` + реакція on-call SEV-1 ≤ 15 хв → діагностика 5 хв → зупинка API 2 хв → перевірка копії в тимчасовій БД + відновлення: drill 2 с на 12 таблицях; для бази до 1 ГБ `pg_restore` займає ~ 5–10 хв → запуск API і верифікація 10 хв. **Разом ≈ 37–42 хв**, запас ~ 20 хв на повтор із попередньою копією |

### 7.5 Політика резервного копіювання

| Параметр | Значення |
|----------|----------|
| Обсяг даних | ~ 50 МБ (том `pgdata`, 2026-11-22); дамп 35 КБ (стиснений custom-формат), зашифрований 12 КБ |
| Тип і розклад | **повна** логічна копія `pg_dump -Fc` щодня о 02:15 UTC + позапланова перед оновленням з міграціями. Інкрементних копій немає: при такому обсязі повна копія займає секунди, а WAL-архівування (PITR) ускладнює експлуатацію без виграшу для RPO 24 год |
| Зберігання (retention) | 14 щоденних копій на PVC `db-backup` (`find -mtime +14 -delete`); 12 щомісячних (перша копія місяця) у offsite-сховищі |
| Шифрування | offsite-копії шифруються `gpg --symmetric --cipher-algo AES256` (`backup_db.sh` з `BACKUP_PASSPHRASE_FILE`); парольна фраза — у сейфі команди, не в Git і не поруч із копіями. Цілісність — `sha256` для кожного файлу |
| **3-2-1** | **3 копії**: робоча БД (PVC `postgres-data`) + копія на PVC `db-backup` + offsite. **2 типи носіїв**: блоковий том кластера і об'єктне сховище (бакет із versioning/retention lock). **1 поза майданчиком**: offsite-бакет в іншому регіоні/провайдері, синхронізація щодня після CronJob (`rclone copy` зашифрованих файлів) |
| Відповідальний | роль SRE (L2), власник документа — у front matter |

Поточний стан: CronJob і скрипти впроваджено (ЛР-12), увімкнено в `values-prod.yaml` і `values-minikube.yaml`. Offsite-синхронізація потребує облікового запису хмарного сховища і поки не налаштована: правило 3-2-1 виконано на рівні політики, а не інфраструктури.

### 7.6 Restore drill

Копія, з якої ніколи не відновлювались, копією не вважається.

- **Періодичність:** щомісяця (перший робочий понеділок) і після кожної зміни схеми копіювання.
- **Процедура:** `scripts/ops/backup_db.sh` → `scripts/ops/restore_db.sh <файл> tournament_drill` → скрипт перевіряє sha256 і кількість рядків кожної таблиці з маніфестом → smoke-запит до API, підключеного до `tournament_drill` → видалити `tournament_drill`.
- **Критерії успіху:** `verify: row counts match manifest`, час відновлення в межах бюджету RTO, протокол у `docs/quality-labs/` або тікеті.
- **Останній drill:** 2026-11-22 — зашифрована копія, sha256 OK, 12/12 таблиць, 2 с; захист від перезапису робочої бази спрацював (exit 2).
