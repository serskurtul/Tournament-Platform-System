---
owner: Скуртул Сергій
status: published
review_date: 2027-02-19
tags: [api]
---

# API

Контракт [`openapi.yaml`](https://github.com/serskurtul/Tournament-Platform-System/blob/main/docs/api/openapi.yaml) — єдине джерело правди (SSOT) для інтеграції. Довідка нижче рендериться з нього, тому розійтися з контрактом не може. Повний перелік маршрутів — у [SDD §4](../architecture/sdd.md), коди помилок — у [каталозі помилок](error-catalog.md).

| Що | Як отримати |
|----|-------------|
| Перевірка контракту | `npx @stoplight/spectral-cli lint docs/api/openapi.yaml` (правила — `.spectral.yaml`) |
| TypeScript SDK | `scripts/docs/generate_sdk.sh` → `build/sdk/typescript` (openapi-generator, `typescript-fetch`) |
| Контрактний тест | `scripts/docs/contract_test.sh` (Schemathesis проти запущеного API) |

Автентифікація: `Authorization: Bearer <accessToken>` з відповіді `POST /api/v1/auth/login`.

<swagger-ui src="openapi.yaml"/>
