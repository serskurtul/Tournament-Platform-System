# Architecture Decision Records

Рішення фіксуються в момент прийняття і заднім числом не переписуються. Застаріле отримує статус `Superseded by ADR-XXXX`, а нове рішення — новий номер.

| № | Рішення | Статус |
|---|---------|--------|
| [ADR-0001](0001-postgresql-with-ef-core.md) | PostgreSQL + EF Core як основне сховище | Accepted |
| [ADR-0002](0002-decompose-tournament-service.md) | Декомпозиція `TournamentService` за відповідальностями з контрактами через DI | Accepted |

Ранній запис про макростиль системи (модульний моноліт) — [`docs/ADR_System_Style.md`](0000-modular-monolith-system-style.md), до введення нумерації.
