# Demo data

The deployed app must never contain the author's real data. This document
describes how that is guaranteed structurally rather than carefully, and what the
demo dataset actually contains.

## The three barriers

Each is a property of the code, not a habit. Each is tested.

### 1. Generate, never capture

The fixture is [`Data/DemoData.cs`](../server/UpgradePlanner.Api/Data/DemoData.cs)
— C# source, committed to the repository, readable by anyone who can read this
sentence. `DemoData.Build(now)` is a pure function returning a list.

**There is no export step from a personal device anywhere in the pipeline.** No
script dumps the real database, no fixture file is generated from one, and
nothing in the deploy path reads `upgradeplanner.db`. The demo cannot contain
personal data because there is no mechanism by which personal data could reach
it.

### 2. Separate namespaces

Demo mode uses its own database file, and **does not read the connection string
from configuration at all**:

```csharp
// AppOptions.Parse
var connectionString = demoMode
    ? DemoConnectionString              // "Data Source=demo.db", hard-coded
    : configuredConnection ?? DefaultConnectionString;
```

Setting `ConnectionStrings__Default=Data Source=upgradeplanner.db` alongside
`DEMO_MODE=true` does not point the demo at the real database. It logs a warning
and uses `demo.db` anyway. There is no environment variable that can defeat this,
because the value is not configurable.

> Tested by `AppOptionsTests.Demo_mode_uses_its_own_database_whatever_configuration_says`.

### 3. Seed only into empty storage

Filling an empty database and wiping a populated one are **two separately named
operations**, never one function with a `bool overwrite` parameter:

```csharp
DemoSeeder.SeedIfEmpty(db, now)       // writes only if the table is empty. Never deletes.
DemoSeeder.ResetToDemoData(db, now)   // deletes everything, then seeds. Destructive.
```

A call site cannot ask for the first and receive the second by passing the wrong
argument, because there is no argument to get wrong. Startup calls **only**
`SeedIfEmpty`. The destructive one is reachable exclusively from the explicit
`--reset-demo-data` command-line flag, which refuses to run unless demo mode is
on — and demo mode forces `demo.db`.

> Tested by `DemoSeederTests.SeedIfEmpty_never_overwrites_existing_data`, which
> plants a row called "My real upgrade", seeds, and asserts that exactly one row
> remains and it is that one.

### What this replaced

The previous implementation was a single function:

```csharp
public static void Reseed(AppDbContext db)
{
    db.Upgrades.RemoveRange(db.Upgrades);   // unconditional
    db.SaveChanges();
    ...
}
```

called whenever the configuration string `SeedDemoData` equalled `"true"`. There
was no "seed if empty" path at all. One mis-set environment variable on the
author's machine would have silently and irreversibly destroyed their real
planning data. This was the most serious finding of the whole productionization.

## What is in the dataset

Thirteen upgrades. The content is chosen so a reviewer understands the app in
about ten seconds.

### The showcase: a three-link prerequisite chain

| Upgrade | Priority | Effective | Status | Requires |
| --- | --- | --- | --- | --- |
| Bigger desk | 70 | **92** | Ready to Buy | — |
| Third monitor | 75 | **92** | Idea | Bigger desk |
| Monitor arm | 92 | 92 | Idea | Third monitor |

This exists to make the product's one non-obvious idea visible immediately. The
desk is only priority 70, yet the dashboard ranks it above the **priority 85**
ergonomic chair and near the **priority 88** winter tyres — because it is the
first unblocked step toward the priority-92 monitor arm. A reviewer who notices
"why is the 70 above the 85?" has understood the entire application.

### Coverage

Every category and every status appears at least once, so no UI state goes
undemonstrated.

- **Categories** (all 7): Home (blackout curtains, radiator, robot vacuum),
  Office (desk, chair), Gym (weight bench), Technology (monitor, arm, NAS),
  Vehicle (winter tyres), Lifestyle (under-desk treadmill), Other (tap washer,
  shed roof).
- **Statuses** (all 5): Idea, Researching, Ready to Buy, Purchased, Cancelled.

> Tested by `Every_category_appears_at_least_once` and
> `Every_status_appears_at_least_once`, which iterate `Enum.GetValues` rather
> than a hand-written list, so adding a category makes the test fail until the
> fixture covers it.

### Deliberate edge cases

| Case | Row | What it exercises |
| --- | --- | --- |
| Minimal record | *Replace the kitchen tap washer* | Only required fields; description, notes, link, cost, prerequisite all null |
| Priority floor | *Replace the kitchen tap washer* — priority **1** | The lower bound of `CK_Upgrade_Priority` |
| Priority ceiling | *Fix the bedroom radiator* — priority **100** | The upper bound |
| No cost recorded | *Fix the bedroom radiator* | The cost column must show an em dash, not `$0` or `NaN` — "unknown" and "free" must not look alike |
| Very long title | *Replace the garden shed roof felt before the autumn rain gets into the timber* (77 chars) | List layout wraps or truncates instead of overflowing |
| Long description | same row, 393 chars | The detail view has real content to lay out |
| Blocked item | *Monitor arm*, *Third monitor* | The "Blocked" pill and the "Requires: …" hint |
| Unlocking item | *Bigger desk* | The "Unlocks: …" hint and inherited priority |
| Over-estimate | *Robot vacuum* — est. $260, actual $319 | Purchased rows show actual, not estimated, cost |
| Under-estimate | *Adjustable weight bench* — est. $300, actual $275 | The same, in the other direction |
| Abandoned | *Under-desk treadmill* | Cancelled status, excluded from recommendations |

The **empty state** is the one screen the fixture cannot show while also showing
data. To see it, run without `DEMO_MODE` against a fresh database, or delete
every row.

### Dates are relative, never fixed

Every timestamp is an offset from the seed time — `now.AddDays(-46)`, not
`2026-07-05`. A fixture pinned to absolute dates rots: opened a year later it
shows dead streaks, stale "recently updated" hints, and empty
this-month statistics.

> Tested by `Every_date_is_relative_to_seed_time`, which builds the fixture at
> two `now` values three years apart and asserts every date moved with it. The
> build stays deterministic for any given `now`
> (`The_fixture_is_deterministic_for_a_given_now`).

This required a fix elsewhere: `AppDbContext.TouchTimestamps` previously stamped
`CreatedAt`/`UpdatedAt` with `DateTime.UtcNow` on **every** insert, silently
overwriting the backdated history the old fixture asked for. It now fills those
fields only when they are unset, so a caller supplying its own history keeps it
while an ordinary insert still cannot end up with a zero timestamp.

### Nothing that looks like personal data

Four scans run as tests over every string in the fixture:

| Scan | Rejects |
| --- | --- |
| `The_fixture_contains_no_email_addresses` | anything matching an email shape |
| `The_fixture_contains_no_phone_numbers` | seven or more consecutive digits, or a grouped `+…` run |
| `The_fixture_contains_no_credential_shaped_strings` | `password:`/`api_key=` assignments, `ghp_…`, `sk-…`, private-key headers |
| `Every_url_in_the_fixture_points_at_a_reserved_example_domain` | any host other than `example.com` / `example.org` (RFC 2606, reserved and resolving nowhere) |

**These scans were verified to actually work.** An email address, a phone number,
and a real-looking product host were temporarily injected into the fixture;
exactly three tests failed, one per category. The fixture was then restored.

Two further tests keep them honest:
`The_personal_data_scan_actually_has_something_to_scan` fails if the text
collection ever becomes empty (a scan over nothing passes trivially), and
`The_email_scan_would_catch_an_email_if_one_appeared` asserts the pattern matches
known-bad input.

Finally, `Every_fixture_row_would_pass_the_public_api_validation` runs each row
through the same `Validator` the API uses: the demo cannot contain data a user
would be forbidden from entering.

## How seeding works

On startup, when `DEMO_MODE` is on:

```
Program.cs
  → db.Database.Migrate()              apply migrations (empty DB on a cold start)
  → DemoSeeder.SeedIfEmpty(db, UtcNow) writes 13 rows if the table is empty, else 0
  → logs "Demo seed complete. RowsWritten=13 TotalRows=13"
```

Rows are inserted in list order so a prerequisite always exists before something
points at it. `DemoData` declares each row after its own prerequisite;
`Every_prerequisite_key_is_defined_before_it_is_referenced` enforces that
ordering, and the seeder throws a named error rather than writing a null link if
it is ever violated.

Prerequisites are declared by **key**, not id:

```csharp
Row("monitor", "desk", new Upgrade { ... })
```

which is what keeps `Build` a pure function — it can describe the graph without a
database round-trip to discover generated ids.

## How to reset the demo

**In production: redeploy, or wait.** The free instance has an ephemeral
filesystem and spins down after 15 minutes idle. Every cold start begins with an
empty database, so the fixture regenerates itself. Vandalism self-heals.

This is why there is **no reset endpoint**. An unauthenticated destructive HTTP
endpoint would be a genuine vulnerability, and protecting it would mean
introducing the first secret in the project to solve a problem the platform
already solves for free.

**Locally:**

```bash
cd server/UpgradePlanner.Api
DEMO_MODE=true dotnet run -- --reset-demo-data
```

This wipes and reseeds, then exits. It refuses to run — with an error, not a
warning — if `DEMO_MODE` is not enabled, and demo mode forces `demo.db`, so it
cannot touch `upgradeplanner.db`.

To simply get a demo database without destroying anything, delete `demo.db` and
start normally; `SeedIfEmpty` fills it.

## Demo credentials

**There are none, because the app has no login.** Anyone opening the link is
straight into a working app with no sign-up, no seeded account, and no
credentials to circulate.

This is stated here and in the README rather than leaving a reviewer hunting for
a login that does not exist. The consequences of having no auth — including the
fact that any visitor can edit the demo — are worked through in
[SECURITY.md](SECURITY.md).
