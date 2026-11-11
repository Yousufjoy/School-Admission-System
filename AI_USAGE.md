# AI Usage Disclosure

## Tools Used

- **Antigravity IDE / Gemini Assistant**: Used iteratively across Phases 1–5 for scaffolding the .NET 8 solution, generating EF Core entities/migrations, writing ASP.NET Core controllers and exception middleware, building xUnit and Vitest suites, creating the Vite + React + TypeScript + Tailwind CSS frontend, and authoring Docker Compose and documentation files.

---

## Parts Generated with AI Assistance

1. **Backend Scaffolding & Data Layer (`backend/SchoolAdmission.Api`)**:
   - Project structure (`SchoolAdmission.sln`, `SchoolAdmission.Api`, `SchoolAdmission.Tests`), `ClassEntity`, `HoldEntity`, `HoldStatuses`, `IdGenerator`, `AppDbContext`, and the EF Core `InitialCreate` migration.
   - `ClassesController`, `HoldsController`, `MeController`, `HoldOptions`, `AdmissionHelpers`, `GlobalExceptionMiddleware`, and `AdmissionDbExtensions`.
2. **Automated Tests (`backend/SchoolAdmission.Tests`, `frontend/src/App.test.tsx`)**:
   - `Phase1ScaffoldTests`, `AdmissionApiIntegrationTests`, `Phase3RequiredTests` (`Test1_Validation_Returns400_WhenStudentIdIsInvalid`, `Test2_IdempotentConfirm_DoesNotDuplicateSeats`, `Test3_HighConcurrency_OversellingPrevention`), and Vitest component tests.
3. **Frontend & Containerization (`frontend/`, `docker-compose.yml`)**:
   - Responsive Bengali/English admission UI (`HeaderBar`, `ClassCreationForm`, `ClassListSection`, `MyHoldsSection`, `StatusBanner`, `ErrorBoundary`), `admissionApi.ts`, `admissionUtils.ts`, Dockerfiles, `nginx.conf`, and `docker-compose.yml`.

---

## Realistic AI Mistakes Caught & Fixed

### 1. EF Core `FromSqlRaw` + `FirstOrDefaultAsync` Subquery Wrapping & In-Memory Concurrency Gap
- **Mistake**: An initial implementation of `SELECT * FROM "Classes" WHERE "Id" = @classId FOR UPDATE` composed `.FirstOrDefaultAsync()` directly on `FromSqlRaw`, and relied solely on `BeginTransactionAsync()` without accounting for how EF Core `InMemory` ignores transactions and locks. Composing LINQ `.FirstOrDefaultAsync()` over `FromSqlRaw` causes EF Core to wrap the raw SQL in a subquery (`SELECT ... FROM (... FOR UPDATE) LIMIT 1`), and running 50 concurrent `Task.WhenAll` requests against `InMemory` without serialization would oversell seats.
- **How it was caught & fixed**: Reviewed the EF Core SQL translation behavior and replaced `.FirstOrDefaultAsync()` with `.ToListAsync()` followed by `.FirstOrDefault()` in memory (since `Id` is the primary key), sending the exact `SELECT * FROM "Classes" WHERE "Id" = @classId FOR UPDATE` query verbatim to PostgreSQL. Isolated a `SemaphoreSlim`-based `InProcessLockRegistry` fallback strictly for `!dbContext.Database.IsNpgsql()` test runs, while preserving pure PostgreSQL row-level locking in production.

### 2. Vite Environment Variables Inside Static `nginx:alpine` Docker Builds
- **Mistake**: Initially `frontend/src/api/admissionApi.ts` only checked `import.meta.env.VITE_API_BASE_URL` (defaulting to `http://localhost:5159`). When Phase 5 specified `docker-compose.yml` with `environment: VITE_BACKEND_URL=http://localhost:8080`, two issues surfaced: (a) the variable name was `VITE_BACKEND_URL`, not `VITE_API_BASE_URL`, and (b) Vite bakes `import.meta.env.*` only at `npm run build` time—runtime `environment:` entries on an `nginx:alpine` container do not reach the already-built static JS bundle.
- **How it was caught & fixed**: Updated `admissionApi.ts` to check `window.__APP_CONFIG__` and `VITE_BACKEND_URL` (with fallback to `VITE_API_BASE_URL` and `http://localhost:5159`), passed `VITE_BACKEND_URL` as a Docker `build.args` in `docker-compose.yml`, and added `/docker-entrypoint.d/40-runtime-env.sh` in `frontend/Dockerfile` so runtime `environment:` overrides also populate `/env-config.js` at container startup.

### 3. `.NET 8` Package Version Alignment & UTC Timestamp Consistency
- **Mistake**: Unpinned `dotnet add package` initially pulled EF Core `10.0.x` packages incompatible with `net8.0`, and `8.0.33` of `Microsoft.EntityFrameworkCore.Design` alongside `8.0.11` of `Npgsql.EntityFrameworkCore.PostgreSQL` triggered `MSB3277` version-conflict warnings. Additionally, timestamps read from non-Npgsql test providers can lose `DateTimeKind.Utc` and serialize without a trailing `Z`.
- **How it was caught & fixed**: Pinned both `Npgsql.EntityFrameworkCore.PostgreSQL` and `Microsoft.EntityFrameworkCore.Design` (plus test packages) cleanly to `8.0.11` (`0` build warnings), enforced `DateTime.UtcNow` on entity creation/expiration, and normalized all outgoing timestamps with `AdmissionHelpers.EnsureUtc` before ISO-8601 JSON serialization.

### 4. React `oxlint` `set-state-in-effect` Warning & Test Cleanup
- **Mistake**: Synchronous `setState` inside `useEffect` during initial polling setup triggered a React Compiler `oxlint` warning (`react(set-state-in-effect)`), and missing automatic DOM cleanup in Vitest caused duplicate button queries across sequential tests.
- **How it was caught & fixed**: Moved `isLoadingHolds` synchronization into `handleStudentIdChange`, derived `visibleHolds` during render, detached the initial fetch in `useEffect` via `Promise.resolve().then(...)`, and added `afterEach(cleanup)` in `frontend/src/test/setup.ts`, resulting in `0` lint warnings and `4/4` passing Vitest tests.

