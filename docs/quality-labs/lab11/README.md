# ЛР-11. Documentation as Code: база знань, API-first контракт, конвеєр «не збирається — не їде»

Проєкт: ZVYTIAHA (Tournament Platform System). Дата: 2026-11-22.
Сайт: <https://serskurtul.github.io/Tournament-Platform-System/> (публікується конвеєром `docs.yml` після push у `main` і вмикання Pages).

Позначка **[ДЛЯ СТУДЕНТА]** — кроки, які потребують твого акаунта або GUI (GitHub UI, Obsidian). Їх не підроблено і не імітовано.

## Завдання 1. Піраміда документації, MkDocs, front matter, ADR

### 1.1 Піраміда

| Рівень | Питання | Каталог | Сторінки |
|--------|---------|---------|----------|
| 1 | Що це і як запустити? | `README.md`, `docs/index.md` | призначення, швидкий старт, карта бази знань |
| 2 | Як долучитися? | `docs/developer/` | contributing (DoD), local-setup, naming-conventions (стандарти ЛР-1), test-plan, maintenance, handover |
| 3 | Як інтегруватися? | `docs/api/` | `openapi.yaml`, Swagger UI, error-catalog |
| 4 | Чому так? | `docs/architecture/` | SDD, ISD (ЛР-10), ADR-0000…0003, data-dictionary / integrity / flow, SAD (deprecated) |

Переміщення зроблено `git mv` (коміт `3c21c09`), тож історія файлів збереглася.

### 1.2 MkDocs Material

`mkdocs.yml`: тема `material`, `language: uk`, nav за чотирма рівнями, плагіни `search`, `mermaid2` (C4-діаграми SDD), `git-revision-date-localized` (дата останнього редагування з Git) і `swagger-ui-tag`. Залежності зафіксовано в `docs/requirements.txt`. Через `validation` сторінка поза nav або мертве посилання дає попередження, яке `--strict` перетворює на помилку. Звіти лабораторних і маркетинг винесено з сайту через `exclude_docs`.

Скріншот головної сторінки: `evidence/mkdocs-home.png`.

### 1.3 Front matter і CODEOWNERS

Кожна сторінка сайту (22) має паспорт:

```yaml
---
owner: Скуртул Сергій
status: published        # draft | in-review | published | deprecated
review_date: 2027-02-19  # 90 днів для developer/api, 180 — для architecture (цикли з ЛР-10)
tags: [developer, process]
---
```

SAD переведено в `status: deprecated` з переспрямуванням на SDD. Сторінку не видалено.

`.github/CODEOWNERS` призначає `@serskurtul` власником `/docs/`, `mkdocs.yml`, `.spectral.yaml`, контракту й CHANGELOG.

Branch protection для `main`: *Require a pull request* (1 approval), *Require review from Code Owners*, обов'язковий status check `build`. Тепер зміна в `docs/` без рев'ю власника не зливається.

![Branch protection](evidence/screenshots/branch-protection.png)

*Settings → Branches → правило для `main`.*

### 1.4 Кущ ADR і граф знань

| ADR | Рішення | Статус |
|-----|---------|--------|
| 0000 | Модульний моноліт | Accepted (номер присвоєно під час міграції, зміст не змінено) |
| 0001 | PostgreSQL + EF Core | Accepted |
| 0002 | Декомпозиція TournamentService | Accepted |
| 0003 | Documentation as Code | Accepted |

Канон (журнал `adr/README.md`): наскрізна нумерація, `Proposed → Accepted → Superseded by ADR-XXXX`, прийнятий запис незмінний. Записів зі статусом Superseded поки немає: жодне рішення ще не переглядалося, і вигадувати його я не став.

Зв'язність: кожен ADR посилається на SDD, ISD і журнал. `scripts/docs/link_graph.py` будує той самий граф, що й Obsidian (посилання між сторінками), і падає, якщо є ізольовані сторінки. Результат: **22 сторінки, 62 зв'язки, 1 компонента, 0 «островів»** (`evidence/link-graph.txt`, Mermaid-граф — `evidence/knowledge-graph.md`). Перевірка входить у `docs.yml`.

Wikilinks `[[...]]` не використано: MkDocs їх не розуміє і strict-збірка впала б. Obsidian будує граф і зі звичайних Markdown-посилань, а теги бере з front matter.

![Obsidian graph](evidence/screenshots/obsidian-graph.png)

*Obsidian, vault = `docs/`, Graph view з тегами: один зв'язний граф. Кущ ADR (червоні вузли 0000–0003) прив'язаний до README, SDD, ISD; блоки експлуатації (RB, INC, alerts, maintenance-plan) і змін (RFC, CCB, release-checklist) з'єднані з архітектурою. Сірі вузли (CHANGELOG, CONTRIBUTING, docs.yml) — посилання на файли поза vault.*

## Завдання 2. API-first контракт

### 2.1 Контракт `docs/api/openapi.yaml`

OpenAPI 3.0.3, `info.version: 1.1.1` (SemVer). Операції: `POST /api/v1/auth/login`, `GET /api/v1/sport`, `GET /api/v1/tournaments/{id}/participants`, `GET /api/v1/tournaments/{id}/participants/count` (ендпоінт з ЛР-10).

- Схеми зроблено з реальних DTO (`LoginRequestDto`, `LoginResponseDto`, `SportDto`, `TeamDto`, анонімний об'єкт count).
- Помилки йдуть через спільні `components/responses` → схема `Error` (= `ErrorResponseDto`).
- Приклади є для кожної успішної відповіді й для помилки валідації.

### 2.2 Swagger UI, «Try it out», SDK

- Swagger UI вбудовано в сторінку `api/index.md` тегом `<swagger-ui src="openapi.yaml"/>`, скріншот — `evidence/mkdocs-api-swagger-ui.png`.
- «Try it out» для `countActiveParticipants` виконано зі сторінки документації проти запущеного API: **200**, `{"tournamentId":"33333333-…-000000000001","activeParticipants":4}` (`evidence/swagger-try-it-out-participants-count.png`).
- Перша спроба впала на CORS: API дозволяв лише origin фронтенду. У Development додано origin сайту документації (`DOCS_ORIGINS`); продакшн-політика не змінилась.
- SDK: `scripts/docs/generate_sdk.sh` (openapi-generator-cli v7.10.0, `typescript-fetch`) → `build/sdk/typescript`: `AuthApi`, `SportApi`, `ParticipantsApi` + моделі. `tsc --noEmit` проходить без помилок (`evidence/sdk-generation.txt`). Каталог `build/` не комітиться, бо SDK відтворюється з контракту.

### 2.3 Spectral

`.spectral.yaml` розширює `spectral:oas` і додає правила:

- `operationId`, tags і summary обов'язкові;
- 4xx/5xx лише через `components/responses`;
- маршрути версіоновані (`/api/vN/`);
- успішна відповідь має приклад (warn).

Результат: **0 errors, 0 warnings**. Щоб довести, що правила реально спрацьовують, прогнав навмисно зіпсований контракт: 2 errors (`evidence/spectral.txt`).

Дорогою знайшов дві пастки, які зробили б правило «мертвим»:

1. На відсутньому полі Spectral функцію не викликає, тому додано `truthy`.
2. Spectral розв'язує `$ref` до перевірки, тому правилу потрібен `resolved: false`.

### 2.4 SSOT і контрактний дрейф

Довідка не може розійтися з контрактом: Swagger UI на сайті читає той самий `openapi.yaml`, і в репозиторії немає другої копії. Відповідність контракту живому коду перевіряє `scripts/docs/contract_test.sh` (Schemathesis 3.39.16 проти API + PostgreSQL з міграціями й seed).

Знайдені розбіжності (дефекти документації та коду):

| # | Розбіжність | Рішення |
|---|-------------|---------|
| 1 | Невалідний `id` (не UUID) → 400, контракт описував лише 200/404 | Додано `400` до контракту (реальна поведінка) |
| 2 | Дати `createdAt`/`updatedAt` без часової зони (`2026-11-21T20:14:41.905285`), не RFC 3339; браузер читає їх як локальний час | **Виправлено в коді**: `UtcDateTimeJsonConverter` + 3 юніт-тести (тестів тепер 48) |
| 3 | 500-ті з `ExceptionHandlingMiddleware` мали PascalCase (`"Error":{"Code":…}`) — порушення схеми `Error` | **Виправлено**: серіалізація з `JsonSerializerOptions.Web` |
| 4 | Запит з NUL-байтом (`%00`) у шляху → порожній 400 від Kestrel до застосунку | Поведінка веб-сервера поза контрактом; генерацію `\x00` вимкнено з коментарем у скрипті |

Підсумковий прогін: **4/4 операції, 154/154 перевірки** (`not_a_server_error`, `status_code_conformance`, `content_type_conformance`, `response_schema_conformance`) — `evidence/contract-test-pass.txt`.

### 2.5 Варіативний пункт: Storybook

Проєкт має UI (Vue 3), тому зроблено Storybook 10.6.8 (`@storybook/vue3-vite`) для `ConfirmModal`:

- `Default` — основний стан;
- `Danger` — error/незворотна дія;
- `Disabled` — `loading`, обидві кнопки вимкнено.

Сторінка автодокументації генерується з JSDoc. Запуск: `npm run storybook`, збірка — `npm run build-storybook`. Скріншоти: `evidence/storybook-confirmmodal-*.png`.

Знахідка: кнопки мають фіксовану ширину 120 px, і довгий підпис («Дискваліфікувати») виходить за межі кнопки. У story використано короткий підпис, а дефект компонента лишено на окреме виправлення.

## Завдання 3. Конвеєр документації

### 3.1 `.github/workflows/docs.yml`

checkout (повна історія для дат) → Python 3.12 + `docs/requirements.txt` → **markdownlint** → **Spectral** (`--fail-severity error`) → **аудит review_date** → **граф знань** → **`mkdocs build --strict`** → `upload-pages-artifact` → `deploy-pages`. Деплой — лише з `main`. На PR конвеєр тільки перевіряє.

### 3.2–3.3 Краш-тест FAIL → PASS

| Коміт | Дефект | Strict-збірка |
|-------|--------|---------------|
| `docs(ci): inject broken reference into strict build, FAIL` | у `developer/index.md` додано посилання на неіснуючий `runbook.md` | `WARNING - Doc file 'developer/index.md' contains a link 'runbook.md', but the target 'developer/runbook.md' is not found…` → `Aborted with 1 warnings in strict mode!`, **Exit Code 1** |
| `docs(ci): restore strict build pipeline, PASS` | посилання прибрано | markdownlint 0, Spectral 0 errors, audit PASS, graph PASS, **Exit Code 0**; на сторінках є дата редагування з Git |

Локальні логи: `evidence/strict-build-FAIL.txt`, `evidence/strict-build-PASS.txt`.

Протокол у GitHub Actions (FAIL- і PASS-коміти запушено окремо, щоб кожен мав свій run):

![docs #1 FAIL](evidence/screenshots/docs-run1-fail.webp)

*docs #1 на `27d8688 … FAIL` — червоний: strict-збірка впала на мертвому посиланні.*

![docs #2 first attempt](evidence/screenshots/docs-run2-deploy-fail.webp)

*docs #2 на `fea9959 … PASS`, перша спроба: `build` зелений, `deploy` червоний.*

![deploy 404](evidence/screenshots/deploy-pages-not-enabled.webp)

*Причина червоного `deploy`: `HttpError: Not Found … Ensure GitHub Pages has been enabled`. Pages ще не було ввімкнено в налаштуваннях репозиторію; після ввімкнення (Source: GitHub Actions) — Re-run failed jobs.*

![docs #2 success](evidence/screenshots/docs-run2-rerun-success.webp)

*docs #2 після перезапуску: `build` і `deploy` зелені, Status Success.*

![docs runs](evidence/screenshots/docs-runs-fail-pass.webp)

*Підсумок workflow `docs`: #1 FAIL (червоний) → #2 PASS → #3 (реліз) — зелені.*

![commits](evidence/screenshots/commits-fail-pass.webp)

*Вкладка Commits: `27d8688 … FAIL` з ❌ 4/9 перевірок, `fea9959 … PASS` з ✅ 10/10.*

Задеплоєний сайт https://serskurtul.github.io/Tournament-Platform-System/:

![site home](evidence/screenshots/site-home.webp)

*Головна сторінка бази знань (рівень 1, піраміда документації).*

![revision date](evidence/screenshots/site-revision-date.webp)

*Дата останнього редагування сторінки з Git (плагін git-revision-date-localized) — «22 листопада 2026 р.» внизу сторінки.*

![site API](evidence/screenshots/site-api-swagger.webp)

*Рівень 3 · API: Swagger UI, згенерований з `openapi.yaml` (на момент знімка — версія 1.1.1, стан PASS-коміту ЛР-11).*

### 3.4 Definition of Done і супровід

- `docs/developer/contributing.md` (+ корінний `CONTRIBUTING.md` з посиланням): чек-лист **docs-or-don't-merge** — `openapi.yaml` (SemVer), SDD/ISD/ADR, CHANGELOG, `review_date`.
- Той самий пункт додано в PR-шаблон.
- README: таблиця документаційних перевірок (Spectral, markdownlint, strict build, review_date, граф, контракт код↔SDD).
- `scripts/docs/review_date_audit.py` у CI: прострочений `review_date` валить збірку з переліком сторінок і власників (перевірено `--as-of 2027-03-01`: 10 прострочених → FAIL). Сторінки зі `status: deprecated` не блокують збірку.
- `CHANGELOG.md` (Keep a Changelog) і тег **`docs-v1.0`** на PASS-коміті — знімок документації, що відповідає релізу.

## Що не зроблено або обмеження

- Конвеєр `docs.yml` відпрацював у GitHub Actions (FAIL → PASS, деплой на Pages, скріншоти вище). Перший деплой упав, бо Pages не було ввімкнено в налаштуваннях; після ввімкнення jobs перезапущено на тому самому PASS-коміті.
- Для локальної strict-збірки без доступу до unpkg.com mermaid підмінявся локальним файлом; у CI використовується CDN.
- «Лог валідації ШІ-Техліда» не складено.
