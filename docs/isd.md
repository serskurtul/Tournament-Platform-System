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

**Kubernetes.** Namespace `tournament-platform`: Deployment (або Argo Rollout) API з liveness `/version` і readiness `/health`; Deployment клієнта; PostgreSQL з PVC; Job/hook міграцій; Service-и `ClusterIP` / `NodePort 30173`; Ingress `tournament.local`. Деталі — [`k8s/README.md`](../k8s/README.md), [`charts/tournament-platform/README.md`](../charts/tournament-platform/README.md).

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

**Резервне копіювання.**

| Параметр | Цільове значення | Поточний стан |
|----------|------------------|---------------|
| RPO (допустима втрата даних) | ≤ 24 год | Ручний дамп (`db/backups/backup_20260611.sql`); автоматичного розкладу немає — прогалина |
| RTO (час відновлення) | ≤ 1 год | Відновлення: розгорнути стек → `psql -f <dump>` → перевірити `/health` |

Процедура: щоденний `pg_dump -Fc tournament_db` (CronJob у Kubernetes або cron на хості) у зовнішнє сховище з ротацією 14 днів; щомісячна перевірка відновлення на тестовому стенді. Поки CronJob не впроваджено, цільове RPO не забезпечено, і в [handover](handover.md) це позначено як ризик.
