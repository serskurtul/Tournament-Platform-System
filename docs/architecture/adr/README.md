---
owner: Скуртул Сергій
status: published
review_date: 2027-05-20
tags: [architecture, adr]
---

# Architecture Decision Records

Рішення фіксуються в момент прийняття і заднім числом не переписуються. Застаріле отримує статус `Superseded by ADR-XXXX`, а нове рішення — новий номер.

| № | Рішення | Статус |
|---|---------|--------|
| [ADR-0000](0000-modular-monolith-system-style.md) | Модульний моноліт як макростиль системи | Accepted |
| [ADR-0001](0001-postgresql-with-ef-core.md) | PostgreSQL + EF Core як основне сховище | Accepted |
| [ADR-0002](0002-decompose-tournament-service.md) | Декомпозиція `TournamentService` за відповідальностями з контрактами через DI | Accepted |
| [ADR-0003](0003-documentation-as-code.md) | Documentation as Code: MkDocs Material + OpenAPI-контракт | Accepted |

## Життєвий цикл

`Proposed` → `Accepted` → `Superseded by ADR-XXXX` (або `Rejected`). Нумерація наскрізна, номер не перевикористовується. Прийнятий запис незмінний: щоб переглянути рішення, створюється новий ADR, а в старому змінюється лише рядок статусу на `Superseded by ADR-XXXX`.

Запис ADR-0000 створено до введення нумерації; у ЛР-11 йому присвоєно номер 0000 без зміни змісту.

Контекст рішень: [SDD](../sdd.md) (що реалізовано) і [ISD](../isd.md) (як розгорнуто).
