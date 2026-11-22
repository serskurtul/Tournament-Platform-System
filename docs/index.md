---
owner: Скуртул Сергій
status: published
review_date: 2027-02-19
tags: [overview]
---

# ZVYTIAHA — база знань

ZVYTIAHA — веб-платформа турнірів. Організатори створюють турнір за кілька хвилин, набирають учасників і ведуть сітку single elimination. Гравці знаходять турніри за видом спорту, подають заявки й бачать свої матчі.

**Швидкий старт** (повна версія — у [README](https://github.com/serskurtul/Tournament-Platform-System#readme)):

```bash
cp .env.example .env
docker compose up --build    # UI: http://localhost:5173, API: http://localhost:5050/swagger
```

## Піраміда документації

| Рівень | Питання | Розділ |
|--------|---------|--------|
| 1 | Що це і як запустити? | ця сторінка, [README](https://github.com/serskurtul/Tournament-Platform-System#readme) |
| 2 | Як долучитися до розробки? | [Розробникам](developer/index.md) |
| 3 | Як інтегруватися? | [API](api/index.md) |
| 4 | Чому система влаштована саме так? | [Архітектура](architecture/index.md) |

Кожен факт живе в одному місці (Single Source of Truth): контракти API — в [openapi.yaml](api/index.md), структура — в [SDD](architecture/sdd.md), розгортання — в [ISD](architecture/isd.md).
