# AI vs me

For the bonus stage I built the same service **twice** and compared.

- **"AI"** - a fresh agent given only a short, brief-level prompt (`PROMPT.md`): **no** repo,
  **no** environment info, and no iteration on real failures. It wrote the whole service in one
  pass, started it once (a single `app.log` is all it left behind), and moved on. Its output is
  preserved verbatim in `ai-version/` (never edited afterwards).
- **"Me"** - the implementation in `src/ReportApi/`, built with full tooling: I could build it,
  run it, curl it, read the failures, and iterate, and I committed it stage by stage.

**This is not a level playing field and the comparison should be read with that in mind.** The
"AI" side never saw this repository or this machine, so it could not know the browser path, the
port, or how `curl` actually sends a body - and it had no loop to catch what it could not foresee.
Its code is genuinely good *given that*. The interesting part is not "who wrote cleaner code" but
*where writing blind and testing in a loop diverge*.

Both prompts asked for the same pipeline: SQLite `orders` -> aggregation -> HTML -> headless
print -> PDF stored on disk -> row in `reports` -> serve by link, with a once-per-day duplicate check.

## Where we independently converged (the same right answers)

- The whole shape of the pipeline, and storing the file on disk while handing back a **link**
  (no bytes in JSON).
- The exact print-CSS tricks: `thead { display: table-header-group; }` so the header repeats, and
  `page-break-inside: avoid` / `break-inside: avoid` on rows so a row is never cut.
- A4 + `PrintBackground`.
- `201` + `{id, file}` on create, `200` + same id on a same-day duplicate, and `404` on an unknown id
  or a missing file.
- **The same blind spot:** both of us initially reached for `ReadFromJsonAsync` to read the
  `{"force":true}` body.

## Where the tested loop pulled ahead

1. **The `force` flag - the sharpest example.** Both versions were written with
   `ctx.Request.ReadFromJsonAsync<JsonElement>()` inside a `try/catch`. But `curl -d '{"force":true}'`
   sends `Content-Type: application/x-www-form-urlencoded`, so that call **throws**.
   The AI shipped this **blind**, and its `force` silently does nothing - the exact command the
   brief implies to exercise the override returns the same-day report instead.
   I hit the identical wall, **tested it**, watched `force` get ignored, and fixed it by reading the
   raw request body and parsing it regardless of content-type. Same blind spot; one stayed a silent
   bug, the other became a two-line fix. That difference *is* the loop.

2. **Portability.** The AI hardcodes `ExecutablePath = "C:\Program Files (x86)\...\msedge.exe"` **and**
   `Channel = "msedge"`, and calls `app.Run("http://localhost:5288")`. On any machine without Edge at
   that exact path it throws, and the port is not configurable. Mine launches via the `msedge`
   channel with an Edge `--headless --print-to-pdf` **fallback**, and the URL comes from
   `--urls` / launchSettings.

3. **Paths.** The AI keeps `report.db` and `reports/` **relative to the current working directory**,
   and stores the relative path in the DB - so `dotnet run` from a different directory targets a
   different database, and the stored path is ambiguous. Mine resolves an absolute repo root
   (walking up to `.git`) and stores absolute paths.

4. **Double render.** The AI renders **twice per POST** - once to `reports/test.pdf` and once to
   `reports/{id}.pdf` - so every request does the most expensive step (browser + print) two times.
   Mine renders once.

5. **Seeding semantics.** The AI seeds with `INSERT OR IGNORE` and a `need = 200 - count` top-up.
   "Run it twice, no duplicates" happens to hold, but "exactly 200" depends on id-collision luck and
   silently does nothing if the table already holds more than 200. Mine deletes then inserts exactly
   200, every time.

6. **Missing surface.** No `/report-data` JSON endpoint (the aggregation step is only visible inside
   the PDF), no README, no `.gitignore`, no stage/commit structure. Mine exposes `/report-data`, ships
   a README with the SQL + proof + screenshot, and is committed in stages.

7. **Completeness vs the brief's later stages.** The AI's prompt only covered stages 0-5, so it has no
   window parameter, report listing, background job, cron, or email. That is fair to the AI - but it
   means "me" is the only one that reaches stages 6-7 and the stretches.

## Where the AI was as good, or arguably better

- **Legibility.** The entire service fits in one 354-line file you can read top to bottom. There is no
  hunting across files. For a service this small that is a real virtue, not a defect.
- **A legitimate alternative idempotency idea.** `INSERT OR IGNORE` for orders is a reasonable
  "top-up, don't duplicate" strategy - I chose delete-and-insert for determinism, but the AI's choice
  is defensible.
- **No over-engineering.** It did exactly what the prompt said and nothing more.

## Bottom line

Given the AI could not run a single line of its own output, its code is impressive and the core
pipeline is correct. But the decisive differences were all things you can only find by **running** it:
a flag that silently no-ops, a hardcoded browser path, a port you cannot move, work done twice, and a
seed that only *usually* lands on 200. That is the honest takeaway - not "the AI writes worse code,"
but *a blind one-shot and a tested loop converge on the happy path and diverge exactly where the
environment punishes assumptions.*
