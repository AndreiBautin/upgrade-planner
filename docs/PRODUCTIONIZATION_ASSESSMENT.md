# Productionization Assessment

Assessed 2026-08-20 against commit `e602724`. Every number below was produced by
running the command, not by reading configuration.

## 1. What this application is

Upgrade Planner is a personal decision tool. You record things you want to buy
(a desk, a monitor arm, a NAS), give each a priority 1–100 and an estimated
cost, and optionally declare that one upgrade is a **prerequisite** of another.
The app then answers one question: *given the money I have right now, what
should I actually buy next?*

The non-obvious part — and the reason the app is worth showing anyone — is that
the answer is not "sort by priority". Priority propagates **up the prerequisite
chain**: a low-priority item that unblocks a high-priority item inherits that
higher priority. Verified live against a seeded database:

```
eff= 92 pri= 92 blocked=true  Monitor Arm            requires=Third Monitor
eff= 92 pri= 75 blocked=true  Third Monitor          requires=Bigger Desk
eff= 92 pri= 70 blocked=false Bigger Desk            unlocks=Monitor Arm
eff= 85 pri= 85 blocked=false Upgrade Office Chair
```

"Bigger Desk" is only priority 70, but it ranks above the priority-85 office
chair because it is the first unblocked step toward the priority-92 monitor arm.
That is the product.

## 2. Current architecture

```
client/  React 19 + TypeScript + Vite 8 SPA          (1,420 lines)
   |  fetch() in src/api.ts  ->  /api/upgrades
   |  dev: Vite proxy :5176 -> :5131
   v
server/UpgradePlanner.Api/  ASP.NET Core 9 Web API    (606 lines, 10 files)
   Controllers/UpgradesController.cs   HTTP + business rules + DTO mapping
   Services/RecommendationEngine.cs    pure priority propagation
   Data/AppDbContext.cs                EF Core, timestamps, check constraint
   Data/DemoSeeder.cs                  destructive reseed
   Models/ Dtos/ Migrations/
   v
SQLite file (upgradeplanner.db), EF Core migrations applied at startup
```

There is no authentication, no authorization, no background work, no caching,
and no external service dependency. The whole app is one process plus a file.

## 3. Baseline — measured, not assumed

| Check | Command | Result |
| --- | --- | --- |
| Server build | `dotnet build -c Release` | **Succeeded, 0 warnings, 0 errors** (5.95s) |
| Client build | `npm run build` | **Succeeded**, 34 modules, 247.31 kB (77.64 kB gzip) |
| Client lint | `npm run lint` (oxlint) | **Clean** |
| Tests | — | **Zero. No test project, no test runner, nothing.** |
| npm audit | `npm audit --audit-level=high` | **1 high** (nanoid `<3.3.18`) |
| NuGet audit | `dotnet list package --vulnerable --include-transitive` | **1 high** (SQLitePCLRaw.lib.e_sqlite3 2.1.10) |
| Secrets in history | `git log --all -p` grep | **None found.** Only GitHub secret *names* in a deleted workflow |
| Personal DB committed | `git ls-files` | **No.** `upgradeplanner.db` is ignored by `.gitignore:4` and untracked |

## 4. Honest strengths

These are real and should not be "improved" away.

1. **`RecommendationEngine` is already a pure function.**
   `Compute(List<Upgrade>) -> Dictionary<int, Result>` touches no database, no
   clock, no HTTP. It is the most valuable code in the repository and it is
   already in the shape that makes it trivially testable. It even carries a
   deliberate cycle guard with a comment explaining why.
2. **Timestamps are enforced at the persistence boundary**, in
   `AppDbContext.SaveChanges`, not sprinkled through call sites. `CreatedAt`
   cannot be forgotten.
3. **Referential integrity is enforced in the database**, not just in C#:
   `OnDelete(DeleteBehavior.Restrict)` plus a `CK_Upgrade_Priority` check
   constraint. The delete-guard in the controller is a nicer error message on top
   of a real constraint, not a substitute for one.
4. **Cycle detection exists and works.** Setting a prerequisite that would close
   a loop is rejected. Most apps this size do not bother.
5. **DTOs are separate from entities.** `UpsertUpgradeDto` cannot set `Id`,
   `CreatedAt`, or the derived recommendation fields. There is no
   mass-assignment hole.
6. **The demo seeder already uses relative dates** (`now.AddDays(-18)`), so the
   fixture does not rot.
7. The client is small, dependency-light (3 runtime dependencies), and has no
   state-management library it does not need.

The architecture is *appropriate for its size*. It does not need Clean
Architecture, MediatR, a repository layer, or a DI container beyond what ASP.NET
Core ships with. Nothing below recommends imposing one.

## 5. Weaknesses that matter

| # | Finding | Impact | Severity |
| --- | --- | --- | --- |
| W1 | **`DemoSeeder.Reseed` unconditionally deletes every row**, gated only on the string `SeedDemoData == "true"`. One stray environment variable on the author's machine destroys the real database. There is no "seed only if empty" path at all. | Total, silent, unrecoverable personal data loss | **Critical** |
| W2 | **No tests of any kind.** The priority-propagation algorithm, the cycle detector, and the delete-guard — the three pieces of genuine logic — are entirely unverified. | Any refactor is a guess | **High** |
| W3 | **Out-of-range enum values are accepted and stored.** `POST {"category":99}` returns `201`. Confirmed live. The client then renders `CATEGORY_LABELS[99]` → `undefined` (empty tag) and `class="pill undefined"`. | Corrupt data, broken UI, no error | **High** |
| W4 | **Unbounded string fields.** A 2,000,000-character `notes` value was accepted and stored. Confirmed live. | Disk exhaustion on an ephemeral free tier | **High** |
| W5 | **Business logic lives in the controller.** Cycle detection, the delete-guard, and prerequisite validation are inside `UpgradesController`, reachable only through HTTP. This is what makes W2 hard to fix. | Blocks testing | **Medium** |
| W6 | **Negative money is accepted.** `estimatedCost: -500` returns `201`. Confirmed live. | Nonsense totals | **Medium** |
| W7 | **`PurchasedDate` and `ActualCost` can be set on an item whose status is `Idea`.** Confirmed live. | Incoherent records | **Medium** |
| W8 | **No error boundary in the client.** A single render throw blanks the entire page to white. | Worst-possible failure mode in a portfolio demo | **Medium** |
| W9 | **No root `README.md`.** The only README is the unmodified Vite template boilerplate in `client/`, which describes React and Oxlint and says nothing about this app. | This is the first thing an employer sees | **Medium** |
| W10 | **`<title>client</title>`** and no root-level app identity. | Reads as unfinished | **Low** |
| W11 | **`UpgradePlanner.sln` registers no projects.** Opening the solution in Visual Studio shows an empty solution. | Broken for the stated IDE | **Low** |
| W12 | **No health endpoint.** A PaaS host has nothing to probe and no way to distinguish "cold-starting" from "dead". | Undiagnosable outages | **Low** |
| W13 | **No CI.** Nothing runs on push. | Regressions ship | **Medium** |
| W14 | `GetById` loads the entire table to answer a single-row query (it needs the full set for propagation, but not for the 404 check). | Irrelevant at this scale; would matter at 10⁴ rows | **Low** |
| W15 | **`AppDbContext.TouchTimestamps` overwrote `CreatedAt`/`UpdatedAt` on every insert**, unconditionally. Found while implementing the fixture: the existing seeder's `now.AddDays(-20)` backdating was being silently discarded, so every "historical" row claimed to have been created at seed time. | Demo history collapses to "five seconds ago"; any caller-supplied timestamp is silently lost | **Medium** |
| W16 | **An item cannot be dragged to the last position.** `computeDropPriority` treats a drop as "insert before the target", and the only drop targets are existing rows — so there is no gesture for the tail, and the `prev && !next` branch is unreachable. Found by writing the test. | Minor UX gap in an existing feature | **Low** |

W15 and W16 were found during implementation rather than during the initial
read — W15 by writing a fixture with backdated history and watching it not
persist, W16 by writing a test whose expectation turned out to be wrong about the
code rather than the code being wrong. W15 is fixed (timestamps are now filled
only when unset). W16 is **not** fixed: changing drag semantics is a product
decision, not a productionization one, so it is recorded as a known property in
`reorder.test.ts` and in `docs/TESTING.md` instead.

## 6. Security findings

**Threat model first, because it determines which of the standard list applies.**

This is a **single-user personal tool with no accounts**, being published as a
**public, unauthenticated demo**. That shape removes whole categories of risk
and creates one large one.

*Structurally absent — not "mitigated", genuinely not present:*

- **No CSRF surface.** There are no cookies, no sessions, and no ambient
  credentials. A forged cross-site request achieves exactly what a direct
  request achieves, so there is nothing to forge.
- **No authentication or authorization vulnerabilities**, because there is no
  authentication. There are no users, no ownership, and therefore no IDOR: every
  row is equally public to everyone by design.
- **No SQL injection.** Every query goes through EF Core LINQ. There is no raw
  SQL, no string-concatenated query, and no dynamic table or column name
  anywhere in the repository.
- **No file upload, no file serving, no path handling** → no path traversal, no
  unrestricted upload.
- **No deserialization of untrusted binary formats**; `System.Text.Json` with
  fixed target types only.
- **No secrets to leak.** The application has no API keys, no tokens, and no
  external service credentials. The only connection string is a local file path.
  Git history was scanned in full and contains none.

*Real findings:*

| # | Finding | Severity | Status |
| --- | --- | --- | --- |
| S1 | **The public demo API accepts unauthenticated writes from anyone**, with no rate limit and no request-size cap. Combined with W4, one client can fill the host's disk. | **High** | Fix with limits + reset; residual risk documented |
| S2 | **Missing input validation at the trust boundary** — enum range (W3), string length (W4), numeric sign (W6), state coherence (W7). The `[ApiController]` attribute validates what is annotated; these fields are not annotated. | **High** | Fix |
| S3 | **`app.UseAuthorization()` is security theater.** There is no `UseAuthentication()`, no authentication scheme, and no `[Authorize]` attribute anywhere in the codebase. This middleware cannot deny a single request. It looks like an access control and is not one. | **Medium** | Remove and document |
| S4 | **`ProductLink` accepts any string, including `javascript:alert(1)`** (confirmed live, `201`). It is *not* currently rendered as an anchor, so this is **not** an XSS vulnerability today — it is invalid data in a field typed `url`. Fixing it is input validation, and will be described as such and not as XSS protection. | **Low** | Fix |
| S5 | **CORS is configured but is not an access control here.** With no cookies and no credentials, `AllowedOrigins` restricts *browsers on other origins*, not `curl`. It is a correctness setting. Documenting it as a security boundary would be the theater this audit is meant to catch. | **Informational** | Document honestly |
| S6 | **No global exception handler.** An unhandled exception returns an empty 500. ASP.NET Core does not leak stack traces in `Production`, so this is not an information-disclosure bug — it is a diagnosability gap. | **Low** | Fix |
| S7 | Dependency vulnerabilities: nanoid `<3.3.18` (high), SQLitePCLRaw.lib.e_sqlite3 2.1.10 (high, transitive). | **High** | Fix + gate in CI |

**`UseHttpsRedirection()` sits inside the `IsDevelopment()` branch.** That looks
inverted, but it is correct for this deployment: the app runs behind a
TLS-terminating proxy that forwards plain HTTP internally, so redirecting in
production would cause a loop. This is a deliberate choice and will be
documented as one rather than "fixed".

## 7. Data and privacy concerns

This is the highest-consequence section, so it is stated bluntly.

- The author's real database, `server/UpgradePlanner.Api/upgradeplanner.db`
  (28 KB), contains their actual purchase plans. **It is untracked, matched by
  `.gitignore:4` (`*.db`), and excluded from the image build by
  `.dockerignore`.** Verified both ways. It has never been committed.
- The existing demo fixture is **generated in code**, not exported from the
  author's instance. That is already the right barrier and will be kept.
- **The gap is W1**: there is no barrier preventing the demo path from running
  *against the real database*. Today the only thing standing between
  `upgradeplanner.db` and `DELETE FROM Upgrades` is one environment variable
  spelled correctly. The fix must be structural, not careful.
- Publishing the repository makes the **source** public. It does not publish the
  database. Confirmed above. The repository must go public for free GitHub Pages
  hosting; that is a decision for the author, not something to do unilaterally.

## 8. Deployment concerns

- Nothing is deployed. `render.yaml` exists but points `AllowedOrigins` at the
  literal placeholder `https://REPLACE-WITH-YOUR-VERCEL-URL.vercel.app`, so the
  browser would be blocked even if it deployed today.
- `client/vercel.json` targets a third provider. The repo currently carries
  half-finished config for Vercel, Render, and (in history) Azure.
- The client hardcodes nothing about its own base path, so it only works when
  served from `/`. A GitHub Pages project site serves from `/upgrade-planner/`,
  which would 404 every asset **and** every client-side route.
- Free-tier terms, verified with web access rather than memory:
  - **GitHub Pages** requires a **public repository** on a free personal
    account; Pages from a private repo needs GitHub Pro. No card either way.
  - **Render** free web services: 750 instance-hours/month, **spin down after
    15 minutes idle with a ~1 minute cold start**, and an **ephemeral
    filesystem**. Critically, with **no payment method on file** Render
    *suspends* services on limit overrun rather than billing — so it is free in
    the strict sense required here.
  - Render's ephemeral disk is normally a drawback. For a demo whose data is
    supposed to be disposable, it is an **advantage**: the fixture regenerates
    on every cold start, so vandalism self-heals.

## 9. Recommended architecture

Incremental. One structural change, justified by testability rather than taste:

- Extract business rules from `UpgradesController` into
  `Services/UpgradeService`. The controller keeps HTTP concerns (status codes,
  routing, DTO mapping); the service owns cycle detection, the delete-guard, and
  state coherence. **This is done to create a testable seam, not to add a
  layer** — it is what makes W2 fixable.
- **No repository abstraction.** `DbContext` already is one. Wrapping it would
  be the unearned pattern.
- Keep `RecommendationEngine` exactly as it is: a pure static function.
- Add a pure, total configuration parser so a typo degrades to a documented
  default with a warning instead of enabling the wrong mode.
- Split `DemoSeeder` into two separately-named operations — `SeedIfEmpty` and
  `ResetToDemoData` — so no call site can ask for one and get the other, and put
  the demo in its own database file so it cannot collide with the real one.

## 10. Recommended deployment

| Component | Provider | Why |
| --- | --- | --- |
| React SPA | **GitHub Pages** via Actions | Repo is already on GitHub; deployment authenticates with the workflow's built-in `GITHUB_TOKEN`. **No new account, no new secret.** Requires the repo to be public. |
| .NET API | **Render**, Docker runtime, free plan | Only free, no-card option that runs a long-lived .NET container. A `Dockerfile` and `render.yaml` already exist. |
| Database | **SQLite on Render's ephemeral disk** | Free, zero new services, and its impermanence is the demo-reset mechanism rather than a problem to work around. |

Rejected, with grounds: **Vercel** (commercial-use terms are a live question for
a portfolio piece, and it adds an account for a job GitHub Pages already does);
**Fly.io** (requires a payment method); **Azure App Service** (the free F1 tier
still requires a card at subscription signup — the author already tried and
reverted this, see commits `7d01246` / `4994890`); **Neon / Supabase Postgres**
(a real database is the correct call for real data, but this demo's data is
meant to be disposable, and adding managed Postgres adds an account and a secret
to solve a problem the demo does not have).

## 11. Major risks

1. **W1 destroying the author's real data during this very workflow.** Mitigated
   by never pointing a demo-mode process at `upgradeplanner.db`; every command
   run during this assessment used a throwaway database in a scratch directory,
   and the author's already-running instance (PID 17504) was left untouched.
2. **Render cold starts.** A reviewer clicking the link after 15 minutes of
   quiet waits ~1 minute. The frontend must say so explicitly rather than showing
   a dead spinner, or the demo reads as broken.
3. **Docker on Render's free plan** is not stated outright in Render's public
   docs. `render.yaml` already declares `runtime: docker` + `plan: free`; if
   Render rejects that combination it will surface at deploy time.
4. **Two blockers require the author personally**: flipping the repository to
   public, and creating a Render account. Neither can or should be done
   unilaterally.

## 12. Implementation order

1. Fix W1 first — the destructive seeder — before anything else runs.
2. Extract the service seam (W5); no behavior change.
3. Validation and security fixes (S1, S2, S4, S6, S7; remove S3).
4. Rebuild the demo fixture: all categories, all statuses, deliberate edges.
5. Configuration layer, health endpoint, error boundary, build metadata.
6. Tests — the properties above, not a coverage number.
7. Base-path-aware build, CI, deploy, smoke test.
8. Docs and interview guide last, describing what the code actually does.
