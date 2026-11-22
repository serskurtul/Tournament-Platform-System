---
owner: Скуртул Сергій
status: published
review_date: 2027-02-20
tags: [ops, monitoring]
---

# Каталог алертів

Правило: алерт без runbook — це шум. Кожен алерт має `runbook_url`. Визначення —
[`charts/tournament-platform/templates/prometheusrule.yaml`](https://github.com/serskurtul/Tournament-Platform-System/blob/main/charts/tournament-platform/templates/prometheusrule.yaml), SLO — [`docs/slo.yaml`](https://github.com/serskurtul/Tournament-Platform-System/blob/main/docs/slo.yaml).

| Назва | Умова спрацювання | Серйозність | Відповідальна роль | Runbook |
|-------|-------------------|-------------|--------------------|---------|
| `TournamentApiHighErrorRate` | частка 5xx серед усіх запитів API > 5% за 2 хв, триває 1 хв | warning (SEV-2) | L2 backend on-call | [RB-001 §1](RB-001.md#1-тригер) |
| `TournamentApiDown` | `sum(up{job=api}) == 0` протягом 2 хв | critical (SEV-1) | L2 SRE on-call | [RB-001 §1](RB-001.md#1-тригер), [ISD §7.3](../architecture/isd.md#73-відновлення-після-відмови) |
| `TournamentBannerUploadFailing` | > 50% запитів `POST api/v1/tournaments/{id}/image` за 15 хв — 5xx, і спроб ≥ 3; триває 5 хв (CAPA CA-2, [INC-001](../incidents/INC-001.md)) | warning (SEV-3) | L2 backend on-call → L3 (GCP) | [RB-002](RB-002.md) |
| `TournamentDatabaseDown` | у deployment PostgreSQL 0 доступних реплік протягом 2 хв | critical (SEV-1) | L2 SRE on-call → L3 DB | [RB-001](RB-001.md) |
| `TournamentDatabaseBackupStale` | остання успішна копія старша за 26 год (RPO 24 год + запас на тривалість job), триває 10 хв | warning (SEV-3) | L2 SRE | [RB-001 §7](RB-001.md#7-резервна-копія-не-створилась) |

Серйозність алерту відповідає рівню SEV з [матриці ескалації](../maintenance-plan.md#матриця-ескалації).
