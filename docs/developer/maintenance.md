---
owner: Скуртул Сергій
status: published
review_date: 2027-02-19
tags: [developer, process]
---

# Регламент підтримки документації

**Головне правило: зміна коду = зміна документації.** PR, який змінює поведінку (API, модель даних, розгортання), не зливається в `main` без оновлення зачеплених документів. Автоматична перевірка: джоба `doc-contract` у [`quality-metrics.yml`](https://github.com/serskurtul/Tournament-Platform-System/blob/main/.github/workflows/quality-metrics.yml) запускає `scripts/quality/doc_contract_check.py`, яка падає, якщо маршрут API відсутній у [SDD](../architecture/sdd.md) або порядок старту сервісів у `docker-compose.yml` не збігається з [ISD](../architecture/isd.md). Решту рев'юер перевіряє за чекбоксом у шаблоні PR.

Цикли перегляду: експлуатаційна документація — не рідше ніж раз на **90 днів**, архітектурна — раз на **180 днів**. Перегляд = звірити з кодом, оновити дату в таблиці, закомітити.

| Документ | Категорія | Власник | Цикл перегляду | Дата останнього перегляду | Наступний перегляд |
|----------|-----------|---------|---------------:|---------------------------|--------------------|
| [README.md](https://github.com/serskurtul/Tournament-Platform-System/blob/main/README.md) | експлуатаційна | Анур'єва Катерина (PM) | 90 днів | 2026-11-21 | 2027-02-19 |
| [docs/architecture/isd.md](../architecture/isd.md) | експлуатаційна | Скуртул Сергій (Backend / DevOps) | 90 днів | 2026-11-21 | 2027-02-19 |
| [docs/architecture/sdd.md](../architecture/sdd.md) | архітектурна | Скуртул Сергій (Backend) | 180 днів | 2026-11-21 | 2027-05-20 |
| [docs/architecture/adr/](../architecture/adr/README.md) | архітектурна | Скуртул Сергій, Дудко Володимир | 180 днів (нові ADR — при кожному рішенні) | 2026-11-21 | 2027-05-20 |
| [data-dictionary.md](../architecture/data-dictionary.md) | технічна | Дудко Володимир (DB) | 180 днів | 2026-06-11 | **прострочено** → 2026-12-08 |
| [docs/api/error-catalog.md](../api/error-catalog.md) | технічна | Скуртул Сергій | 180 днів | 2026-11-21 | 2027-05-20 |
| [docs/developer/test-plan.md](test-plan.md) | технічна | Загребенюк Богдан (QA) | 180 днів | 2026-04-07 | **прострочено** → 2026-12-08 |
| [docs/Data_Flow_Specification.md](../architecture/data-flow-specification.md) | архітектурна | Скуртул Сергій | 180 днів | 2026-04-06 | **прострочено** → 2026-12-08 |
| [client/README.md](https://github.com/serskurtul/Tournament-Platform-System/blob/main/client/README.md) | технічна | Ярмолюк Людмила (Frontend) | 180 днів | 2026-11-21 | 2027-05-20 |
| [docs/developer/handover.md](handover.md) | експлуатаційна | Анур'єва Катерина (PM) | 90 днів | 2026-11-21 | 2027-02-19 |

Історичні записи (звіти лабораторних, `ADR_System_Style.md`, deprecated `SAD_Internal_View.md`, повідомлення комітів) — документація процесу. Вони не оновлюються, а лише позначаються `Deprecated` / `Superseded`.

Перевірка актуальності: `python3 scripts/quality/doc_audit.py` (вік, зміни коду після оновлення, биті посилання).
