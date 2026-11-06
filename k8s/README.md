# ЛР-3: розгортання застосунку в Kubernetes (plain manifests)

Маніфести описують той самий застосунок, що й ЛР-1/ЛР-2: ASP.NET Core API, Vue-клієнт (nginx) і PostgreSQL.
Кожен файл у `k8s/` містить один об'єкт. Namespace `tournament-platform`.

## Склад об'єктів

| Файл | Об'єкт | Призначення |
|------|--------|-------------|
| `00-namespace.yaml` | Namespace | Ізоляція всіх об'єктів проєкту |
| `01-configmap.yaml` | ConfigMap `tournament-config` | Несекретні параметри (env) |
| `02-secret.yaml` | Secret `tournament-secrets` | Пароль БД і ключ JWT (лише плейсхолдери) |
| `03-postgres-pvc.yaml` | PersistentVolumeClaim | Сховище для стану БД (1Gi) |
| `04-postgres-deployment.yaml` | Deployment `postgres` | База даних, `Recreate` через ReadWriteOnce |
| `05-postgres-service.yaml` | Service `postgres` (ClusterIP) | Доступ до БД лише всередині кластера |
| `06-api-deployment.yaml` | Deployment `tournament-api` | 2 репліки API, requests/limits, liveness `/version`, readiness `/health` |
| `07-api-service.yaml` | Service `tournament-api` (ClusterIP) | Внутрішній доступ до API (nginx клієнта проксіює на нього) |
| `08-client-deployment.yaml` | Deployment `tournament-client` | 2 репліки nginx зі SPA |
| `09-client-service.yaml` | Service `tournament-client` (NodePort 30173) | Доступ з хоста (minikube) |
| `10-ingress.yaml` | Ingress `tournament-ingress` | Альтернативний вхід за іменем `tournament.local` |
| `11-db-init-job.yaml` | Job `tournament-db-init` | Міграції EF Core та seed (аналог сервісу `init` з compose) |

Усі об'єкти мають мітку `mascot` і анотацію `devops-lab/mascot-approved: "true"` за Додатком А.
Примітка: значення мітки Kubernetes не може містити кирилицю, тому мітка `mascot: kabachok` (транслітерація),
а оригінальна назва `кабачок` записана в анотації `devops-lab/mascot-name`.

## Передумови

```bash
minikube start --driver=docker
minikube addons enable ingress
minikube addons enable metrics-server
kubectl get nodes
kubectl -n kube-system get pods
```

Образи мають бути в реєстрі (ЛР-2 CI публікує їх у GHCR при push тегу `vX.Y.Z`, наприклад `v1.0.0`):
`ghcr.io/katushhiaa/tournament-platform-server:1.0.0` і `ghcr.io/katushhiaa/tournament-platform-client:1.0.0`.

## Розгортання однією командою з каталогу `k8s/`

```bash
kubectl apply -f k8s/
```

Файли застосовуються в алфавітному порядку, тож Namespace (`00-`) створюється першим.
Перевірка:

```bash
kubectl -n tournament-platform get pods,deploy,rs,svc,job
kubectl -n tournament-platform wait --for=condition=complete job/tournament-db-init --timeout=300s
```

Відкрити застосунок: `minikube service tournament-client -n tournament-platform --url` (або `http://$(minikube ip):30173`).

## Параметри ConfigMap `tournament-config`

| Ключ | Значення в лабі | Опис |
|------|-----------------|------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Режим ASP.NET Core |
| `FRONTEND_ORIGIN` | `http://tournament.local` | Дозволений origin для CORS |
| `JWT__ISSUER` | `tournament-api` | Емітент JWT |
| `POSTGRES_DB` | `tournament_db` | Назва бази |
| `POSTGRES_USER` | `postgres` | Користувач БД |

## Параметри Secret `tournament-secrets`

| Ключ | Опис |
|------|------|
| `POSTGRES_PASSWORD` | Пароль БД. У файлі — плейсхолдер `CHANGE_ME_postgres_password` |
| `JWT__KEY` | Ключ підпису JWT. У файлі — плейсхолдер |

Реальні значення у Git не зберігаються. Для справжнього розгортання створити Secret з командної
строки (див. коментар у `02-secret.yaml`). Рядок підключення збирається Kubernetes з цих двох значень через `$(VAR)`.

## Демонстрація життєвого циклу (пп. 5–7 завдання)

```bash
# 5. Об'єкти
kubectl -n tournament-platform get pods,deploy,rs,svc
# Значення з ConfigMap і Secret всередині контейнера
kubectl -n tournament-platform exec deploy/tournament-api -- printenv FRONTEND_ORIGIN JWT__ISSUER POSTGRES_PASSWORD

# 6a. Самовідновлення
kubectl -n tournament-platform delete pod -l app.kubernetes.io/name=tournament-api --wait=false
kubectl -n tournament-platform get pods -w

# 6b. Масштабування і розподіл трафіку
kubectl -n tournament-platform scale deploy/tournament-api --replicas=4
kubectl -n tournament-platform run curl --rm -it --image=curlimages/curl -- \
  sh -c 'for i in $(seq 1 10); do curl -s http://tournament-api/version; echo; done'

# 6c–6e. Оновлення, історія, відкат, доступність
kubectl -n tournament-platform set image deploy/tournament-api api=ghcr.io/katushhiaa/tournament-platform-server:1.1.0
kubectl -n tournament-platform rollout status deploy/tournament-api
kubectl -n tournament-platform rollout history deploy/tournament-api
kubectl -n tournament-platform rollout undo deploy/tournament-api

# 7a. Навмисна помилка: неіснуючий тег -> ImagePullBackOff
kubectl -n tournament-platform set image deploy/tournament-api api=ghcr.io/katushhiaa/tournament-platform-server:9.9.9
kubectl -n tournament-platform get pods
kubectl -n tournament-platform describe pod -l app.kubernetes.io/name=tournament-api | sed -n '/Events:/,$p'

# 7c. Логи та вхід у контейнер
kubectl -n tournament-platform logs deploy/tournament-api --tail=50
kubectl -n tournament-platform exec -it deploy/tournament-api -- sh

# 7d. Виправлення
kubectl -n tournament-platform set image deploy/tournament-api api=ghcr.io/katushhiaa/tournament-platform-server:1.0.0
kubectl -n tournament-platform rollout status deploy/tournament-api
```

Знімки екрана або збережений вивід команд для пунктів 5–7 — окремо у звіті лабораторної.

## Подяки

Автор висловлює щиру подяку кабачку за моральну під…
