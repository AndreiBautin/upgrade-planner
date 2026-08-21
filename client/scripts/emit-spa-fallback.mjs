// Emits dist/404.html as a copy of dist/index.html.
//
// GitHub Pages serves static files only: a request for /upgrade-planner/upgrades/7
// matches no file on disk, so Pages answers with its 404 document. Making that
// document the app itself means the SPA boots, the router reads the URL, and the
// deep link works.
//
// Honest limitation: the HTTP status stays 404. The page renders correctly and a
// human sees the right screen, but a crawler or an uptime check reading the
// status code will see a 404 for every route except the index. A host with real
// rewrite rules (Cloudflare Pages, Netlify) returns 200 instead; this is the
// trade-off accepted in exchange for deploying with no extra account and no
// extra secret. See docs/DEPLOYMENT.md.
import { copyFile, access } from 'node:fs/promises'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const distDir = join(dirname(fileURLToPath(import.meta.url)), '..', 'dist')
const source = join(distDir, 'index.html')
const target = join(distDir, '404.html')

try {
  await access(source)
} catch {
  console.error(`emit-spa-fallback: ${source} does not exist. Run the build first.`)
  process.exit(1)
}

await copyFile(source, target)
console.log('emit-spa-fallback: wrote dist/404.html')
