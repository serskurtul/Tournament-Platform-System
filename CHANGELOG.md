# Changelog

Формат — [Keep a Changelog](https://keepachangelog.com/uk/1.1.0/), версії — [SemVer](https://semver.org/lang/uk/).
Версія контракту API — `info.version` у [docs/api/openapi.yaml](docs/api/openapi.yaml). Знімки документації — теги `docs-vX.Y`.

## [Unreleased]

### Added

- База знань MkDocs Material за чотирирівневою пірамідою (`docs/`), деплой на GitHub Pages (`.github/workflows/docs.yml`).
- Контракт API `docs/api/openapi.yaml` (1.1.1): login, sport, participants, participants/count; правила Spectral (`.spectral.yaml`).
- Контрактний тест (Schemathesis), генерація TypeScript SDK (OpenAPI Generator).
- Storybook для `ConfirmModal` (Default / Danger / Disabled).
- `GET /api/v1/tournaments/{id}/participants/count` — кількість активних учасників.
- Front matter-паспорти (owner, status, review_date, tags), CODEOWNERS для `docs/`, аудит `review_date` у CI.

### Changed

- `TournamentService` розбито на п'ять сервісів (ADR-0002); репозиторії переведено на композицію.
- У Development API дозволяє CORS з сайту документації, щоб «Try it out» у Swagger UI працював проти локального API.

### Fixed

- `GET /api/v1/users/me` повертає профіль власника токена замість заглушки з випадковим `id` (RFC-001); додано відповіді 401/404. Контракт API 1.2.0.
- Дати в JSON-відповідях тепер у RFC 3339 UTC (`...Z`). Раніше значення без зони клієнт читав як локальний час (знайдено контрактним тестом).
- Відповіді 500 від `ExceptionHandlingMiddleware` тепер у camelCase, як решта API (схема `Error`).

## [1.2.0] — 2026-11-21

### Security

- Оновлено AutoMapper до 15.1.4 і System.IdentityModel.Tokens.Jwt до 8.14.0 (HIGH CVE), оновлено пакети ОС в образах (Trivy).

## [1.1.0] — 2026-11-21

### Added

- CI-конвеєр: lint, build, test, сканування образів Trivy, публікація в GHCR.
- Kubernetes-маніфести, Helm-чарт, Argo CD App-of-Apps, Argo Rollouts, моніторинг (Prometheus/Grafana).
- `GET /version`, метрики Prometheus, JSON-логи, `FAIL_RATE` для fault injection.

## [1.0.1] — 2026-06-21

- Release Candidate курсового проєкту (MVP: реєстрація, турніри, сітка single elimination).

[Unreleased]: https://github.com/serskurtul/Tournament-Platform-System/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/serskurtul/Tournament-Platform-System/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/serskurtul/Tournament-Platform-System/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/serskurtul/Tournament-Platform-System/releases/tag/v1.0.1
