---
paths:
  - "frontend/src/**"
  - "frontend/public/**"
  - "frontend/index.html"
  - "frontend/package.json"
  - "frontend/package-lock.json"
  - "frontend/tsconfig*.json"
  - "frontend/vite.config.ts"
  - "frontend/.env*"
---

# Willy Shop Frontend Rules

These conditional rules apply to the Vue frontend in `frontend/`.

## Technology and conventions

- Use the existing Vue 3 Composition API with `<script setup lang="ts">`, Vite, TypeScript, Pinia, Vue Router, and Axios patterns.
- Preserve strict typing. Do not introduce `any` unless an external untyped boundary makes it unavoidable and the reason is documented locally.
- Explicitly type props, emits, API payloads, API responses, store state, nullable values, and reusable composables.
- Prefer `ref` for mutable scalar or replaceable state and `computed` for derived state. Do not copy derived state into independent mutable variables.
- Keep components focused. Move reusable API access to `src/api`, shared contracts to `src/types`, and cross-view state to Pinia stores when justified.
- Preserve the `@/` import alias and existing formatting conventions.

## Routing and user experience

- Continue using dynamic imports for route components.
- Define route metadata with typed `RouteMeta` fields and preserve authentication and role checks.
- Represent loading, empty, success, validation-error, authorization-error, and network-error states where relevant.
- Do not expose stack traces, backend internals, raw exception text, or sensitive data to users.
- Preserve keyboard access, semantic HTML, labels, focus behavior, and accessible names when changing UI interactions.

## API and security boundaries

- Use the shared Axios client in `frontend/src/api/http.ts`; do not create inconsistent ad hoc clients without a verified need.
- Preserve `withCredentials`, timeout, CSRF-token handling, and centralized error normalization.
- Read environment-dependent values through Vite environment variables. Do not hard-code credentials, production URLs, machine paths, or developer-specific ports.
- When changing a request, response, route, enum, validation rule, or DTO expectation, inspect the corresponding ASP.NET Core contract and endpoint under `WillyShop/`.
- Treat client-side authorization as user experience only; server-side authorization remains mandatory.

## Validation

- Run `npm --prefix frontend run build` after frontend changes. This command includes TypeScript checking and the Vite production build.
- Do not claim ESLint, Prettier, or frontend tests ran unless those tools and scripts have actually been added to the repository and executed.
- Remove unused imports, debug logging, temporary UI, unrequested placeholders, and generated `dist` output before delivery.