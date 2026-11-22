---
owner: Скуртул Сергій
status: published
review_date: 2027-02-19
tags: [developer, process]
---

# Як долучитися до розробки

## Робочий процес

- Гілка від `main`: `feature/<коротко>`, `fix/<коротко>`, `docs/<коротко>`.
- Коміти в стилі Conventional Commits: `feat(api): …`, `fix(client): …`, `docs(architecture): …`.
- Pull request з шаблоном; CI (`ci.yml`, `quality-metrics.yml`, `docs.yml`) має бути зеленим; рев'ю власника за [CODEOWNERS](https://github.com/serskurtul/Tournament-Platform-System/blob/main/.github/CODEOWNERS).

## Definition of Done: docs-or-don't-merge

PR, що змінює **публічну поведінку** (новий або змінений ендпоінт, формат відповіді, конфігурація, порядок розгортання), не зливається, доки не виконано все:

- [ ] Оновлено контракт [`docs/api/openapi.yaml`](../api/index.md) (і SemVer у `info.version`)
- [ ] Оновлено зачеплені сторінки: [SDD](../architecture/sdd.md), [ISD](../architecture/isd.md), README; для архітектурного рішення — новий [ADR](../architecture/adr/README.md)
- [ ] Додано запис у [CHANGELOG](https://github.com/serskurtul/Tournament-Platform-System/blob/main/CHANGELOG.md)
- [ ] У змінених сторінках оновлено `review_date` у front matter
- [ ] Зелені автоматичні перевірки документації (таблиця нижче)

| Перевірка | Інструмент | Де |
|-----------|-----------|----|
| Контракти API | Spectral (`.spectral.yaml`, 0 errors) | `docs.yml` |
| Розмітка | markdownlint (`.markdownlint-cli2.yaml`) | `docs.yml` |
| Посилання, навігація | `mkdocs build --strict` | `docs.yml` |
| Маршрути коду ↔ SDD, compose ↔ ISD | `scripts/quality/doc_contract_check.py` | `quality-metrics.yml` |
| Актуальність | `scripts/quality/review_date_audit.py` (прострочені `review_date`) | `docs.yml` |

Застарілу сторінку не видаляємо: ставимо `status: deprecated` у front matter і додаємо посилання на актуальний документ.
