# Architecture

A React SPA and an ASP.NET Core API over SQLite. One process, one file, no
external services. The interesting part is not the shape — it is what the shape
deliberately leaves out.

## The picture

```
                         BROWSER
  ┌──────────────────────────────────────────────────────────┐
  │  React 19 SPA (client/)                                  │
  │                                                          │
  │   pages/          Dashboard, AllUpgrades, AddUpgrade,    │
  │                   UpgradeDetails                         │
  │   components/     Layout, StatusPill, CategoryTag,       │
  │                   ErrorBoundary                          │
  │   reorder.ts      drag-to-reorder → priority             │
  │   format.ts       money and dates                        │
  │   config.ts       env → AppConfig (pure, total)          │
  │   api.ts          the ONLY module that calls fetch()     │
  └───────────────────────────┬──────────────────────────────┘
                              │  JSON over HTTPS
                              │  dev: Vite proxy :5176 → :5131 (same origin)
                              │  prod: GitHub Pages → Render (cross origin, CORS)
  ┌───────────────────────────▼──────────────────────────────┐
  │  ASP.NET Core 9 API (server/UpgradePlanner.Api/)         │
  │                                                          │
  │   Program.cs            composition root — every          │
  │                         dependency is constructed here    │
  │        │                                                  │
  │        ├── rate limiter (120 req/min per client)          │
  │        ├── CORS policy (from AppOptions)                  │
  │        └── exception handler → ProblemDetails             │
  │        │                                                  │
  │   Controllers/         HTTP only: routes, status codes,   │
  │   UpgradesController   model validation. No queries.      │
  │        │                                                  │
  │   Services/            ALL business rules:                │
  │   UpgradeService       cycles, delete-guard, prerequisite │
  │        │               existence, DTO projection          │
  │        │                                                  │
  │   Services/            pure function, no I/O:             │
  │   RecommendationEngine priority propagation               │
  │        │                                                  │
  │   Data/AppDbContext    EF Core; timestamps and the        │
  │        │               priority CHECK constraint          │
  └────────┼─────────────────────────────────────────────────┘
           ▼
      SQLite file — upgradeplanner.db, or demo.db in demo mode
      (migrations applied at startup)
```

## What each layer is responsible for

| Layer | Owns | Explicitly does not own |
| --- | --- | --- |
| `config.ts` / `AppOptions` | Turning environment variables into a validated, immutable settings object | Anything conditional on those settings |
| `api.ts` | Every network call, the request timeout, the slow-request signal, turning a failure response into a sentence | What to do with the data |
| Pages / components | Rendering and local form state | Business rules — they ask the API |
| `UpgradesController` | Routing, status codes, `[ApiController]` model validation | Queries, and any decision about whether something is allowed |
| `UpgradeService` | Every business rule, and mapping entities to DTOs | HTTP; it returns a `ServiceResult`, never an `ActionResult` |
| `RecommendationEngine` | Priority propagation and blocked-ness | I/O of any kind — no database, no clock |
| `AppDbContext` | Persistence, timestamps, schema constraints | Business rules |

## How a request flows, end to end

**Scenario: a visitor opens the dashboard.** Real files, in order.

1. **`client/src/pages/Dashboard.tsx:15`** — on mount, `load()` calls
   `getRecommendations()`.

2. **`client/src/api.ts:114`** — `getRecommendations` builds the URL from
   `BASE`, which came from `config.apiBaseUrl` (empty locally, so the Vite proxy
   makes it same-origin; the Render URL in production).

3. **`client/src/api.ts:84`** — `request()` starts a 2.5-second timer. If the
   response has not arrived by then it publishes a "slow" event, and
   `Layout.tsx`'s `ColdStartNotice` explains that the free instance is waking up.
   It also attaches a 90-second `AbortSignal.timeout`.

4. **`server/.../Program.cs`** — the request enters the pipeline: exception
   handler, then CORS (`Origin` checked against `AppOptions.AllowedOrigins`),
   then the rate limiter (a fixed 120-per-minute window keyed on the forwarded
   client address), then routing.

5. **`server/.../Controllers/UpgradesController.cs:35`** —
   `GetRecommendations` does one thing: `await _upgrades.GetRecommendationsAsync(ct)`.
   No query, no rule, no branch.

6. **`server/.../Services/UpgradeService.cs:52`** — loads every upgrade with
   `AsNoTracking()`, then calls `Project`.

7. **`server/.../Services/RecommendationEngine.cs:9`** — `Compute` builds a
   child index, then walks each node's descendants memoising the highest priority
   found. This is where a desk at priority 70 acquires an *effective* priority of
   92 because it unblocks a monitor arm. It also marks an upgrade `IsBlocked`
   when its prerequisite is anything other than `Purchased`.

8. **`UpgradeService.cs:217`** — `ToDto` merges the stored row with the computed
   result, resolving `PrerequisiteTitle` from an in-memory dictionary rather than
   a second query.

9. **`UpgradeService.cs:56`** — Purchased and Cancelled rows are dropped (neither
   is something money can still be spent on), and the rest are ordered by
   effective priority, then raw priority as a tiebreak.

10. **Back in the browser** — `handle()` parses the JSON,
    `Dashboard.tsx` picks the first unblocked row as the hero card, and the
    remainder render as a ranked list.

**The one thing worth noticing:** step 7 is why the app exists, and it is a pure
function. It can be tested by handing it a list — which is exactly what
`RecommendationEngineTests` does, thirteen times, with no database.

## Where the data lives

One table.

```
Upgrades
  Id                     INTEGER  PK, autoincrement
  Title                  TEXT     not null
  Description            TEXT     null
  Category               INTEGER  enum, validated at the API boundary
  Priority               INTEGER  CHECK (Priority BETWEEN 1 AND 100)
  EstimatedCost          decimal(10,2)  null
  Status                 INTEGER  enum, validated at the API boundary
  Notes                  TEXT     null
  ProductLink            TEXT     null
  PrerequisiteUpgradeId  INTEGER  null → Upgrades.Id, ON DELETE RESTRICT
  PurchasedDate          TEXT     null
  ActualCost             decimal(10,2)  null
  CreatedAt / UpdatedAt  TEXT     not null, set by AppDbContext

  INDEX IX_Upgrades_PrerequisiteUpgradeId
```

The self-referencing foreign key is the whole data model. An upgrade points at
the one thing that must happen first, so the prerequisite graph is a forest, and
`RecommendationEngine` is a walk over it.

Two invariants are enforced by the **database**, not only by C#:

- `ON DELETE RESTRICT` — a prerequisite cannot be deleted out from under its
  dependents. `UpgradeService.DeleteAsync` checks for dependents first, but only
  so the user gets a sentence instead of a constraint violation.
- `CK_Upgrade_Priority` — priority stays in 1–100 even if a future code path
  forgets to check.

## How dependencies flow

Everything is constructed in `Program.cs` and nowhere else.

```
AppOptions      singleton  (parsed once, immutable)
AppDbContext    scoped     (connection string from AppOptions)
UpgradeService  scoped     (takes AppDbContext)
UpgradesController         (takes UpgradeService, via MVC)
```

`RecommendationEngine` is static because it holds no state and needs no
injection. `DemoSeeder` is static for the same reason and takes its `DbContext`
and its `now` as parameters, which is what makes both testable without a
container.

**There is no repository interface.** `DbContext` is already a unit of work over
a set of repositories; adding another one would produce a layer whose methods
forward to EF and whose only benefit — a test double — is unnecessary, because
the tests run against real SQLite in memory.

## Auth

There is none. No accounts, no sessions, no cookies, no tokens.

That is a deliberate product decision, not an omission: the app was built as a
single-user tool for one person's own planning, and the deployed copy is a demo
whose data is generated and disposable. What follows from having no auth — and
what does *not* follow — is worked through in [SECURITY.md](SECURITY.md).

## How errors are handled

| Where | What happens |
| --- | --- |
| Invalid request body | `[ApiController]` + annotations on `UpsertUpgradeDto` → 400 with an RFC 9457 `ProblemDetails` naming the fields |
| Broken business rule | `ServiceResult.Invalid(message)` → 400 with a plain sentence |
| Missing row | `ServiceResult.NotFound()` → 404 |
| Request over 64 KB | Kestrel's `BadHttpRequestException` → 413, mapped by status rather than reported as a 500 |
| Too many requests | Rate limiter → 429; the client turns this into "wait a moment and try again" |
| Anything unhandled | Exception handler → 500 `ProblemDetails`, logged with method and path; detail included only in Development |
| Network failure in the browser | `api.ts` distinguishes timeout, unreachable, and HTTP error, and says which |
| Render-time exception | `ErrorBoundary` shows a reload button instead of a blank page |

## How configuration and secrets are handled

Every setting that differs between development, production, and the demo is an
environment variable, documented in [`.env.example`](../.env.example).

Both parsers — `AppOptions.Parse` and `readConfig` — are **pure and total**. They
never throw, and every unrecognised value falls back to a documented default with
a warning. For every flag, `false` is the conservative direction, so a typo can
never turn on the mode that writes data. That property is tested from both sides
(`AppOptionsTests`, `config.test.ts`).

**There are no secrets.** No API keys, no tokens, no external service
credentials; the only connection string is a local file path. Deployment
inherits this: GitHub Pages authenticates with the workflow's built-in token, so
there is nothing to store, rotate, or leak. Git history was scanned in full and
contains no credential.

## Why this shape suits this app

- **One user, one small dataset.** Tens of rows. Every "scaling" decision that
  would matter at 10⁴ rows is irrelevant here and is named as such below rather
  than pre-solved.
- **The interesting logic is a pure function**, so the architecture's job is
  mostly to stay out of its way.
- **A single deployable artefact per half** keeps the free-tier deployment
  boring: a static bundle and one container.
- **SQLite's weakness is the demo's feature.** An ephemeral file means the demo
  regenerates on every cold start, so vandalism self-heals with no scheduled job
  and no reset endpoint to protect.

## Where this stops working

Stated plainly, because knowing the limit matters more than pretending there
isn't one.

- **`GetAll` loads the whole table** on every read, including for a single-row
  fetch. Effective priority is a property of the whole graph, so the derived
  fields genuinely cannot be computed from one row — but past a few thousand
  rows this needs either a recursive CTE or a cached `EffectivePriority` column
  invalidated on write.
- **SQLite serialises writes.** Fine for one user; wrong for concurrent ones.
  Postgres would be the swap, and `UseSqlite` in `Program.cs` is the only line
  that names the provider.
- **No pagination.** The client fetches everything and filters in memory.
- **Rate limiting is per-instance and in-memory.** With more than one replica the
  effective limit multiplies by the replica count.
- **The prerequisite model allows one parent per upgrade.** Real dependencies are
  a DAG, not a forest. Supporting several would mean a join table and turning
  `Compute` into a topological walk.
