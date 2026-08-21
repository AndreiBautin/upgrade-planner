# Security

## Threat model first

The standard checklist is useless until you know what this is. Two facts
determine everything below.

**1. There is no authentication.** No accounts, no sessions, no cookies, no
tokens. That is a product decision — the app was built as a single-user planning
tool — not an oversight.

**2. The deployed copy is a public, unauthenticated, writable demo.** Its data is
generated in source code and disposable.

### What is structurally absent

Naming what genuinely does not exist is more useful than a checklist of items
marked N/A, because each of these is absent by *construction* — there is no
configuration that could reintroduce it.

| Category | Why it does not exist here |
| --- | --- |
| **CSRF** | No cookies, no sessions, no ambient credentials of any kind. A forged cross-site request accomplishes exactly what a direct `curl` accomplishes, so there is nothing to forge. Adding an anti-forgery token would be pure theatre. |
| **Broken authn / authz, IDOR** | There is no authentication to break and no ownership to confuse. Every row is equally public to everyone, deliberately. `/api/upgrades/1` is not a horizontal privilege escalation when there are no privileges. |
| **SQL injection** | Every query is EF Core LINQ. The repository contains no raw SQL, no string-concatenated query, no `FromSqlRaw`, and no dynamic table or column name. |
| **Path traversal, unrestricted upload** | The API serves no files and accepts none. There is no file path anywhere in a request. |
| **Unsafe deserialization** | `System.Text.Json` into fixed target types. No `BinaryFormatter`, no polymorphic type handling, no `$type` resolution. |
| **Secrets exposure** | The application has no API keys, no tokens, and no external service credentials. The only connection string is a local file path. Full git history was scanned (see below) and contains none. |

### What the real risk is

**Anyone on the internet can create, edit, and delete rows in the demo.** That is
the exposure, and it is accepted rather than fixed, because a read-only demo of a
CRUD planning tool demonstrates nothing. What is fixed is the *blast radius*.

## Findings and what was done

Each finding below was confirmed against a running instance before it was fixed,
and re-confirmed after.

### S1 — Unauthenticated writes with no limits — **High** — mitigated, residual risk documented

Before: no rate limit, and Kestrel's default 30 MB body cap. A single client
could write continuously, and — combined with S2 — store a 2 MB note per request
until the host's disk filled.

Fixed:

- **Rate limiting** (`Program.cs`): a fixed 120-request-per-minute window
  partitioned by client address, returning 429. Verified: 130 rapid requests
  produced 109 × 200 and 21 × 429.
- **64 KB request body cap** (`Program.cs`, Kestrel limits). Verified: a 2 MB
  body returns **413**, not 500 — Kestrel's typed `BadHttpRequestException` is
  mapped by its own status code rather than swallowed as a server error.
- **Field length limits** — see S2.
- **Self-healing data.** The demo runs on an instance with an ephemeral
  filesystem that spins down after 15 minutes idle, so the database is recreated
  and reseeded on every cold start. Vandalism disappears on its own, with no
  scheduled job and no reset endpoint that would itself need protecting.

**Residual risk, stated plainly:** within any 15-minute window a determined
visitor can fill the demo with junk, and other visitors will see it. The rate
limit bounds the rate, not the intent. This is accepted: the alternative is
either authentication (which the app does not have) or a read-only demo (which
would not show the app working). Nothing of value is at risk — the data is fake
by construction and the container is disposable.

The rate limiter partitions on `X-Forwarded-For` when present. **That header can
be spoofed.** It is used because behind a proxy the socket address is the
proxy's, making the limit global instead of per-client. It is a fairness control,
not a security control, and is not presented as one.

### S2 — Missing validation at the trust boundary — **High** — fixed

`[ApiController]` validates what is annotated. These fields were not annotated,
so they were not validated. All confirmed live with `201 Created` before the fix,
`400 Bad Request` after.

| Input | Before | After |
| --- | --- | --- |
| `{"category": 99}` | 201, stored; client rendered an empty tag and `class="pill undefined"` | 400 |
| `{"status": 99}` | 201, stored | 400 |
| `{"estimatedCost": -500}` | 201 | 400 |
| `notes` of 2,000,000 characters | 201, stored in full | 400 (and 413 above 64 KB) |
| `{"status": 0, "purchasedDate": "2020-01-01", "actualCost": 9}` | 201 — an "Idea" with a purchase date | 400 |
| `purchasedDate` in the year 2200 | 201 | 400 |

Implemented as `EnumDefinedAttribute` (`Enum.IsDefined`, because the JSON binder
converts any integer to an enum-typed property), `[Range]` and `[MaxLength]` on
`UpsertUpgradeDto`, and `IValidatableObject` for the cross-field rules.

**A limitation worth stating:** SQLite does not enforce `VARCHAR` lengths. The
length limits live at the API boundary only. Adding `MaxLength` to the entity
would generate a migration that changes nothing at the storage layer — a column
type with no enforcement behind it — so it was not added. The boundary check is
the enforcement, and this is the honest description of it.

### S3 — `app.UseAuthorization()` was security theatre — **Medium** — removed

The middleware was in the pipeline. There was no `UseAuthentication()`, no
authentication scheme registered, and no `[Authorize]` attribute anywhere in the
codebase. It could not deny a single request. It looked like an access control
and was incapable of being one.

It was **removed**, and `Program.cs` carries a comment explaining the absence so
nobody re-adds it reflexively. Deleting a line and documenting the gap is
strictly better than keeping a line that provides false assurance.

### S4 — `ProductLink` accepted any string — **Low** — fixed, and described accurately

`{"productLink": "javascript:alert(1)"}` returned 201 and was stored. Now 400,
via `HttpUrlAttribute`.

**This was not an XSS vulnerability and the fix is not an XSS defence.** The
field is never rendered as an anchor — the client shows it in an
`<input type="url">` — so the stored value was never executable. It was invalid
data in a field whose entire meaning is "a link to a product page", and a value
that would have to be rejected the day the field becomes clickable. Calling it
XSS protection would be the theatre this audit exists to remove.

### S5 — CORS is not an access control here — **Informational** — documented

`ALLOWED_ORIGINS` restricts which **browser origins** may read responses. With no
cookies and no credentials, it does nothing against `curl`, a script, or any
non-browser client. It is a correctness setting that keeps the deployed frontend
working; it is not a security boundary, and this document does not count it as
one.

The parser drops malformed entries with a warning, normalises away a trailing
slash (which silently breaks CORS matching and is miserable to debug from a
deployed page), and falls back to `http://localhost:5176`. If `ALLOWED_ORIGINS`
is unset in Production, startup logs a warning saying the deployed frontend will
be blocked.

### S6 — No global exception handler — **Low** — fixed

An unhandled exception returned an empty 500. ASP.NET Core already withholds
stack traces outside Development, so this was a diagnosability gap rather than an
information leak. There is now a handler returning `ProblemDetails`, logging
method and path, and including exception detail **only** in Development.

### S7 — Vulnerable dependencies — **High** — fixed and gated

| Package | Advisory | Resolution |
| --- | --- | --- |
| `nanoid < 3.3.18` (transitive, via vite → postcss) | GHSA-2v37-7h3g-55p8, high | `npm audit fix` → 3.3.18 |
| `SQLitePCLRaw.lib.e_sqlite3 2.1.10` (transitive, via EF Core 9.0.9) | CVE-2025-6965 / GHSA-2m69-gcr7-jv3q, high — affects ≤ 2.1.11 | Explicit `PackageReference` pinning `SQLitePCLRaw.bundle_e_sqlite3` to **2.1.13** |

Both audits are clean, verified by running them. The pin carries a comment saying
exactly when it may be removed, so it does not become permanent cargo.

## HTTPS redirection is inside the `IsDevelopment()` branch — on purpose

This looks inverted and is not. In production the app runs behind a
TLS-terminating proxy that forwards plain HTTP on the internal hop; redirecting
there would produce a loop. TLS is terminated at the edge by the host, which also
supplies the certificate. Locally, where nothing terminates TLS, the redirect is
enabled.

## Data protection

The highest-consequence guarantee in the project: **the author's personal data
cannot reach the public demo.** It rests on three structural barriers, not on
care.

1. **Generate, never capture.** The fixture is `Data/DemoData.cs` — source code,
   readable by anyone with the repository. There is no export step from a
   personal device anywhere in the pipeline.
2. **Separate namespaces.** In demo mode the connection string is
   `Data Source=demo.db`, hard-coded in `AppOptions` and **not read from
   configuration at all**. No environment variable can point a demo seed at the
   real database. Tested: `Demo_mode_uses_its_own_database_whatever_configuration_says`.
3. **Seed only into empty storage.** `SeedIfEmpty` returns 0 and writes nothing
   if any row exists. Tested: `SeedIfEmpty_never_overwrites_existing_data`.

This replaced a genuinely dangerous predecessor. The previous `DemoSeeder.Reseed`
executed `RemoveRange(db.Upgrades)` **unconditionally** whenever the string
`SeedDemoData` equalled `"true"` — one mis-set environment variable on the
author's machine away from silently destroying their real database. Filling and
wiping are now two separately named functions, never one function with a flag, so
no call site can ask for one and receive the other. The destructive one
(`ResetToDemoData`) is reachable only from an explicit `--reset-demo-data`
command-line flag, which refuses to run unless demo mode is on — and demo mode
forces the demo database.

The real database, `server/UpgradePlanner.Api/upgradeplanner.db`, is matched by
`.gitignore` (`*.db`), excluded from the container build by `.dockerignore`, and
has never been committed. Verified with `git log --all --name-only`.

### Personal data in logs

Logging records **event names and scalars only**. No log statement in the
application can print an upgrade's title, description, or notes. The
configuration log prints a boolean, a count, a commit sha, and an environment
name. The exception log prints an HTTP method and a path.

EF Core's SQL logging is set to `Warning` in production. Note that EF masks
parameter values as `?` regardless, so even at `Information` it prints no user
content — the level is about noise, not exposure. That is why these logs are safe
to leave on.

The demo fixture is scanned by tests for email addresses, phone numbers,
credential-shaped strings, and any URL outside the RFC 2606 reserved
`example.com` / `example.org`. Those scans were verified non-vacuous by
temporarily injecting an email, a phone number, and a real-looking host into the
fixture: **three tests failed, one per category.** The fixture was then restored.

## Secrets management

There are none, which is the strongest possible position and mostly a
consequence of the deployment choice:

- GitHub Pages deploys with the workflow's built-in `GITHUB_TOKEN`. No secret to
  create, store, rotate, or leak.
- The API has no credentials. `ALLOWED_ORIGINS` is a public URL and is set in the
  Render dashboard rather than committed.
- `.env.example` documents every variable and states explicitly that **any value
  prefixed `VITE_` is compiled into the public bundle and is world-readable**, so
  a credential must never be put behind that prefix.
- `.gitignore` covers `.env`, `.env.local`, and `.env.*.local` while keeping the
  `.env.example` files tracked.

## Git history

Scanned in full before recommending the repository be made public.

- Every path ever added, via `git log --all --name-only`: **no `.db`, `.env`,
  `.pem`, `.key`, or credential file has ever been committed.**
- The complete patch history (4,838 lines) searched for GitHub/OpenAI/AWS/Google/
  Slack token shapes, private-key headers, and `password|secret|api_key = "…"`
  assignments: **no matches.**
- Searched for email addresses and phone numbers: **no matches** beyond a
  `localhost` variable in an `.http` scratch file.

Nothing needs rotating, and no history rewrite is required. Had a secret been
found, the correct order would have been rotate first, then decide about
history — a removed-but-already-leaked credential is still leaked — and that
decision would have been the author's, not something to do unilaterally.

## Scanning in CI

- **`gitleaks`** over the **full history** (`fetch-depth: 0`). A credential
  removed from the working tree is still leaked if it sits in an old commit.
- **`npm audit --audit-level=high`**.
- **NuGet audit**, both as `NuGetAudit`/`NuGetAuditLevel=high` in the csproj (so
  it fails the build) and as an explicit `dotnet list package --vulnerable` step.

Both dependency gates are set at **high**, not at the lowest severity. Failing a
build on a low-severity advisory in a build-time transitive package teaches
people to ignore the job, and an ignored gate is not a gate. The parsing of the
NuGet audit output was itself verified against a known-vulnerable sample and a
low-severity sample, so it neither misses a high nor fires on noise.

## Remaining risks

Stated without hedging.

1. **The demo API accepts writes from anyone.** Bounded by rate limiting, a body
   cap, field validation, and a container that resets itself — but within a
   15-minute window, a visitor can fill the demo with nonsense that other
   visitors will see. Accepted deliberately; the data is fake and the container
   is disposable.
2. **The rate limit is per-instance and in-memory.** More than one replica
   multiplies the effective limit by the replica count. Irrelevant on a free tier
   that runs exactly one.
3. **`X-Forwarded-For` is spoofable**, so the rate-limit partition can be evaded
   by varying the header. Fixing this properly needs `ForwardedHeaders` with a
   known-proxy allowlist. Not done, because the limiter protects a disposable
   demo, and a partial fix here would be more misleading than the honest note.
4. **String length is enforced only at the API boundary**, because SQLite does
   not enforce column lengths. A future code path that writes to the `DbContext`
   without going through `UpsertUpgradeDto` would bypass it.
5. **No HTTP security headers** (CSP, `X-Content-Type-Options`, etc.) are set by
   the app. GitHub Pages controls the response headers for the static site and
   the app cannot override them, so setting them in application code would be a
   header the platform ignores — precisely the theatre named in S3. The gap is
   documented instead.
6. **No authentication**, so nothing here is suitable for real multi-user data.
   Turning this into a multi-user product means real accounts, per-row ownership,
   and an authorization check on every path — not a flag.
