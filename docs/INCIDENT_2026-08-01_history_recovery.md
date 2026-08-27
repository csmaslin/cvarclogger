# 2026-08-01 incident: lost history, split repos, and what still needs doing

Read this before starting CvarcLogger work after today. It exists so we don't
redo work that's already done, and don't forget work that's genuinely still
needed. Cross-check this list before implementing anything that touches
DXCC entities, contest/SKCC fields, or database migrations.

## TL;DR

- CvarcLogger's `master` had quietly lost two real releases (v1.44, v1.45)
  to an accidental `git reset --hard` on 2026-07-30. Recovered today.
- Recovering it undid *other* real work that had been built on top of the
  lost state without knowing it was lost (SOTA/POTA `StationProfile`
  fields, a schema-repair fix, a backup-safety fix). Some of that has been
  reapplied to the recovered line; some has **not** and is listed below.
- CvarcCellLog (the Android app) no longer shares source with this repo at
  all, as of today. It forked its own copy. **Changes made here do not
  reach CvarcCellLog automatically anymore**, and vice versa.
- One real, unresolved bug remains in this repo: the DXCC seed file throws
  a duplicate-entity-code error when seeding a fresh database. Diagnostic
  test is written but hasn't been run yet.

## What actually happened (so the next person doesn't have to re-derive this)

1. **2026-07-24 to 2026-07-30**: normal development. v1.42 released, then a
   QSO-edit fix, then (2026-07-29/30) **v1.44** (Log Mode picker, Sequence #
   field, ARRL contest/SKCC support) and **v1.45** (DXCC seed expanded to
   the full official ADIF list). All real, all released, all working.
2. **2026-07-30, ~06:14am**: two `git reset` commands walked `master`
   backward past v1.45, the DXCC seed expansion, and v1.44 — three real
   commits — landing back at the pre-v1.44 QSO-edit commit. Cause unknown;
   nothing in the reflog explains why. `master`'s pointer stayed at that
   rolled-back point from then on.
3. **2026-07-30 through 2026-08-01**: new work continued from that
   rolled-back point, **unaware v1.44/v1.45 had ever existed**. This
   included redoing a smaller version of the contest/SKCC migration (this
   time with a bug — see below), and, separately, adding SOTA/POTA GPS
   fields to `StationProfile` for the CvarcCellLog app.
4. **2026-08-01**: while testing CvarcCellLog's DXCC page against a real
   5,721-record ADIF import, a `SQLite Error: no such column: s.SkccNr`
   surfaced creating a brand-new database. Root-caused to the redone
   contest/SKCC migration from step 3 being **silently gutted to a no-op**
   by an even earlier same-day mistake (a commit that assumed those
   columns were "already in the schema" when they weren't). Fixed with a
   defensive `SchemaRepairRunner` that checks the live schema and patches
   any of the 6 known-missing columns.
5. Investigating *why* the redone migration existed at all led to
   discovering the reflog evidence from step 2 — i.e. v1.44/v1.45 were
   never actually gone, just orphaned. **User decision: treat v1.45 as the
   true history.** `master` was moved there (clean fast-forward, verified
   safe first; nothing was destroyed — the abandoned line is preserved on
   branch `schema-repair-2026-08-01`).
6. Moving `master` to v1.45 also **un-did** the SOTA/POTA `StationProfile`
   fields and the backup-safety fix from step 3/later, since those were
   built on the now-abandoned line, not on v1.45. This broke CvarcCellLog's
   build (it referenced those fields) with zero changes made in the
   CvarcCellLog repo itself.
7. **Consequence, user decision**: stop sharing source between the two
   repos. CvarcCellLog forked its own permanent copy of `CvarcLogger.Core`/
   `CvarcLogger.Data` (from the last known-good working state) into its own
   repo. See `project_cvarccelllog_split_from_cvarclogger` memory. The two
   copies **will diverge** unless someone deliberately keeps them in sync
   — that's an accepted tradeoff, not an oversight.

## Current state (as of 2026-08-01 end of session)

- `master` = the recovered v1.45 commit (`3252167`), **plus** the
  `SchemaRepairRunner` re-added on top (commit exists locally, defensive
  only — this line's own migration was never actually broken).
- Tag `v1.45-recovered` marks the pure recovery point, before
  SchemaRepairRunner was reapplied.
- Branch `schema-repair-2026-08-01` preserves the abandoned line's tip
  (commit `3e7f7e7`) in full, in case anything else on it turns out to be
  needed. Nothing was deleted.
- Test suite: **153/158 passing.** The 5 failures are one real bug (below),
  not a regression from today's recovery work.

## Real, unresolved bug: DXCC seed duplicate-key error

`dotnet test` fails 5 tests with:
```
DbUpdateException -> InvalidOperationException: The instance of entity
type 'DxccEntity' cannot be tracked because another instance with the
same key value for {'EntityCode'} is already being tracked.
```
Failing: `SeedIfEmpty_PopulatesDxccEntitiesOnFreshDatabase`,
`SeedIfEmpty_IsIdempotent`, `ComputeDxccProgress_FiltersByBandWhenRequested`,
`ComputeDxccProgress_CountsWorkedAndConfirmedEntities`,
`ComputeWasProgress_TreatsMainlandAlaskaHawaiiAsOneCombinedAward` (last
four cascade from the same seeding failure via shared test fixtures).

**Ruled out:**
- Stale build cache (full clean + delete all `obj`/`bin` + rebuild: same
  failures).
- Duplicate `entityCode` in the raw JSON parsed independently with Python:
  `src/CvarcLogger.Data/Seed/dxcc_prefixes.json` has **338 entries, 338
  distinct codes, zero duplicates**. The v1.45 commit message claims "520
  entities" — that number does not match what's actually in the file.
  Worth understanding why (wrong count in the message? file changed since?
  520 was the source cty.csv's raw line count before filtering, not the
  entity count?), but not yet explained.

**Not yet found:** why .NET's own seeding path throws a same-key conflict
if the raw file has no duplicates. Possible angles not yet tried:
- The conflict may be about `PrefixMapping` foreign-key tracking, not
  `DxccEntity` itself, with EF's error message pointing at the wrong type.
- A test-fixture reuse issue (does a test class share one `DbContext`
  across tests without resetting the change tracker?).

**Next step, ready to run:** `tests/CvarcLogger.Tests/ZZDiagnosticSeedTest.cs`
is already written (uncommitted) — it reproduces `SeedRunner`'s exact load
path via reflection + `System.Text.Json` (the same parser production
uses, not Python) and reports total entries, any duplicate `EntityCode`
groups, and any prefix claimed by more than one entity. Run it first:
```
dotnet test tests\CvarcLogger.Tests\CvarcLogger.Tests.csproj --filter "FullyQualifiedName~ZZDiagnosticSeedTest"
```
Delete the file once the real bug is found and fixed — it's diagnostic
scaffolding, not real test coverage.

## Fixes made today that need to be REAPPLIED to this repo (lost in the reset)

These were real, tested fixes built on the abandoned line. They are **not**
present on the recovered v1.45 `master`. Each is small and known-good —
don't rediscover the reasoning, just reapply it:

1. **Backup-before-copy WAL checkpoint** (`src/CvarcLogger.App/App.xaml.cs`,
   `BackupDatabase()`/`OnExit`). SQLite runs in WAL journal mode; a raw
   `File.Copy` of the `.db` file can silently miss recent commits still
   sitting in the `.db-wal` sidecar. Fix: run
   `db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);")`
   on the live `DbContext` immediately before the copy. Confirmed via
   `git show schema-repair-2026-08-01:src/CvarcLogger.App/App.xaml.cs` for
   the exact prior diff if needed. (CvarcCellLog's own equivalent, in its
   now-forked copy, already has this — no action needed there.)

2. **SOTA/POTA `StationProfile` fields** — `MySotaRef`, `MyPotaRef`,
   `TxPowerWatts`, `NearestSummit`, `NearestPark`, `Latitude`, `Longitude`,
   plus the two migrations that added them (`20260731153644_AddStationProfileGps`,
   `20260731195738_AddStationProfileStaticFields`). These exist in
   CvarcCellLog's forked copy already (that's the working, tested version)
   and are recoverable from `schema-repair-2026-08-01` if this desktop app
   should also get them (check with the user first — this may be a
   CvarcCellLog-only feature that was never meant for the WPF app; don't
   assume it should be ported without asking).

## General lessons (apply to both programs)

- **XML/XAML comments**: a literal `--` anywhere inside a `<!-- ... -->`
  comment is a hard build error (`MC3000`/`MSB4025` depending on context).
  Write around it from the start — use a comma or "and" instead of a
  double-hyphen, don't rely on catching it after a failed build. Hit this
  three separate times today alone.
- **`git reset` on a branch you're actively building on is dangerous and
  silent.** It doesn't delete commits (they stay reachable via reflog/any
  branch still pointing at them), but it *does* silently detach real work
  from `master` with no error, no warning, and no obvious symptom until
  something built on top of the missing state breaks — possibly days
  later, possibly in a completely different program. Before any reset,
  check `git log <target>..<current>` for what would become unreachable,
  and prefer creating a safety branch at the current tip first.
- **A gutted/no-op migration is worse than a missing one.** EF Core's
  `__EFMigrationsHistory` marks a migration "applied" the moment its
  (possibly empty) `Up()` runs, regardless of whether it actually did
  anything. A migration edited to do nothing, after some databases already
  ran its real version, creates two silently-incompatible database shapes
  under the same migration name with no way to tell them apart from the
  history table alone. If a migration turns out to be redundant, prefer
  leaving it alone (even as dead weight) over gutting it — or if it must
  be neutralized, add a *new* migration that's provably safe on both old
  and new databases (schema-check-then-patch, as `SchemaRepairRunner`
  does now) rather than editing the old one in place.
- **SQLite WAL mode**: any tool that copies a `.db` file directly (backup
  features, manual `adb pull`, etc.) must either checkpoint first
  (`PRAGMA wal_checkpoint(TRUNCATE)`) or also copy the `.db-wal`/`.db-shm`
  sidecars alongside it. A `.db` file alone, copied while the app that
  wrote it is still running, can be missing recent data with no error.
