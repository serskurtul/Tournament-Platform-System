# ZVYTIAHA — web client

Vue 3 + TypeScript + Vite SPA. In containers it is served by nginx, which proxies `/api/` to the backend (`API_UPSTREAM`).

## Scripts

| Command | What it does |
|---------|--------------|
| `npm ci` | install dependencies from the lock file |
| `npm run dev` | dev server with hot reload (expects the API on the same origin or a proxy) |
| `npm run lint` | ESLint (flat config `eslint.config.mjs`), blocking in CI |
| `npm run build` | type-check (`tsc -b`) and production bundle into `dist/` |

## Structure

`src/api` HTTP client and token refresh · `src/services` API calls · `src/stores` Pinia state · `src/views` pages · `src/components` UI blocks · `src/router` routes.

System-level documentation: [SDD](../docs/architecture/sdd.md) (components, API), [ISD](../docs/architecture/isd.md) (deployment).
