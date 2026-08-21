# Interview guide

Written to be said out loud. Every claim here is true of the code in this
repository — check anything you are unsure of before you repeat it.

---

## The 30-second version

> "It's a planning tool for deciding what to buy next. You list things you want,
> give each a priority and a cost, and say which ones depend on others — you
> can't put up a third monitor before you've got a desk big enough for it.
>
> The interesting bit is that it doesn't just sort by priority. Priority
> propagates *up* the dependency chain, so something low-priority that unblocks
> something important gets promoted. A desk I rated 70 gets recommended above a
> chair I rated 85, because the desk is the first step toward a monitor arm I
> rated 92.
>
> It's a .NET 9 API over SQLite with a React front end. The recommendation
> algorithm is a pure function, which is why it's the easiest part to test."

Stop there. Let them ask.

## Explaining the architecture — lead with these three

1. **"The interesting logic is a pure function."** `RecommendationEngine.Compute`
   takes a list and returns a dictionary. No database, no clock, no HTTP. That is
   why there are thirteen tests on the core algorithm and none of them need a
   fixture.
2. **"The controller does HTTP and nothing else."** Routing, status codes, model
   validation. Every rule lives in `UpgradeService`. I moved them there during
   productionization for one reason: they were only reachable through an HTTP
   request, so they could not be tested.
3. **"I deliberately did not add a repository layer."** `DbContext` is already a
   unit of work over a set of repositories. Wrapping it would add a layer that
   forwards calls, and the only thing it buys is a test double I do not need,
   because the tests run against real SQLite in memory.

Point three is the one that lands, because most people expect the opposite answer
and it shows you chose rather than defaulted.

## The request lifecycle, end to end

Practise this with real file names — it is the single most convincing thing you
can do.

> "Say the dashboard loads.
>
> `Dashboard.tsx` calls `getRecommendations` in `api.ts` — the only module in the
> client that calls `fetch`. It starts a 2.5-second timer; if the response hasn't
> arrived by then it publishes a 'slow' event and the layout shows a cold-start
> notice, because the free tier sleeps and I'd rather tell the user than show
> them a dead spinner.
>
> Server side it goes through the exception handler, CORS, then a rate limiter —
> 120 a minute, keyed on client address — then routing.
>
> `UpgradesController.GetRecommendations` awaits `UpgradeService`. One line. No
> query, no branch.
>
> The service loads every upgrade `AsNoTracking` and hands the list to
> `RecommendationEngine.Compute`. That builds a child index, then walks each
> node's descendants memoising the highest priority found — that's where the
> desk picks up an effective priority of 92. It also marks anything blocked whose
> prerequisite isn't purchased yet.
>
> Then the service drops Purchased and Cancelled — neither is something money can
> still be spent on — orders by effective priority, and maps to DTOs, resolving
> prerequisite titles from an in-memory dictionary rather than a second query.
>
> Back in the browser the first unblocked row becomes the hero card."

**Why it loads every row for a single-item fetch** — have this ready, because a
good interviewer will ask:

> "Effective priority is a property of the whole graph, so I genuinely can't
> compute the derived fields for one upgrade without its descendants. At tens of
> rows it's one small query. Past a few thousand I'd need a recursive CTE, or a
> cached `EffectivePriority` column invalidated on write."

## Engineering decisions

Format: decision → alternatives → why → **the trade-off**. The trade-off is what
makes it credible.

### Business rules in a service, not the controller

- **Alternatives:** leave them in the controller; go full Clean Architecture with
  MediatR and handlers.
- **Why:** the rules were unreachable except through HTTP, which made them
  untestable. One class fixed that.
- **Trade-off:** one more indirection to read through for a trivial CRUD call.
  For an app this size that's a real cost, and I accepted it because it bought
  the test suite. I did *not* take the next step to CQRS — that would be adding
  ceremony to a single-entity app.

### Real SQLite in memory for tests, not the EF in-memory provider

- **Alternatives:** `Microsoft.EntityFrameworkCore.InMemory`; mock `DbContext`.
- **Why:** the in-memory provider silently ignores check constraints and foreign
  keys — and `CK_Upgrade_Priority` and `ON DELETE RESTRICT` are exactly the
  guarantees worth testing. A suite on the fake provider would pass while they
  were broken.
- **Trade-off:** tests are slower (a real connection and migrations per test) and
  are integration tests wearing unit-test clothes. Worth it — running migrations
  per test also re-proves the schema builds from empty, which is what happens on
  every production cold start.

### Seeding split into two named functions

- **Alternatives:** `Seed(bool overwrite)`; a config flag.
- **Why:** the previous code deleted every row whenever an env var was set. One
  typo away from destroying real data. Now `SeedIfEmpty` and `ResetToDemoData`
  are separate functions, so a call site can't ask for one and get the other —
  there's no argument to get wrong.
- **Trade-off:** two names instead of one. That's the whole cost.

### Demo mode hard-codes its own database file

- **Alternatives:** document "point the connection string at demo.db"; use a
  separate table or a tenant column.
- **Why:** documentation isn't a barrier. In demo mode the connection string
  isn't read from configuration at all, so no environment variable can aim a demo
  seed at the real database.
- **Trade-off:** you can't run the demo against a database of your choosing. That
  inflexibility is the feature.

### GitHub Pages + Render, not one platform

- **Alternatives:** Vercel for both; Azure; Fly.io.
- **Why:** Pages adds no new account and no new secret — it deploys with the
  workflow's built-in token. Render is the only container host I could verify is
  free with no card on file. Azure's free tier still wants a card at signup; I'd
  actually tried Azure earlier and reverted it.
- **Trade-off:** two providers, and Pages needs the repo public. Also, the free
  instance sleeps — the first visitor waits up to a minute. I surfaced that in
  the UI instead of pretending it doesn't happen.

### SQLite on an ephemeral disk for the demo

- **Alternatives:** Neon or Supabase Postgres; a persistent disk.
- **Why:** the demo's data is *supposed* to be disposable. The container loses
  its filesystem on restart, so the fixture regenerates on every cold start and
  vandalism heals itself — no scheduled job, and no reset endpoint that would
  itself need protecting.
- **Trade-off:** anything a visitor adds disappears. For a demo that's correct;
  for real data it would be a catastrophe, and I'd move to Postgres, which is one
  line — `UseSqlite` is the only place the provider is named.

### Rate limiting instead of authentication on the demo

- **Alternatives:** add auth; make the demo read-only.
- **Why:** a read-only demo of a CRUD planning tool demonstrates nothing. Auth
  would be building a feature the product doesn't have just to deploy it.
- **Trade-off:** anyone can vandalise the demo for up to 15 minutes. I bounded
  the rate, capped the body at 64 KB, validated every field — and accepted the
  rest, because the data is fake and the container is disposable.

## Security talking points — lead with the threat model

> "The first thing I did was write down what this actually is, because that
> determines which of the standard list even applies.
>
> There's no authentication — it started as a single-user tool. That removes
> whole categories *structurally*: no cookies and no sessions means there's no
> CSRF surface, because a forged cross-site request achieves exactly what a
> direct curl achieves. No accounts means no IDOR — there are no privileges to
> escalate. Everything goes through EF Core LINQ, so there's no SQL injection
> surface. No file handling, so no path traversal.
>
> That leaves one real risk: the public demo takes writes from anyone."

Then the concrete findings:

> "Testing the boundary, I found the API accepted `{"category": 99}` with a 201 —
> the JSON binder happily casts any integer to an enum — and the front end then
> rendered `undefined`. It accepted a two-million-character note, which on a host
> with an ephemeral disk is a disk-exhaustion vector. It accepted negative costs,
> and purchase dates on items that were never purchased.
>
> The one I'm most pleased about removing is `app.UseAuthorization()`. It was in
> the pipeline with no authentication scheme and no `[Authorize]` attribute
> anywhere — it couldn't deny a single request. It looked like an access control
> and wasn't one. I deleted it and documented the gap, because a line that
> provides false assurance is worse than no line."

If they push on CORS, do **not** claim it as a defence:

> "CORS is a browser control, not an access control. With no cookies it does
> nothing against curl. It's there so the deployed front end works. Calling it
> security would be exactly the theatre I was trying to remove."

## The database

- **One table**, `Upgrades`, with a self-referencing nullable FK
  `PrerequisiteUpgradeId`. That FK *is* the data model — the graph is a forest,
  and the engine is a walk over it.
- **Indexed** on `PrerequisiteUpgradeId`, which is the column every traversal
  filters on.
- **Two invariants in the database, not just in C#:** `ON DELETE RESTRICT` so a
  prerequisite can't be deleted out from under its dependents, and
  `CK_Upgrade_Priority` keeping priority in 1–100. The service checks for
  dependents first, but only to give the user a sentence instead of a constraint
  violation — the constraint is the real guarantee.
- **Migrations** are EF Core, applied at startup by `Database.Migrate()`. On the
  ephemeral demo host that means every cold start builds the schema fresh — and
  CI proves that path on every push.
- **Access** is EF Core LINQ directly from the service. No repository, no raw
  SQL, no stored procedures.

**What breaks at scale:** loading every row per request; SQLite serialising
writes; no pagination; and one parent per upgrade — real dependencies are a DAG,
which would need a join table and turning `Compute` into a topological walk.

## Deployment

> "The front end is a static bundle on GitHub Pages, deployed by Actions with the
> built-in token — no secret to manage. The API is a Docker container on Render's
> free tier.
>
> The bit that usually breaks a static deploy is the base path: a project page
> serves from `/repo-name/`, and if the bundler's base and the router's basename
> disagree you get working assets and a 404 on every route. Both derive from one
> value — Vite computes `BASE_URL` from `base`, and the router reads its basename
> from that.
>
> Static hosts have no rewrite rules, so the build copies `index.html` to
> `404.html` and Pages serves it for unmatched paths. I'll be honest that the
> status code stays 404 — the page renders correctly and a human sees the right
> screen, but a crawler sees a 404. A host with real rewrites returns 200. That's
> the price of not adding another account."

On CI:

> "It builds *both* shipping configurations, because the demo build differs from
> the default and a failure that only shows under the demo config shouldn't first
> surface at deploy time. And after deploying it fetches the live URL and checks
> the body — a green deploy step only proves an upload succeeded.
>
> CI and deploy run in parallel rather than gated. That's deliberate for a demo,
> and it's one line to change — I documented both the trade-off and the fix."

## Testing

> "It went from zero tests to 175. I didn't chase a coverage number, because
> chasing one produces tests written to raise it.
>
> I prioritised the algorithm, destructive operations tested from the
> 'must-not-destroy' side, and validation at the trust boundary — every
> validation test corresponds to an input I'd confirmed was accepted with a 201
> before the fix.
>
> The one I'd point at is `SeedIfEmpty_never_overwrites_existing_data`. It plants
> a row called 'My real upgrade', seeds, and asserts one row remains and it's
> that one. That test is what stands between a mis-set environment variable and
> my actual data.
>
> And I checked the fixture's personal-data scans weren't vacuous by injecting an
> email, a phone number, and a real hostname — three tests failed, one per
> category. A scanning test that scans nothing passes."

What's not tested, said without apology:

> "No component rendering tests — the components are thin, and the logic worth
> testing was extracted out of them. No `WebApplicationFactory` tests of the HTTP
> pipeline; I verified rate limiting and the body cap by hand against a running
> instance and left it to the CI smoke test, because restructuring the
> composition root to suit the test host cost more than it bought. And I don't
> test that EF saves things — that's Microsoft's code."

## Deliberate simplifications

Knowing where you *didn't* build something is a stronger signal than a longer
feature list.

| Not built | Why | What it would take |
| --- | --- | --- |
| Authentication | Single-user tool; the demo is meant to be open | Identity or an OIDC provider, ownership on every row, an authz check on every path |
| Multiple prerequisites | One parent covers real use; the forest keeps `Compute` simple | Join table; `Compute` becomes a topological walk |
| Pagination | Tens of rows | Cursor paging, and effective priority computed server-side per page |
| Soft delete / audit trail | Personal tool; nothing to reconstruct | `DeletedAt` and a filter on every query |
| Optimistic concurrency | One user, no concurrent edits | A rowversion column and 409 handling |
| Postgres | SQLite's impermanence is the demo's reset mechanism | One line, plus a hosted database and its first secret |
| Third-party error reporting | Would ship a visitor's session to a vendor for data a planning tool has no use for | Sentry SDK and a DSN |
| Component tests | Components are thin; the logic lives outside them | jsdom + Testing Library |
| CSP and security headers | Pages controls the static response headers; setting them in app code would be a header the platform ignores | A host that lets you set them, or a server in front |

## Likely questions

**"Isn't this over-engineered for a shopping list?"**
> "The app itself is deliberately not — one table, no repository layer, no CQRS,
> no state management library. What I did add is a service class, because the
> rules were untestable in the controller, and a test suite. If you're pointing
> at the documentation, that's for this conversation rather than for the code."

**"What's the weakest part?"**
> "The demo takes unauthenticated writes. I bounded it — rate limit, 64 KB body
> cap, validation, a container that reseeds itself — but within a fifteen-minute
> window somebody can fill it with junk and other visitors will see it. I chose
> that over a read-only demo, which would show nothing. Second weakest: the rate
> limiter partitions on `X-Forwarded-For`, which is spoofable. Doing it properly
> needs a known-proxy allowlist. I wrote that down rather than half-fixing it."

**"What would you do differently?"**
> "I'd have written the recommendation engine's tests first — it's a pure
> function, it was always the easiest thing to test, and I built the whole app
> before testing any of it. And I'd have designed the seeder as two functions
> from the start instead of discovering later that one env var could wipe my
> database."

**"Why .NET and React rather than one stack?"**
> "The rules are server-side over a relational graph — that's where .NET's typing
> and boundary validation earn their keep. The UI is genuinely stateful: drag to
> reorder, optimistic updates, live filtering. Server-rendered forms handle that
> badly. If it were mostly read-only I'd have used Razor Pages and skipped the
> SPA."

**"Why no repository pattern?"**
> "`DbContext` is already a unit of work over a set of repositories. The usual
> argument for wrapping it is testability, and I get that from real in-memory
> SQLite — which is *better*, because it enforces the check constraints and
> foreign keys a mock wouldn't."

**"How would you scale this?"**
> "The first thing that breaks is loading every row per request. That's not
> laziness — effective priority is a property of the whole graph — but past a few
> thousand rows it needs a recursive CTE or a cached column invalidated on write.
> Then SQLite's write serialisation, so Postgres. Then pagination. I'd want to see
> it hurt before doing any of it."

**"How do you know the demo has no real data in it?"**
> "Three barriers, all tested. The fixture is generated in source code — there's
> no export step from my machine anywhere in the pipeline. Demo mode uses its own
> database file and doesn't read the connection string at all, so no env var can
> point it at my data. And seeding only fills an empty database; it never
> overwrites. Plus tests that scan the fixture for emails, phone numbers,
> credentials, and non-example URLs — and I verified those scans actually fire."

**"Anything you'd flag as risky?"**
> "The seeder as it was. It deleted every row whenever a config string equalled
> 'true'. It was a single mistyped environment variable from destroying my real
> planning data, and I'd been running it that way. That's the finding I'd lead
> with if you asked what the audit turned up."

## Things not to say

| Don't say | Because |
| --- | --- |
| "It's production-ready" | It's a deployed demo with no auth. Say what's deployed, tested, and documented — and what isn't. |
| "It's secure" / "CORS secures the API" | CORS is a browser control. Claiming it as a defence invites the follow-up you can't answer. |
| "It's fully tested" / quoting a coverage number | 175 tests, and you can name exactly what isn't covered. That's stronger. |
| "It scales" | It loads every row on every request. You know where it breaks — say that instead. |
| "I used Clean Architecture / DDD" | You used a controller, a service, and a DbContext. Describing it accurately is more impressive than borrowing a label. |
| "The demo resets every hour" | It resets on cold start, after 15 minutes idle. Don't invent a schedule that doesn't exist. |
| "There's a health check dashboard" | There's a `/health` endpoint returning JSON. Nothing more. |
| Anything about caching, queues, or microservices | None exist here. |
