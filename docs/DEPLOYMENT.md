# Deployment

Two free, no-credit-card services. The frontend is a static bundle on GitHub
Pages; the API is a container on Render.

| Component | Provider | URL |
| --- | --- | --- |
| React SPA | GitHub Pages | https://andreibautin.github.io/upgrade-planner/ |
| .NET API | Render (free instance, Docker) | set as `VITE_API_BASE_URL` once created |
| Database | SQLite on Render's ephemeral disk | recreated on every cold start |

## Why these, and what was rejected

**GitHub Pages for the frontend** — the deciding factor is that it adds
**no new account and no new secret**. The repository is already on GitHub, and
`actions/deploy-pages` authenticates with the workflow's built-in `GITHUB_TOKEN`.
Nothing to create, store, rotate, or leak. For a static bundle that is the whole
job, every other option was strictly more moving parts.

Its cost: Pages requires a **public repository** on a free personal account
(private Pages needs GitHub Pro). For a portfolio piece that is what you want
anyway, but it is a real constraint and it was the author's decision to make.

**Render for the API** — the .NET API is a long-running container, which rules
out every static and edge-function host. Of the container platforms, Render is
the one that is genuinely free with **no payment method**: verified against their
docs, which state that without a card on file Render *suspends* services on limit
overrun rather than billing. A `Dockerfile` and `render.yaml` already existed in
the repository.

**SQLite on the ephemeral disk** — normally a drawback, here an advantage. Free
instances lose their filesystem on restart, so the demo database is recreated and
reseeded on every cold start. Vandalism of a public, unauthenticated demo heals
itself with no scheduled job and no reset endpoint to protect.

### Rejected, and why

| Option | Why not |
| --- | --- |
| **Vercel** | Commercial-use terms on the hobby tier are a live question for a portfolio piece, and it adds an account for a job Pages already does. The repo's leftover `vercel.json` was deleted. |
| **Fly.io** | Requires a payment method. Fails the "free means free" bar. |
| **Azure App Service (F1)** | The free tier still needs a card at subscription signup. The author already tried this and reverted it — see commits `7d01246` / `4994890`. |
| **Cloudflare Pages** | A genuine alternative, and the right answer if the repo had to stay private (it connects to private repos, free, no card). Rejected only because it adds an account and a GitHub authorization that Pages does not need. |
| **Neon / Supabase Postgres** | The correct call for real data. This demo's data is *meant* to be disposable, so a managed database would add an account and a secret to solve a problem that does not exist here. Render's free Postgres also expires. |
| **Railway** | No meaningful free tier for a persistent service. |

## Accounts required

1. **GitHub** — already have it. Nothing further.
2. **Render** — free, sign in with GitHub, **no credit card**.

## Frontend: GitHub Pages

Already configured and running. Nothing to do by hand.

- Workflow: [`.github/workflows/deploy-pages.yml`](../.github/workflows/deploy-pages.yml)
- Trigger: every push to `master`, plus manual `workflow_dispatch`
- Pages source: **GitHub Actions** (set via the API, not the branch-based
  publisher)

### The base path, which is where static deploys usually break

A Pages *project* site is served from `/<repo>/`, not `/`. Get this wrong and you
get one of two classic failures: assets 404, or assets load and every client-side
route 404s.

It is handled with **one value**:

```
VITE_BASE_PATH  (workflow, = /${{ github.event.repository.name }}/)
      ↓
vite.config.ts  base: basePath          → asset URLs
      ↓
import.meta.env.BASE_URL                 (Vite derives this from `base`)
      ↓
config.ts  normalizeBasePath()
      ↓
<BrowserRouter basename={config.basePath}>  → routes
```

There is no second copy to drift. `normalizeBasePath` is unit-tested against
missing slashes, doubled slashes, and a full URL pasted in by mistake.

### SPA fallback, and an honest caveat

Static hosts have no rewrite rules. `client/scripts/emit-spa-fallback.mjs` copies
`dist/index.html` to `dist/404.html` after every build, so Pages serves the app
for any unmatched path, the router reads the URL, and deep links work.

**The status code stays 404.** The page renders correctly and a human sees the
right screen, but a crawler or uptime check reading the status will see 404 on
every route except the index. A host with real rewrites (Cloudflare Pages,
Netlify) returns 200. This is the price of deploying with no extra account and no
extra secret, and it is accepted knowingly.

Verified locally against a simulator that reproduces exactly this behaviour:

| Path | Status | Body |
| --- | --- | --- |
| `/upgrade-planner/` | 200 | app |
| `/upgrade-planner/assets/index-*.js` | 200 | bundle |
| `/upgrade-planner/upgrades` | **404** | **app (renders correctly)** |
| `/upgrade-planner/upgrades/7` | **404** | **app (renders correctly)** |

## API: Render — the click-path

`render.yaml` is committed, so Render configures the service from the repository.

1. Go to **https://render.com** and **Sign in with GitHub**. Free; it will not
   ask for a card.
2. Authorize Render to read `AndreiBautin/upgrade-planner`.
3. **New ➜ Blueprint**, pick the `upgrade-planner` repository, `master` branch.
   Render reads `render.yaml` and proposes a web service called
   `upgrade-planner-api` on the **Free** plan.
4. It will prompt for the one variable marked `sync: false`:

   | Key | Value |
   | --- | --- |
   | `ALLOWED_ORIGINS` | `https://andreibautin.github.io` |

   **Origin only — no path, no trailing slash.** CORS matches on origin, so even
   though the app is served under `/upgrade-planner/`, the origin is the bare
   `github.io` host. A trailing slash is normalised away by the parser, but a
   path is not a valid origin and would be dropped with a warning.
5. **Apply**. The first build takes roughly 5–10 minutes (it builds the .NET
   image from source).
6. Copy the service URL, e.g. `https://upgrade-planner-api.onrender.com`, and
   check it:

   ```bash
   curl https://upgrade-planner-api.onrender.com/health
   ```

   Expect `{"status":"ok","demoMode":true,"build":"...","utc":"..."}`.

7. Tell the frontend where the API is, by setting a **repository variable**
   (not a secret — it is a public URL):

   ```bash
   gh variable set VITE_API_BASE_URL --repo AndreiBautin/upgrade-planner --body "https://upgrade-planner-api.onrender.com"
   ```

   Or: repository **Settings ➜ Secrets and variables ➜ Actions ➜ Variables ➜
   New repository variable**.

8. Re-run the Pages deploy so the bundle is rebuilt with that origin baked in:

   ```bash
   gh workflow run "Deploy client to GitHub Pages" --repo AndreiBautin/upgrade-planner
   ```

Until step 7 is done, the deploy logs a warning and the live page shows
*"Could not reach the API"* — by design, rather than a silent blank screen.

## Environment variables

Every variable is documented in [`.env.example`](../.env.example). What each
environment actually sets:

### Render (the API)

| Key | Value | Set where |
| --- | --- | --- |
| `DEMO_MODE` | `true` | `render.yaml` |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `render.yaml` |
| `ALLOWED_ORIGINS` | `https://andreibautin.github.io` | Render dashboard (`sync: false`) |
| `BUILD_SHA` | *not set* — falls back to `RENDER_GIT_COMMIT`, which Render sets on every deploy | nothing to configure |
| `PORT` | assigned automatically | Render |

`ConnectionStrings__Default` is deliberately **not** set: demo mode ignores it and
uses `demo.db` regardless. See [DEMO_DATA.md](DEMO_DATA.md).

### GitHub Actions (the frontend)

| Key | Value | Set where |
| --- | --- | --- |
| `VITE_BASE_PATH` | `/upgrade-planner/` | workflow, from the repo name |
| `VITE_DEMO_MODE` | `true` | workflow |
| `VITE_BUILD_SHA` | `github.sha` | workflow |
| `VITE_API_BASE_URL` | Render URL | **repository variable** |

**No secrets exist in either environment.** Pages deploys with the built-in
token; the API has no credentials.

## Database and migrations

EF Core migrations are applied at startup by `db.Database.Migrate()`. On Render's
ephemeral disk every cold start begins with no file, so each start creates the
schema and then `SeedIfEmpty` writes the 13 fixture rows. There is no separate
migration step to run and no migration job to schedule.

CI proves this path works on every push: the *Migrate and seed from clean* step
starts the API with `DEMO_MODE=true` against an empty database, waits for
`/health`, and fails if the seed produced no rows.

## Resetting the demo data

- **Production:** redeploy, or simply wait — the instance sleeps after 15 minutes
  idle and reseeds on the next request. There is deliberately **no reset
  endpoint**: an unauthenticated destructive endpoint would be a real
  vulnerability, and guarding it would mean introducing the project's first
  secret to solve a problem the platform already solves.
- **Locally:** `DEMO_MODE=true dotnet run -- --reset-demo-data`

## How a deploy happens

```
push to master
   ├── CI                      build, test, lint, audit, secret-scan
   └── Deploy to Pages         build → upload artifact → deploy → smoke test
Render                         watches master, rebuilds the container itself
```

**CI and the Pages deploy run in parallel, not gated.** A failing test does not
currently block a deploy. That is a deliberate trade-off — this is a portfolio
demo where seeing the change live quickly is worth more than a gate — and it is
one line to change. In `deploy-pages.yml`, give the `build` job:

```yaml
  build:
    needs: [ci]        # gate the deploy on CI passing
```

after adding the CI jobs to the same workflow file, or convert `ci.yml` into a
reusable workflow called by the deploy. Left ungated on purpose; the smoke test
below is what actually catches a broken publish.

### The smoke test

A green `deploy-pages` step means an upload succeeded. The extra step means the
site *answered*: it fetches the live URL, retries for up to 100 seconds while
Pages propagates, and fails unless the response body contains
`<title>Upgrade Planner</title>` **and** a reference to a JS bundle. A Pages error
page would satisfy neither.

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| Page loads but shows *"Could not reach the API"* | `VITE_API_BASE_URL` not set, or the Render service is asleep | Set the repository variable and re-run the deploy; if set, wait ~1 min for the cold start |
| First load takes ~60 seconds | Render free instance spinning up from idle | Expected. The UI shows a cold-start notice rather than a dead spinner |
| Browser console: *blocked by CORS policy* | `ALLOWED_ORIGINS` missing, wrong, or includes a path | Set it to `https://andreibautin.github.io` — origin only. Check the Render logs for the startup warning |
| Every asset 404s | `VITE_BASE_PATH` did not reach the build | CI asserts `/upgrade-planner/assets/` appears in `dist/index.html`; check that step |
| Deep links 404 with a *Pages* error page (not the app) | `dist/404.html` missing | The build emits it; CI asserts it exists |
| Site shows an old version | Pages CDN cache, or the deploy did not run | Check `gh run list`; hard-reload |
| Demo data reverted | Cold start reseeded | Expected — that is the reset mechanism |
| Render build fails on `dotnet restore` | Build context wrong | Context is `./server/UpgradePlanner.Api`; the Dockerfile copies only the csproj first |
| `429 Too Many Requests` | Rate limit: 120/min per client | Wait a minute. The UI says so in plain language |

## Free-tier limits, with actual headroom

| Limit | Allowance | Realistic use | Headroom |
| --- | --- | --- | --- |
| Pages bandwidth | 100 GB/month (soft) | ~80 KB gzipped per visit | ~1.2 M visits |
| Pages builds | 10/hour (soft) | a few per day | ample |
| Pages site size | 1 GB | ~260 KB | ~4000× |
| Render instance hours | 750/month | one service, sleeps when idle | one service fits with room to spare |
| Render bandwidth | 100 GB/month | JSON only, a few KB per call | effectively unlimited here |
| Render build minutes | 500/month | ~5 min per deploy | ~100 deploys/month |

Neither provider can bill this account: Render has no payment method on file and
suspends rather than charges, and Pages has no paid dimension on a free personal
account.
