---
owner: Скуртул Сергій
status: published
review_date: 2027-02-19
tags: [developer, ops]
---

# Локальне середовище

Усе разом у контейнерах — `docker compose up --build` (див. [ISD §2](../architecture/isd.md#2-топологія-розгортання)). Нижче — запуск частин окремо для розробки.

| Частина | Вимоги | Команди |
|---------|--------|---------|
| БД | Docker | `docker compose up -d db` |
| Міграції | .NET 9 SDK, `dotnet tool install -g dotnet-ef` | `cd server/TournamentPlatformSystemWebApi.Infrastructure && dotnet ef database update -s ../TournamentPlatformSystemWebApi.API` |
| API | .NET 9 SDK | `cd server/TournamentPlatformSystemWebApi.API && dotnet run` (рядок підключення — `ConnectionStrings__DefaultConnection`) |
| Тести бекенду | .NET 9 SDK | `dotnet test server/Tests` |
| Фронтенд | Node 22 | `cd client && npm ci && npm run dev`; перевірки: `npm run lint && npm run build` |
| Документація | Python 3.12 | `pip install -r docs/requirements.txt && mkdocs serve` → <http://127.0.0.1:8000> |

Змінні середовища без секретів — [ISD §3](../architecture/isd.md#3-змінні-середовища-без-секретів).
