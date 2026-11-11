# Architectural & Business Assumptions

## 1. Hold Expiration & Lifecycle

- **UTC Timestamps Everywhere**: All `CreatedAt` and `ExpiresAt` timestamps are generated using `DateTime.UtcNow` and stored in PostgreSQL `timestamp with time zone` (`timestamptz`) columns. API responses normalize `DateTimeKind.Utc` so JSON payloads always serialize as ISO-8601 UTC (`...Z`).
- **Lazy Expiration Evaluation (No Sweeper Required)**:
  - When `POST /classes/{classId}/holds` succeeds, `ExpiresAt` is set to `UtcNow.AddSeconds(HOLD_DURATION_SECONDS)` (default `600` seconds) with `Status = "HELD"`.
  - A hold is considered **active** only when `Status == "HELD"` and `ExpiresAt > DateTime.UtcNow`.
  - Rather than relying on a background cron/cleanup job to mutate expired rows, seat availability queries (`GET /classes`, `GET /classes/{classId}`, and `POST /classes/{classId}/holds`) filter strictly on `ExpiresAt > DateTime.UtcNow`, and read endpoints (`GET /holds/{holdId}`, `GET /me/holds`) compute effective status via `GetEffectiveStatus`, returning `"EXPIRED"` immediately once `ExpiresAt <= DateTime.UtcNow`.
  - Consequently, an expired seat becomes available the exact millisecond `ExpiresAt` passes, with zero background-job lag.
- **Re-applying After Expiry or Cancellation**: A student with a prior `EXPIRED` (or expired `HELD`) or `CANCELLED` hold in a class is allowed to apply for a new hold in that same class if seats are available. Only an existing `CONFIRMED` or non-expired `HELD` hold in the same class triggers `409 Conflict`.
- **Idempotent Confirm & Cancellation Semantics**:
  - `POST /holds/{holdId}/confirm` is idempotent: calling it again on an already `CONFIRMED` hold returns `200 OK` `{ "holdId", "status": "CONFIRMED" }` without consuming another seat.
  - `POST /holds/{holdId}/confirm` rejects `EXPIRED` (including `HELD` with `ExpiresAt <= UtcNow`) and `CANCELLED` holds with `409 Conflict`.
  - `DELETE /holds/{holdId}` marks an owned hold as `CANCELLED` and returns `204 No Content`, immediately freeing the seat for other students.
- **Client-Side Countdown**: The React UI updates a local clock every 500 ms so when a `HELD` countdown reaches `00:00`, the badge immediately switches to `EXPIRED`, the `ভর্তি ফি দিন` (Pay Fee) button disables without reloading, and the UI refreshes class counts.

---

## 2. Student Validation & Authentication Assumptions

- **Header-Based Identity (`X-Student-Id`)**: Per the specification, there is no separate user login/password system; student identity is passed in the `X-Student-Id` HTTP header and persisted in browser `localStorage` (`school_admission_student_id`).
- **Strict 17-Digit Numeric Validation**: `AdmissionHelpers.RequireValidStudentId` enforces `^[0-9]{17}$` (ASCII digits only) on `POST /classes/{classId}/holds`, `POST /holds/{holdId}/confirm`, `DELETE /holds/{holdId}`, and `GET /me/holds`. Missing, multi-valued, non-numeric, or wrong-length values return `400 Bad Request`.
- **Ownership Enforcement**: `POST /holds/{holdId}/confirm` and `DELETE /holds/{holdId}` return `403 Forbidden` when the valid `X-Student-Id` does not match `HoldEntity.StudentId`, and `404 Not Found` if the hold ID does not exist.
- **Class Creation Inputs**: `POST /classes` assumes an admin action requiring a non-whitespace `name` (up to 200 characters) and an integer `totalSeats >= 1`. Invalid payloads or malformed route IDs return `400 Bad Request` via model validation or `GlobalExceptionMiddleware`, never raw `500`.

---

## 3. Concurrency Safety Across Multiple Backend Processes (Bonus §2.6)

- **PostgreSQL Row-Level Locking (`SELECT ... FOR UPDATE`)**:
  - Inside `POST /classes/{classId}/holds`, `ClassesController.CreateHold` calls `AppDbContext.AcquireClassForUpdateAsync`, which opens a PostgreSQL database transaction (`BeginTransactionAsync`) and executes:
    ```sql
    SELECT * FROM "Classes" WHERE "Id" = @classId FOR UPDATE
    ```
  - In PostgreSQL (under default `READ COMMITTED` isolation), `FOR UPDATE` acquires a row-level exclusive lock on that specific class row inside the database engine itself.
  - **Why this works across two (or N) independent backend processes/containers**:
    1. Suppose Backend Instance A and Backend Instance B receive simultaneous `POST /classes/{classId}/holds` requests for the last remaining seat.
    2. Both instances open transactions against the shared PostgreSQL database. Whichever reaches `SELECT ... FOR UPDATE` first (say Instance A) acquires the row lock on `Classes."Id" = @classId`.
    3. Instance B's `SELECT ... FOR UPDATE` **blocks inside PostgreSQL** until Instance A either commits or rolls back its transaction.
    4. Instance A checks duplicate holds and `(activeHeld + confirmed) < totalSeats`, inserts the new `HoldEntity`, and commits—releasing the row lock.
    5. Under PostgreSQL `READ COMMITTED`, Instance B wakes up and executes its hold-count query using a **fresh statement snapshot** that includes Instance A's newly committed `HoldEntity`. Instance B sees `(activeHeld + confirmed) == totalSeats` and returns `409 Conflict` instead of overselling.
    6. Locking by `ClassId` serializes contention per class while allowing holds for *different* classes to proceed in parallel without blocking each other.
  - Similarly, `POST /holds/{holdId}/confirm` and `DELETE /holds/{holdId}` acquire `SELECT * FROM "Holds" WHERE "Id" = @holdId FOR UPDATE` inside a transaction to prevent concurrent confirm/cancel races on the same hold.
- **Test-Only In-Memory Fallback**: In automated xUnit runs using EF Core's `InMemory` provider (`!dbContext.Database.IsNpgsql()`), transactions and `FOR UPDATE` are unsupported by the provider, so `InProcessLockRegistry` provides a per-key `SemaphoreSlim` solely for non-PostgreSQL test execution. Production PostgreSQL runs bypass `InProcessLockRegistry` completely and rely exclusively on PostgreSQL row-level locking.

