# Лабораторні роботи DevOps: карта репозиторію та runbook

Один застосунок (ZVYTIAHA) проходить через усі лабораторні. Нижче — що в репозиторії,
що треба зробити на власному кластері, і що не могло бути перевірено в середовищі, де писався код.

| ЛР | Тема | Де в репозиторії | Статус |
|----|------|------------------|--------|
| 1 | Контейнеризація | `server/Dockerfile`, `client/Dockerfile`, `docker-compose.yml` | Dockerfile-и є; `docker compose config` проходить; образ клієнта зібрано і перевірено (SPA + проксі) |
| 2 | CI | `.github/workflows/ci.yml` | Пайплайн написано; кроки build/test/lint перевірено локально (див. нижче); branch protection — вручну в GitHub |
| 3 | Kubernetes | `k8s/` + `k8s/README.md` | Маніфести написано й перевірено (YAML, мітки); розгортання на кластері не виконано |
| 4 | Helm | `charts/tournament-platform/` + README | `helm lint` і `helm template` проходять для dev/minikube/prod; `helm install` не виконано |
| 5 | GitOps (Argo CD) | `bootstrap/root-app.yaml`, `argocd-apps/`, `charts/…/values-minikube.yaml` | Структура App-of-Apps; встановлення Argo CD і демонстрація — на кластері |
| 6 | Rollouts (canary) | `argocd-apps/argo-rollouts.yaml`, `templates/api-rollout.yaml`, `values-minikube.yaml` | Конфіг написано; demo на кластері |
| 7 | Observability | `argocd-apps/{kube-prometheus-stack,loki,alloy}.yaml`, `templates/servicemonitor.yaml`, `analysis-template.yaml`, `prometheusrule.yaml`, `dashboard-configmap.yaml`, `files/dashboards/` | Конфіг написано; alloy потребує версії чарта; demo на кластері |

## Що перевірено в середовищі збірки

- Збірка і юніт-тести бекенду (`dotnet build`, `dotnet test server/Tests`): проходять; 43 тести.
- Бекенд запущено локально: `/version` повертає `APP_VERSION`, `/metrics` віддає `http_requests_total{method,route,status}`,
  логи в stdout — плоский JSON (`level`, `message`, `method`, `route`, `status`, `durationMs`),
  `FAIL_RATE=1` дає HTTP 500 з `InjectedFailure`, `FAIL_RATE=0` не змінює поведінку.
- Клієнт: `npm run build` проходить; `npm run lint` — **падає на 21 помилці в наявному коді** (див. нижче).
- `docker compose config`, образ клієнта, nginx-шаблон (`API_UPSTREAM`).
- `helm lint` / `helm template` (dev, minikube, prod з `--set`-секретами; prod без секретів падає).
- YAML усіх маніфестів і Argo-застосунків розбирається; мітки маскота валідні.

## Що не перевірено (потрібен доступ, якого не було)

- Збірка образу сервера: `deb.debian.org` недоступний із контейнерної збірки; Dockerfile сервера не змінювався.
- Публікація в GHCR, branch protection, пайплайн на GitHub: токен GitHub у середовищі невалідний.
- Жодного розгортання в кластері (minikube/Argo CD/Prometheus/Loki). Версію чарта Grafana Alloy не отримано
  (`targetRevision: VERIFY-LATEST-VERSION` у `argocd-apps/alloy.yaml`).

## Відкриті питання, які треба вирішити

1. **Lint клієнта червоний.** 21 помилка в `.vue`/`.ts` (`any`, `v-if` разом із `v-for`, невикористані змінні).
   Блокуючий крок у CI не пройде, доки їх не виправлено. Це поза межами ЛР — рішення за вами.
2. **Форматування сервера.** `dotnet format whitespace` застосовано до всього солюшену окремим комітом
   (переважно кінці рядків/BOM). Міграції EF виключено з перевірки.
3. **Мітка маскота.** Вимога «`mascot: кабачок`» (ЛР-3) і «`mascot: гарбуз`» (ЛР-4) неможлива: значення мітки
   Kubernetes не допускає кирилицю. Використано `mascot: kabachok` / `garbuz` у мітках і оригінальну назву в анотації.
4. **Розділ «Подяки».** У PDF методичок текст рядка обрізаний (`…за моральну під…`, `…не довелос…`), повний текст
   у файлах відсутній. У README записано видиму частину.
5. **Argo CD читає гілку `main`.** Поки зміни не змерджено в `main`, `bootstrap/root-app.yaml` нічого не знайде.

## Порядок для студента (ЛР-5…7 на власному minikube)

```bash
# Передумови
minikube start --driver=docker
minikube addons enable ingress metrics-server

# ЛР-5: Argo CD
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
kubectl -n argocd rollout status deploy/argocd-server
kubectl -n argocd port-forward svc/argocd-server 8080:443 &
# Початковий пароль адміна: kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath='{.data.password}' | base64 -d
# Змінити пароль в UI або: argocd account update-password

# Один раз вручну — лише кореневий застосунок (App of Apps):
kubectl apply -f bootstrap/root-app.yaml
```

Після того як зміни з гілки потрапили в `main`, Argo CD сам створить `tournament-platform`, `argo-rollouts`,
`kube-prometheus-stack`, `loki`, `alloy`. Порядок подів: спершу CRD (wave `-1`), потім застосунок (wave `1`).

Типові перевірки:

```bash
argocd app get root --refresh
argocd app list
kubectl -n argocd get applications
kubectl -n tournament-platform get rollout,pods,svc
```

### ЛР-5 (дрейф і відкат)

```bash
kubectl -n tournament-platform scale deploy/... --replicas=1   # для Deployment; для Rollout: kubectl scale rollout ...
# Без selfHeal: app OutOfSync. З selfHeal: Argo CD повертає значення з Git.
# Неіснуючий тег у Git -> Synced, але Degraded. Відкат: git revert <commit> && git push
argocd app history tournament-platform
```

### ЛР-6 (canary з Argo Rollouts)

```bash
kubectl argo rollouts version
kubectl argo rollouts get rollout tp-tournament-platform -n tournament-platform --watch
kubectl argo rollouts promote tp-tournament-platform -n tournament-platform
kubectl argo rollouts abort tp-tournament-platform -n tournament-platform
kubectl argo rollouts dashboard   # http://localhost:3100
```

Імена: релізу `tp` (`releaseName: tp` в `argocd-apps/tournament-platform.yaml`), тому Rollout називається
`tp-tournament-platform-api`. Для canary трафік ділиться за кількістю подів; запити для перевірки запускати
всередині кластера (`kubectl run curl …`), бо `port-forward` з'єднується з одним подом.

### ЛР-7 (спостережуваність і автовідкат)

```bash
kubectl -n monitoring port-forward svc/kube-prometheus-stack-grafana 3000:80        # Grafana (логін admin; пароль — з secret, не з Git)
kubectl -n monitoring port-forward svc/kube-prometheus-stack-prometheus 9090:9090   # Prometheus: Status -> Targets
```

- Метрики: `http_requests_total`, `http_request_duration_seconds` (labels: `method`, `route`, `status`).
- Логи: LogQL `{namespace="tournament-platform", component="api"} | json | status >= 500`.
- Погана версія: у `values-minikube.yaml` задати `api.failRate: "0.5"` і новий тег образу, закомітити, push.
  Очікування: AnalysisRun `Failed` → автоматичний abort → повернення на stable. Відкат через `git revert`.
- Дашборд «ZVYTIAHA: Tournament API» з'явиться в Grafana через sidecar (ConfigMap з міткою `grafana_dashboard`).

## Джерела версій

- `argo-rollouts` 2.43.9, `kube-prometheus-stack` 92.22.0, `loki` 7.5.0 — з OCI-реєстру ghcr.io (наявні теги).
- `grafana/alloy` — не отримано; вказати через `helm search repo grafana/alloy`.
