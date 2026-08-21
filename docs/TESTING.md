# Testing

**180 tests. 137 xUnit on the API, 43 Vitest on the client.** Before
productionization there were zero — no test project, no runner, nothing.

```bash
dotnet test UpgradePlanner.sln -c Release   # 137 passed
cd client && npm test                       # 43 passed
```

No coverage number is reported here, and none is chased. Chasing a percentage
produces tests written to raise the percentage, which are exactly the tests that
do not catch bugs. What follows is what is tested and why each thing earned a
test.

## Strategy by layer

| Layer | Approach | Why |
| --- | --- | --- |
| `RecommendationEngine` | Direct unit tests, no I/O | It is already a pure function. `Compute(list) → dictionary`. Nothing to mock. |
| `UpgradeService` | Integration against **real SQLite in memory** | The rules interact with foreign keys and check constraints. A fake would not. |
| `UpsertUpgradeDto` | The same `Validator` ASP.NET Core runs | A pass here is a pass at the real trust boundary. |
| `DemoData` / `DemoSeeder` | Property tests over the fixture and the seed operations | These protect the personal-data guarantee. |
| `AppOptions` / `config.ts` | Pure-function tests both sides | A typo must never select the destructive mode. |
| `reorder.ts`, `format.ts`, `types.ts` | Unit tests | Small pure functions with real edge cases. |

### Real SQLite, not the in-memory provider

`TestDatabase` opens `Filename=:memory:` through the actual SQLite provider and
runs the migrations.

This matters. EF Core's `InMemory` provider **silently ignores check constraints
and foreign keys**. A suite built on it would happily pass while
`CK_Upgrade_Priority` and `ON DELETE RESTRICT` were broken — and those two
constraints are precisely the guarantees worth testing. Running the migrations
also means every test re-proves that the schema builds from empty, which is what
happens on every production cold start.

## What is prioritized

### 1. The core business logic — 13 tests

The priority-propagation algorithm is the product. `RecommendationEngineTests`
covers: propagation up a three-link chain; that the reported source is the
highest-priority *descendant* rather than the immediate child; that a high parent
is not dragged down by a low child; that a branch takes the maximum of several
children; blocked-ness against every non-purchased status; a dangling
prerequisite reference; a cyclic chain terminating instead of hanging; and the
empty set.

### 2. Destructive operations, from the "must not destroy" side

The interesting assertion about a delete is never that it worked.

- `Delete_is_refused_while_another_upgrade_depends_on_it`
- `A_refused_delete_leaves_the_row_intact` — asserts the row survives *and* the
  total count is unchanged, not just that the call returned an error
- `SeedIfEmpty_never_overwrites_existing_data` — plants a row called "My real
  upgrade", seeds, asserts exactly one row remains and it is that one

That last test is the most important in the repository. It is what stands between
a mis-set environment variable and the author's real database.

### 3. Validation at the trust boundary — 30 tests

Every case in `ValidationTests` corresponds to an input that was **confirmed
accepted with `201 Created`** against a running instance before the fix:
out-of-range enums, negative money, a two-million-character note, purchase
details on an unpurchased item, a future purchase date, a `javascript:` URL.

Rejection tests alone would be a trap — a validator that rejects everything
passes all of them. So the suite also asserts the positives:
`A_well_formed_upgrade_is_accepted`, `A_fully_populated_upgrade_is_accepted`,
every declared category, both priority boundaries, a zero cost (free things
exist), and a purchased item with no recorded details (marking something bought
without remembering the price is legitimate).

### 4. The properties the deployment depends on

| Property | Test |
| --- | --- |
| The demo fixture contains nothing personal | 4 scans + 2 anti-vacuity guards |
| Seeding cannot overwrite existing data | `SeedIfEmpty_never_overwrites_existing_data` |
| Demo mode cannot reach the real database | `Demo_mode_uses_its_own_database_whatever_configuration_says` |
| Config parsing cannot crash | `Parsing_never_throws_however_bad_the_input` |
| A typo cannot enable the wrong mode | `A_typo_falls_back_to_off_and_says_so` (6 spellings) |
| The base path cannot break deep links | `normalizeBasePath` — 7 cases including a pasted full URL |
| The fixture does not rot | `Every_date_is_relative_to_seed_time` |
| A reorder always produces a valid priority | `always produces a value the API will accept` |

### 5. Tests that keep the other tests honest

A scanning test passes trivially if there is nothing to scan. Two tests prevent
that:

- `The_personal_data_scan_actually_has_something_to_scan` — fails if the fixture
  text collection drops below 20 strings or contains nothing substantial.
- `The_email_scan_would_catch_an_email_if_one_appeared` — asserts the pattern
  matches known-bad input.

**And the scans were verified by mutation.** An email address, a phone number,
and a real-looking product host were temporarily injected into `DemoData.cs`;
exactly **three tests failed, one per category**, and the fixture was restored.
That is the difference between a green suite and a suite that means something.

## What is deliberately not tested, and why

This section is the point. Knowing where the line is drawn is worth more than a
longer test count.

| Not tested | Why |
| --- | --- |
| **React component rendering** | No jsdom, no Testing Library. The components are thin: they fetch, map to a list, and render. The logic worth testing was *extracted* out of them — `reorder.ts`, `format.ts`, `withStatus` — and is tested directly. Adding a DOM test runner to assert that a `<span>` contains a title would test React, not this app. |
| **The HTTP pipeline itself** — rate limiting, the 64 KB body cap, exception-to-status mapping | Verified by hand against a running instance (130 requests → 109×200 + 21×429; a 2 MB body → 413) and continuously by the CI smoke test. Not covered by `WebApplicationFactory`, because the composition root returns an exit code for the `--reset-demo-data` path, which an in-process test host cannot model cleanly. The cost of restructuring `Program.cs` to suit the harness outweighed the benefit. |
| **CORS behaviour** | `AllowedOrigins` *parsing* is tested thoroughly. Whether the browser then honours the header is ASP.NET Core's middleware and the browser's job, not this codebase's. |
| **EF Core / the ORM** | Testing that `SaveChanges` saves is testing Microsoft's code. What *is* tested is the behaviour this repo adds on top: timestamp stamping, and that backdated history survives it. |
| **`RecommendationEngine` performance** | It is O(n) over tens of rows. A benchmark would encode a number nobody will act on. Where it stops scaling is documented in [ARCHITECTURE.md](ARCHITECTURE.md) instead. |
| **The deployed site, beyond a smoke test** | The CI smoke test asserts the live URL serves the app with a JS bundle. Full end-to-end browser automation against a free instance that cold-starts for ~60s would be slow and flaky for little added signal. |
| **Concurrency** | SQLite serialises writes and the app has one user. Concurrency tests would assert behaviour the design does not promise. |

## A known limitation the tests documented rather than hid

Writing `reorder.test.ts` surfaced a real behaviour worth naming: **an item
cannot be dragged to the last position in the list.**

Dropping onto a row means "insert before this row", and the only drop targets are
existing rows — so there is no gesture for the tail. The `prev && !next` branch in
`computeDropPriority` is consequently unreachable through the UI.

This is a pre-existing UX quirk, not a regression, and changing the drag
semantics is a product decision rather than a productionization one. It is
recorded in the test (`cannot place an item below the last row`) so it is a known
property rather than a surprise.

## Test helpers

| Helper | Purpose |
| --- | --- |
| `TestDatabase` | Real in-memory SQLite + migrations, disposed per test |
| `UpgradeServiceTests.Given(...)` | Creates an upgrade and asserts it succeeded, so arrange steps do not silently no-op |
| `ValidationTests.Valid()` | A known-good DTO; each test mutates one field, so a failure names one cause |
| `ValidationTests.Rejects(dto, member)` | Asserts a specific *field* failed, not merely that something did |
| `reorder.test.ts` `row(id, priority)` | A full `UpgradeDto` with one field varied |

`Rejects` is deliberately field-specific. `Assert.NotEmpty(results)` would pass
when the wrong rule fired, which is how a validation suite goes green while
validating the wrong thing.

## In CI

Every push runs, on Linux:

- `dotnet build` + `dotnet test` on the whole solution
- `npm ci` (frozen lockfile — drift fails rather than silently resolving),
  `lint`, `typecheck`, `test`
- Both dependency audits, gated at **high**
- `gitleaks` over the **full** history
- **Both shipping build configurations** — default and demo — because a failure
  that only appears under the demo config must not first surface at deploy time
- Assertions that the base path reached `dist/index.html` and that
  `dist/404.html` was emitted
- A migrate-and-seed-from-clean run against an empty database, mirroring a
  production cold start
- A smoke test that fetches the live URL after deploying and checks the response
  body

Every step above was run locally before being committed, except `npm ci` — which
hit a Windows file lock from a running dev server and works on the Linux
runner — and `gitleaks`, which needs the action. The audit gate's output parsing
was checked against both a real high-severity advisory row and a low-severity row
so it neither misses a finding nor fires on noise.
