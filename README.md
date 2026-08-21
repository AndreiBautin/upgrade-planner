# Upgrade Planner

A planning tool for deciding what to buy next. You list the things you want — a
desk, a monitor arm, winter tyres — give each a priority and a cost, and say
which ones depend on others. It answers one question: *given the money I have
right now, what should I actually buy?*

The answer is not "sort by priority", and that is the point.

### ▶ Live demo — **https://andreibautin.github.io/upgrade-planner/**

**No login.** There is no sign-up and no account — the app has no authentication
at all, so the link opens straight into a working app. The data is generated
fixture data, and it resets itself; nothing there is real.

> The API runs on a free instance that sleeps after 15 minutes idle. The first
> request can take up to a minute to wake it, and the app says so on screen
> rather than showing you a dead spinner.

## The one idea worth 30 seconds

Priority propagates **up** the prerequisite chain. An upgrade inherits the
priority of the most important thing it unblocks.

```
Bigger desk        priority 70   ← recommended first
    unblocks ↓
Third monitor      priority 75
    unblocks ↓
Monitor arm        priority 92
```

So the desk — priority 70 — is recommended **above** an ergonomic chair at
priority 85, because buying the desk is the first available step toward the
priority-92 monitor arm. Anything still blocked is shown as blocked, with the
reason.

That inversion is the whole product. Everything else is CRUD.

## Features

- **Recommendation dashboard** ranking what to buy next by inherited priority,
  with a budget filter that marks what you can afford today
- **Prerequisite chains** with cycle detection and a delete-guard, both enforced
  in the database as well as in code
- **Drag to reorder** priority, computed as a midpoint so one drag is one write
- Search, filter by status and category, and five sort orders
- Estimated vs. actual cost tracking on purchases
- Seven categories, five statuses, full CRUD

## Architecture

```
React 19 + TypeScript (Vite)          GitHub Pages
        │  fetch, JSON
        ▼
ASP.NET Core 9 Web API                Render (Docker, free tier)
   Controller   HTTP only
   Service      every business rule
   Engine       pure function: priority propagation
        ▼
SQLite via EF Core, migrated at startup
```

The insight that makes it click: **the interesting logic is a pure function.**
`RecommendationEngine.Compute(List<Upgrade>) → Dictionary<int, Result>` touches
no database, no clock, and no HTTP — so the algorithm the whole app rests on is
tested by handing it a list.

Full write-up, including a request traced end to end through real files:
**[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**

## Tech stack, and why

| Choice | Why this one |
| --- | --- |
| **ASP.NET Core 9** | The domain is server-side rules over a small relational graph. Strong typing and first-class model validation at the boundary are the job. |
| **EF Core + SQLite** | One user, one file, zero operational surface. Migrations run at startup, so there is no deploy step to forget. Swapping to Postgres is one line — `UseSqlite` is the only place the provider is named. |
| **React 19 + TypeScript** | The UI is stateful in a way server-rendered forms handle badly: drag-to-reorder, optimistic updates, live filtering. |
| **Vite** | Sub-second builds and a dev proxy that makes the API same-origin locally, which keeps CORS out of development entirely. |
| **No state library** | Four pages, and server state is fetched per view. Redux or TanStack Query here would be ceremony over three `useState` calls. |
| **No repository layer** | `DbContext` already is a unit of work over repositories. Wrapping it would add a forwarding layer whose only payoff — a test double — is unnecessary, because the tests run on real in-memory SQLite. |
| **xUnit + Vitest** | Default in each ecosystem. Nothing exotic to explain. |

## Security

No authentication, by design — it began as a single-user tool. That removes CSRF
and authorization bugs *structurally* (no cookies, no sessions, no ownership) and
creates exactly one real risk: the public demo accepts writes from anyone. That
is bounded by rate limiting, a request-size cap, validation at the boundary, and
a container that reseeds itself on every cold start.

Fixed during productionization: out-of-range enums and 2 MB notes were being
accepted and stored; `UseAuthorization()` was in the pipeline with no
authentication behind it and was removed rather than dressed up; two
high-severity dependency advisories were patched. Full history was scanned for
secrets — none, ever.

**[docs/SECURITY.md](docs/SECURITY.md)** — threat model, every finding, and the
risks that remain.

## Testing

**175 tests** (134 xUnit, 41 Vitest), up from zero. Against real in-memory
SQLite, not a fake provider that ignores the constraints worth testing.

The personal-data scans on the demo fixture were verified by injecting an email,
a phone number, and a real host — three tests failed, one per category.

**[docs/TESTING.md](docs/TESTING.md)** — including what is deliberately *not*
tested, and why.

## Deployment

GitHub Pages for the SPA, Render for the API. Both genuinely free, neither
requiring a credit card. Pages was chosen because it adds **no new account and no
new secret** — it deploys with the workflow's built-in token.

CI on every push: build, test, lint, typecheck, both dependency audits gated at
high, `gitleaks` over full history, **both shipping build configurations**, and a
smoke test that fetches the live URL and checks the response body.

**[docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)** — the alternatives that were
rejected and on what grounds, free-tier headroom, and a troubleshooting table.

## Running it locally

**Prerequisites:** [.NET 9 SDK](https://dotnet.microsoft.com/download) and
[Node.js 20+](https://nodejs.org).

On Windows, `start.bat` launches both halves and opens a browser. Otherwise:

```bash
# API — http://localhost:5131
cd server/UpgradePlanner.Api
dotnet run --launch-profile http
```

```bash
# Client — http://localhost:5176
cd client
npm install
npm run dev
```

The dev server proxies `/api` to the API, so the app is same-origin and needs no
configuration to start. The database file is created and migrated on first run.

**To run it with demo data instead of your own:**

```bash
cd server/UpgradePlanner.Api
DEMO_MODE=true dotnet run --launch-profile http
```

Demo mode uses its own database file (`demo.db`) and ignores the configured
connection string entirely, so it cannot touch your real data. Seeding only ever
fills an empty database; it never overwrites.

**Configuration:** every environment variable is documented in
[`.env.example`](.env.example). There are no secrets to set — the app has none.

**Tests:**

```bash
dotnet test UpgradePlanner.sln
```

```bash
cd client && npm test
```

## Documentation

| | |
| --- | --- |
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Layers, a request traced end to end, the schema, and where this design stops working |
| [SECURITY.md](docs/SECURITY.md) | Threat model, findings and fixes, remaining risks |
| [TESTING.md](docs/TESTING.md) | Strategy, priorities, and what is deliberately untested |
| [DEPLOYMENT.md](docs/DEPLOYMENT.md) | Hosting choices, environment, troubleshooting, limits |
| [DEMO_DATA.md](docs/DEMO_DATA.md) | How real data is structurally kept out of the demo |
| [INTERVIEW_GUIDE.md](docs/INTERVIEW_GUIDE.md) | Decisions, trade-offs, and the awkward questions |
| [PRODUCTIONIZATION_ASSESSMENT.md](docs/PRODUCTIONIZATION_ASSESSMENT.md) | The audit this work came from, findings and all |
