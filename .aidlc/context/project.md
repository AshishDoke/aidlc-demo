# Project context: Team Leave Tracker

Every AIDLC stage reads this file. Keep it short and authoritative.

## Product
A small internal web app. Employees submit leave requests. Managers approve or reject them.

## How to read requirements
Requirements in `requirements/` are written by business people in plain language. They describe
*who*, *what* and *why*, never *how*. Map them onto this file:
- "the system knows / keeps / holds X" → persisted data (see Database).
- "people can do X" / "someone can see X" → API behaviour, plus a screen when the requirement says so.
- "a page" / "a screen" → a static page in `wwwroot/`.
- "a clear message saying why" → an API error (see API conventions) shown as text in the UI.
- "pick who they are" → the `X-Employee-Id` identity mechanism below.
- "ready-made team / demo data" → idempotent seed data.
Anything a requirement doesn't say is covered by the **Engineering defaults** below, and does not
need to be in the requirement.

## Tech stack (fixed, do not change without a requirement)
| Layer | Choice |
|---|---|
| Backend | .NET 8, ASP.NET Core **minimal API** (C#) |
| Database | SQLite via EF Core 8 (`Microsoft.EntityFrameworkCore.Sqlite`), file `leave.db` |
| Frontend | Plain HTML + CSS + vanilla JavaScript (ES modules), served from the API's `wwwroot/`. No npm, no build step. |
| Tests | xUnit + `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory) + EF Core SQLite in-memory |

## Folder layout
```
app/
  LeaveTracker.sln
  src/LeaveTracker.Api/
    Program.cs              # composition root + endpoint mapping
    Data/                   # DbContext, entities, seed
    Endpoints/              # one static class per resource: MapXxxEndpoints()
    Contracts/              # request/response records (DTOs)
    Services/               # business rules
    wwwroot/                # index.html, manager.html, css/, js/
  tests/LeaveTracker.Tests/
```

## Engineering defaults (apply to every requirement, even when not mentioned)
- If the solution does not exist yet, create it with the layout above (API project + test project).
- The app exposes `GET /api/health` returning 200 so we know it is running.
- The database is created automatically on first start. No manual setup steps.
- Every acceptance criterion is covered by at least one automated test. Behaviour exposed over HTTP
  is tested through the API (WebApplicationFactory).
- Seed/demo data is idempotent: restarting never duplicates it.
- Business rules live in `Services/`, and the API enforces them. The UI may also check them for convenience,
  but never instead of the API.
- Every error message tells the user what was wrong, in plain words.
- Accessibility: every input has a label, everything works with the keyboard, and status is shown as text, not colour alone.

## Conventions
- C#: nullable enabled, file-scoped namespaces, records for DTOs, `async` all the way, no static mutable state.
- API: JSON, camelCase, routes under `/api/...`. Validation errors → `400` with `ValidationProblem`.
  Not found → `404`. Not allowed for this caller → `403`. Conflicts (for example a state change not allowed) → `409`.
- Database: create the schema with `Database.EnsureCreated()` at startup (demo simplicity, no migrations).
- Dates: `DateOnly` for leave dates. Store as ISO `yyyy-MM-dd`.
- Identity: **no authentication in the demo.** The current user is picked from a dropdown and sent
  as the `X-Employee-Id` header. Never trust it for security. This is documented as a known demo limitation.
- Frontend: no frameworks. `fetch()` against `/api`.
- Tests run with `dotnet test app/LeaveTracker.sln` and must not need a running server.

## Out of bounds for the AI
- No new NuGet packages beyond: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.InMemory` (tests only, if needed),
  `Microsoft.AspNetCore.Mvc.Testing`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`.
  Anything else must be justified in the design and approved at G2.
- No external network calls, telemetry, or CDN scripts in the frontend.
