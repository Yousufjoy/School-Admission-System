# School Admission System

A full-stack school admission seat-reservation system built with **.NET 8**, **PostgreSQL**, **React (Vite + TypeScript + Tailwind CSS)**, and **Docker Compose**.

Students enter their 17-digit birth certificate number (`জন্ম নিবন্ধন নম্বর`), apply for temporary seat holds (`HELD`), track a live `MM:SS` expiration countdown, and confirm admission (`CONFIRMED`) or cancel (`CANCELLED`) in real time without overselling seats.

---

## Project Structure

```text
.
├── backend/
│   ├── Dockerfile
│   ├── SchoolAdmission.sln
│   ├── SchoolAdmission.Api/      # .NET 8 Web API, EF Core Npgsql, controllers & migrations
│   └── SchoolAdmission.Tests/    # xUnit & WebApplicationFactory integration/concurrency tests
├── frontend/
│   ├── Dockerfile
│   ├── nginx.conf
│   ├── 40-runtime-env.sh         # Runtime VITE_BACKEND_URL injection for Nginx
│   └── src/                      # Vite + React + TypeScript + Tailwind CSS app
├── docker-compose.yml
├── README.md
├── ASSUMPTIONS.md
└── AI_USAGE.md
```

---

## Running with Docker (Recommended)

Ensure **Docker Desktop** (or Docker Engine + Compose v2) is running, then from the repository root run:

```bash
docker compose up --build
```

Once all services are healthy and running:

- **Frontend**: http://localhost:3000
- **Backend API**: http://localhost:8080

To stop the stack (keeping PostgreSQL data in the `pgdata` volume):

```bash
docker compose down
```

To reset the database volume as well:

```bash
docker compose down -v
```

---

## Configuration

| Variable | Service | Default | Description |
| --- | --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | `backend` | `Host=localhost;Port=5432;Database=admission_db;Username=postgres;Password=postgrespassword` | PostgreSQL connection string (`Host=db;...` in `docker-compose.yml`). |
| `HOLD_DURATION_SECONDS` | `backend` | `600` | Seat hold duration in seconds before expiration. |
| `VITE_BACKEND_URL` | `frontend` | `http://localhost:5159` (`http://localhost:8080` in Docker) | Backend API base URL consumed by the React app at build and container startup. |

---

## Running Automated Tests

### Backend Tests (.NET 8 / xUnit)

From the repository root:

```bash
dotnet test backend/SchoolAdmission.sln
```

> **Note (Windows PowerShell with user-local .NET 8 SDK):** If `dotnet` resolves only to a system runtime without SDKs, prepend `%LocalAppData%\Microsoft\dotnet` first:
> ```powershell
> $env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
> $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
> dotnet test backend\SchoolAdmission.sln
> ```

### Frontend Tests & Checks (Vitest / TypeScript / oxlint)

```bash
cd frontend
npm test
npm run lint
npm run build
```

---

## API Summary

- `POST /classes` — Create a class (`{ "name", "totalSeats" }`)
- `GET /classes` — List classes with `totalSeats`, `heldSeats`, `confirmedSeats`, and `availableSeats`
- `GET /classes/{classId}` — Get single class details and seat counts
- `POST /classes/{classId}/holds` — Create a timed seat hold (requires `X-Student-Id` matching `^\d{17}$`)
- `GET /holds/{holdId}` — Inspect hold status (`EXPIRED` when `HELD` and `ExpiresAt <= UtcNow`)
- `POST /holds/{holdId}/confirm` — Idempotently confirm an active hold (requires matching `X-Student-Id`)
- `DELETE /holds/{holdId}` — Cancel an owned hold and release its seat (requires matching `X-Student-Id`)
- `GET /me/holds` — List holds belonging to the `X-Student-Id` header

