# ЛР-4: Helm-чарт ZVYTIAHA (`tournament-platform`)

Чарт описує той самий застосунок, що й `k8s/` з ЛР-3 (API, клієнт, PostgreSQL), але з параметрами
для кількох середовищ. Поточна версія чарта `0.1.0`.

## Структура

```
charts/tournament-platform/
├── Chart.yaml                 # name, description, version, appVersion
├── values.yaml                # значення за замовчуванням = розробка (minikube)
├── values-dev.yaml            # розробка: 1 репліка API, ingress вимкнено
├── values-prod.yaml           # прод-подібне: 3 репліки, ingress увімкнено, секрети ПОРОЖНІ
├── values-minikube.yaml       # GitOps-ціль (ЛР-5..7): canary Rollout, моніторинг
├── files/dashboards/          # JSON дашборда Grafana (ЛР-7)
└── templates/
    ├── _helpers.tpl           # повне імʼя, імена компонентів, набори міток, спільний pod-шаблон
    ├── configmap.yaml, secret.yaml, postgres-*.yaml
    ├── api-deployment.yaml    # Deployment, якщо rollout.enabled=false
    ├── api-rollout.yaml       # Argo Rollout, якщо rollout.enabled=true (ЛР-6)
    ├── api-service.yaml, api-service-preview.yaml, client-*.yaml, ingress.yaml
    ├── analysis-template.yaml, servicemonitor.yaml, prometheusrule.yaml, dashboard-configmap.yaml  # ЛР-7
    ├── hooks/                 # db-precheck (pre-upgrade), db-migrate (post-install/post-upgrade)
    ├── tests/test-connection.yaml   # helm test
    └── NOTES.txt
```

Імена об'єктів будуються з імені релізу (`{{ .Release.Name }}-{{ .Chart.Name }}-<компонент>`),
тож чарт можна встановити двічі в один кластер під різними іменами релізів.

## version і appVersion

- **`version: 0.1.0`** — версія самого чарта (пакунка). Змінюється, коли змінюються шаблони або схема values.
- **`appVersion: "1.0.0"`** — версія застосунку, для якого написано чарт (версія образів за замовчуванням).

Для цього проєкту `appVersion` збігається з тегом образу за замовчуванням, але **Helm не використовує її для вибору образу**:
образ задає `api.image.tag` у values. Тому змінювати реліз потрібно саме `api.image.tag`, а `appVersion`
лише інформаційна. Тег також експортується як змінна `APP_VERSION`, тож `/version` показує фактичну версію.

## Параметри values

| Ключ | За замовчуванням | Опис |
|------|------------------|------|
| `api.image.repository` | `ghcr.io/katushhiaa/tournament-platform-server` | Образ API |
| `api.image.tag` | `1.0.0` | Конкретний тег (не `latest`) |
| `api.image.pullPolicy` | `IfNotPresent` | Політика завантаження |
| `api.replicaCount` | `2` | Кількість реплік API |
| `api.failRate` | `"0"` | Частка запитів із 500 (демо ЛР-7); `0` вимкнено |
| `api.service.type` / `.port` | `ClusterIP` / `80` | Тип і порт Service API |
| `api.resources` | requests 100m/256Mi, limits 500m/512Mi | Ресурси API |
| `client.image.repository` / `.tag` | `ghcr.io/katushhiaa/tournament-platform-client` / `1.0.0` | Образ клієнта |
| `client.replicaCount` | `2` | Репліки клієнта |
| `client.service.type` / `.port` / `.nodePort` | `NodePort` / `80` / `30173` | Доступ з хоста |
| `postgres.image.repository` / `.tag` | `postgres` / `15` | Образ БД |
| `postgres.persistence.enabled` / `.size` / `.storageClass` | `true` / `1Gi` / `""` | PersistentVolumeClaim |
| `config.aspnetEnvironment` | `Development` | `ASPNETCORE_ENVIRONMENT` |
| `config.frontendOrigin` | `http://tournament.local` | CORS origin |
| `config.jwtIssuer` | `tournament-api` | Емітент JWT |
| `config.postgresDb` / `config.postgresUser` | `tournament_db` / `postgres` | Параметри БД |
| `secrets.postgresPassword` | плейсхолдер | **Обов'язковий** у проді (`required`) |
| `secrets.jwtKey` | плейсхолдер | **Обов'язковий** у проді (`required`) |
| `ingress.enabled` / `.className` / `.host` | `false` / `nginx` / `tournament.local` | Ingress (опційний) |
| `db.seedOnInstall` | `true` | Seed лише при першому встановленні |
| `rollout.enabled` | `false` | API як Argo Rollout (ЛР-6) |
| `rollout.strategy` | `{}` | `canary.steps` або `blueGreen` (ЛР-6) |
| `analysis.enabled` / `.prometheusAddress` / `.successRateThreshold` | `false` / `…:9090` / `0.95` | AnalysisTemplate (ЛР-7) |
| `metrics.serviceMonitor.enabled` / `.interval` | `false` / `15s` | ServiceMonitor (ЛР-7) |
| `metrics.alerts.enabled` | `false` | PrometheusRule (ЛР-7, d*) |
| `dashboards.enabled` | `false` | ConfigMap з дашбордом Grafana (ЛР-7) |

Мітки всіх об'єктів: `mascot: garbuz`; анотації: `devops-lab/mascot-approved: "true"`, `devops-lab/mascot-name: "гарбуз"`.
Примітка: значення мітки Kubernetes не може містити кирилицю, тому назва маскота (`гарбуз`) винесена в анотацію, а мітка — транслітерована `garbuz`.

## Пріоритет значень

Від нижчого до вищого:

1. `values.yaml` (значення за замовчуванням чарта);
2. файли `-f` (`values-dev.yaml`, `values-prod.yaml`, …). Якщо передано кілька файлів, **останній перемагає**;
3. прапорці `--set` / `--set-file` — найвищий пріоритет, перекривають і `values.yaml`, і файли `-f`.

## Порівняння середовищ без встановлення

```bash
helm template dev  charts/tournament-platform -f charts/tournament-platform/values-dev.yaml  > /tmp/dev.yaml
helm template prod charts/tournament-platform -f charts/tournament-platform/values-prod.yaml \
  --set secrets.postgresPassword=x --set secrets.jwtKey=y > /tmp/prod.yaml
diff /tmp/dev.yaml /tmp/prod.yaml | less
```

## Команди (ЛР-4, пп. 6–10)

```bash
# 7a. Перевірка чарта
helm lint charts/tournament-platform -f charts/tournament-platform/values-dev.yaml

# 7b. Згенеровані маніфести порівняно з ЛР-3
helm template demo charts/tournament-platform -f charts/tournament-platform/values-dev.yaml > /tmp/gen.yaml
kubectl apply --dry-run=client -f /tmp/gen.yaml

# 7c–7d. Встановлення у власний namespace та перелік релізів
helm install tp charts/tournament-platform -n tournament-platform --create-namespace \
  -f charts/tournament-platform/values-dev.yaml --set secrets.postgresPassword=... --set secrets.jwtKey=...
helm list -n tournament-platform
helm get values tp -n tournament-platform
helm get values tp -n tournament-platform --all

# 7e. Де Helm зберігає стан релізу
kubectl -n tournament-platform get secrets -l owner=helm

# 8a. Оновлення тегом через --set
helm upgrade tp charts/tournament-platform -n tournament-platform -f charts/tournament-platform/values-dev.yaml \
  --set api.image.tag=1.1.0 --set secrets.postgresPassword=... --set secrets.jwtKey=...

# 8b. Оновлення файлом значень іншого середовища
helm upgrade tp charts/tournament-platform -n tournament-platform -f charts/tournament-platform/values-prod.yaml \
  --set secrets.postgresPassword=... --set secrets.jwtKey=...

# 8c–8d. Історія ревізій, відкат, видалення
helm history tp -n tournament-platform
helm rollback tp 2 -n tournament-platform
helm uninstall tp -n tournament-platform
kubectl -n tournament-platform get all,secrets,configmaps,pvc,jobs    # має бути порожньо, крім стандартних

# 9b. Хук pre-upgrade (перевірка БД) і тест релізу
helm test tp -n tournament-platform
```

Для відстеження змін між ревізіями: `helm get manifest tp -n tournament-platform --revision N`.

Вимога «реальні паролі не зберігаються»: у `values*.yaml` лише плейсхолдери; у `values-prod.yaml` секрети порожні,
тому прод не встановиться без явного `--set`/`--set-file` (перевірено: `helm template` без секретів завершується помилкою `required`).

## Подяки

Автор дякує гарбузу за тещо жоден реліз не довелос…
