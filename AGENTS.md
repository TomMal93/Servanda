# Repository instructions

## Start here

- Read `docs/development-guide.md` before changing code or project structure.
- Treat that document as the source of truth for the product, stack, architecture, data safety, and definition of done.
- Inspect the working tree before editing and preserve unrelated user changes.

## Technical constraints

- Keep the agreed stack: React, TypeScript, Vite, .NET 10, ASP.NET Core Minimal API, EF Core, and SQLite.
- Keep `npm run dev` as the single command that starts the complete development environment.
- Frontend code must call relative `/api` URLs; Vite owns the development proxy configuration.
- Keep domain code independent of ASP.NET Core, EF Core, React, and infrastructure concerns.
- Do not add a production dependency, framework, state-management library, or architectural layer without a concrete requirement.

## Data safety

- Never use the user's `data/servanda.db` in automated tests.
- Use isolated temporary databases for integration tests.
- Manage schema changes with EF Core migrations; do not use `EnsureCreated()` as the application schema strategy.
- Do not run destructive migrations or delete local data without explicit user approval.
- Do not log note contents, secrets, connection strings, or local filesystem details.

## Verification

- Add or update tests for behavior changes and regressions.
- After implementation, run the relevant focused tests, then `npm test` and `npm run build` when those scripts are available.
- For UI changes, verify the result in a browser and cover critical flows with Playwright where appropriate.
- Report exactly what was verified and any checks that could not be run.
- Update documentation when commands, architecture, schema, or API contracts change.

## Scope discipline

- Prefer the smallest complete change that satisfies the request.
- Avoid speculative abstractions and unrelated cleanup.
- Do not commit, push, publish, or perform destructive Git operations unless the user requests it.

