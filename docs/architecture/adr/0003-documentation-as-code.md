---
owner: Скуртул Сергій
status: published
review_date: 2027-05-20
tags: [architecture, adr]
---

# ADR-0003: Documentation as Code (MkDocs Material + OpenAPI)

- **Статус:** Accepted
- **Дата:** 2026-11-21

## Контекст

Документація жила в різних місцях (Markdown у `docs/`, PDF, Google Drive, Wiki) без версіонування й перевірок. Аудит ЛР-10 показав застарілі сторінки (SAD — 236 днів, 119 змін коду після оновлення) і «мертві» документи. Контракт API існував лише у вигляді Swagger, згенерованого з коду.

## Розглянуті альтернативи

| Варіант | Плюси | Мінуси |
|---------|-------|--------|
| GitHub Wiki | Нуль налаштувань | Окремий репозиторій, немає рев'ю через PR і CI-перевірок |
| Docusaurus | Потужний, React | Node-стек і власні компоненти — зайвий шар для команди |
| **MkDocs Material + OpenAPI-контракт у репозиторії** | Markdown поруч із кодом, strict-збірка, Mermaid, швидкий старт на Python | Сайт статичний, без коментарів читачів |

## Рішення

Документація — частина репозиторію: піраміда `docs/{developer,api,architecture}`, сайт MkDocs Material на GitHub Pages, контракт `docs/api/openapi.yaml` як єдине джерело для довідки (Swagger UI рендериться з нього) і SDK. Конвеєр `docs.yml`: markdownlint → Spectral → `mkdocs build --strict` → деплой.

## Наслідки

- (+) Документацію рев'юють як код (CODEOWNERS), а битий лінк або сторінка поза навігацією зупиняють збірку.
- (+) Довідка API не може розійтися з контрактом.
- (−) Ручний контракт треба тримати в синхроні з кодом: це закриває контрактний тест і чек-лист DoD.

## Пов'язані документи

[Регламент документації](../../developer/maintenance.md) · [CONTRIBUTING](../../developer/contributing.md) · [API](../../api/index.md) · [журнал ADR](README.md)
