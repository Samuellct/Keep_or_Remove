# Keep or Remove - Development Plan

Companion to `CLAUDE.md`. This is the "where we are going" document: the phased build order, the
one-time deployment setup, and the acceptance checklist derived from `Synthèse.md` §15.

Plugin identity (fixed at project start):

| Field | Value |
|---|---|
| Display name | `Keep or Remove` |
| GitHub repo | `Samuellct/Keep_or_Remove` (public) |
| Plugin GUID | `dbcf4f1f-bc0c-4681-b79a-cbd2294b2538` |
| Namespace / assembly | `Jellyfin.Plugin.KeepOrRemove` |
| API route prefix | `/KeepOrRemove` |
| CSS prefix | `kor-` |
| Jellyfin target | `10.11.11` only (`targetAbi 10.11.11.0`, `Jellyfin.*` NuGet pinned `10.11.11`) |
| Data file | `{DataPath}/Jellyfin.Plugin.KeepOrRemove/votes.json` |
| Test container | `keeporremove-test`, Jellyfin `10.11.11`, port `8098` |
| Manifest URL | `https://raw.githubusercontent.com/Samuellct/Keep_or_Remove/main/manifest.json` |

---

## Reused from JellyUX-Homepage (do not rewrite)

Copied and renamed (`JuxHomepage` -> `KeepOrRemove`, `jux-` -> `kor-`, GUID swapped):

- `Inject/FileTransformationDetector.cs` - reflection bridge to the FileTransformation plugin. **Verbatim** except namespace.
- `Inject/StartupService.cs` - trimmed to a single `index.html` transformation registration (no loadSections, no home-html chunk).
- `Controllers/UserAccessGuard.cs` - IDOR guard for user-scoped endpoints. Verbatim except namespace.
- `IO/IFileSystem.cs` + `IO/FileSystem.cs` - file-system abstraction for testable disk access.
- `PluginServiceRegistrator.cs` - DI registration skeleton.
- `.github/workflows/ci.yml` - drop the `jellyfin-web-drift` job's sibling workflow; keep build/test/release.
- `.github/workflows/` - **omit** `docs.yml` and `jellyfin-web-drift.yml`.
- `.github/scripts/set-plugin-version.py` + `update-manifest.py` - path/name constants updated.
- `.releaserc.json`, `package.json`, `vitest.config.js`, `.gitignore` (deny-all), `dependabot.yml`.
- `docker/docker-compose.yml` + `docker/scripts/deploy-plugin.ps1` - container name + port changed.
- `CONTRIBUTING.md`, `LICENSE.md` (GPL-3.0), README structure.
- `.claude/settings.json` - the same allow-list of `dotnet` / `rtk git` / `docker` / context7 permissions.

Not reused: widget system, TMDb/Wikidata clients, per-user config store, scheduled tasks, localization
framework (Keep or Remove ships a tiny `fr.json`/`en.json` pair only if the UI needs it - otherwise
English-only strings inline).

---

## Build phases

Each phase = one or more Conventional Commits. Do not start a phase before the previous one builds and its tests pass.

### Phase 0 - Scaffold (this session)

- Solution + two projects (`Jellyfin.Plugin.KeepOrRemove`, `.Tests`).
- `Plugin.cs` (GUID, name, `IHasWebPages` config page), `PluginConfiguration.cs` (just
  `StartupWarning` + `Enabled`), `PluginServiceRegistrator.cs`.
- Copied infra files (see above). `build.yaml`, `manifest.json` (empty `versions: []`), CI/release
  workflows, scripts, docker, `.gitignore`, README, LICENSE, CONTRIBUTING.
- `git init`, first commit, create `Samuellct/Keep_or_Remove`, push `main`.
- **Acceptance**: `dotnet build -c Release` green; CI green on first push.

### Phase 1 - Vote storage & service (backend, no HTTP yet)

- `Models/Vote.cs` (`enum VoteChoice { Keep, Remove }`, `record VoteRecord`), `Models/VotesFile.cs`.
- `Storage/VoteStore.cs` - load/save `votes.json`, `ReaderWriterLockSlim`, atomic temp-file+rename,
  corrupt-file handling (log + treat as empty, back up the bad file next to it).
- `Services/VoteService.cs`:
  - `ResolveVoteTargetId(Guid itemId)` -> parent Series id for Episode/Season, else the id itself,
    `Guid.Empty` if unresolvable. Uses `ILibraryManager`.
  - `UpsertVote(userId, itemId, choice)` - resolve target, replace-or-insert, stamp `UpdatedAt`.
  - `GetVote(userId, itemId)` -> `VoteChoice?`.
  - `DeleteVote(userId, itemId)`.
  - `GetResults(sort, typeFilter)` -> `IReadOnlyList<VoteAggregate>` (item id, name, type, keep, remove, total).
  - `PurgeOrphans()` -> count removed.
- **Tests** (`Synthèse.md` §15): create / modify (replaces, no dup) / delete / user isolation /
  aggregation counts / sort by keep desc / sort by remove desc / type filter / episode->series
  resolution / no separate season or episode vote row / orphan purge.
- **Acceptance**: `dotnet test` green, coverage of every §15 bullet.

### Phase 2 - HTTP API

- `Controllers/VoteController.cs` (`[Route("KeepOrRemove")]`):
  - `GET  /KeepOrRemove/vote?itemId=` -> `{ vote: "KEEP" | "REMOVE" | null }`, current user only.
  - `PUT  /KeepOrRemove/vote` body `{ itemId, vote }` -> 204.
  - `DELETE /KeepOrRemove/vote?itemId=` -> 204.
  - `GET  /KeepOrRemove/admin/results?sort=keep|remove|total&type=all|movie|series` -> table rows. `RequiresElevation`.
  - `POST /KeepOrRemove/admin/purge` -> `{ removed: n }`. `RequiresElevation`.
  - `GET  /KeepOrRemove/{file}` -> embedded static asset (`kor-vote.js`, `kor-vote.css`, `config.js`).
- User identity from `IAuthorizationContext.GetAuthorizationInfo(HttpContext)`; never from query.
  Non-elevated callers act only as themselves.
- 503 (not 500, not a crash) when `VoteStore` is unavailable.
- **Tests**: auth matrix (anon rejected, user can only touch own vote, admin endpoints reject
  non-admin), payload validation, unknown itemId handling.
- **Acceptance**: `dotnet test` green; manual `curl` against `keeporremove-test` for each route.

### Phase 3 - FileTransformation injection

- `Inject/FileTransformationDetector.cs` + `Inject/TransformationPatches.cs` with a single
  `IndexHtml(PatchRequestPayload)` that inserts `<link .../KeepOrRemove/kor-vote.css?v=VERSION>` and
  `<script .../KeepOrRemove/kor-vote.js?v=VERSION defer>`.
- `Inject/StartupService.cs` registers it once; if FileTransformation missing -> log error, set
  `Configuration.StartupWarning`, no-op.
- **Tests**: `IndexHtml` splices both tags; missing markers -> content returned unchanged.
- **Acceptance**: deploy to container with FileTransformation installed; view page source, confirm
  the two tags and the `?v=` matching the built assembly version.

### Phase 4 - Front-end vote buttons

- `Web/kor-vote.js` (no bundler, exports pure helpers for vitest):
  - `_detailItemId(hash)`, `_isDetailPage(hash)`, `_isSupportedType(item)` (Movie/Series/Season/Episode).
  - single debounced `viewshow` / hashchange hook; no polling, no broad MutationObserver.
  - fetch item via Jellyfin's `ApiClient`; if Season/Episode use `SeriesId`.
  - one `GET /KeepOrRemove/vote` per target, cached in a `Map` for the session.
  - render two buttons into the detail action row using an existing anchor; **skip silently** if the
    anchor is absent. Active state reflects current vote. Click -> `PUT` (or `DELETE` to unset if
    re-clicking the active one - decide during impl, spec allows either), update UI optimistically,
    revert on error.
  - `[KeepOrRemove]`-prefixed `console.warn` on any failure; never throw.
- `Web/kor-vote.css` - style with native Jellyfin vars, `kor-` prefixed.
- **Tests**: vitest/jsdom for every `_helper`.
- **Acceptance** (`Synthèse.md` §15 Films/Séries): vote on a movie, change it, reload -> persisted;
  vote from a Season and an Episode page -> single row on the parent series; two browsers / two users
  -> independent votes.

### Phase 5 - Admin panel

- `Web/config.html` + `Web/config.js` - Jellyfin plugin config page conventions (from Homepage).
  Warning banner bound to `StartupWarning`. Table `Media | 👍 | 👎 | Total`, sort buttons (keep /
  remove / total), type filter (All / Movies / Series), "Purge orphan votes" button with a count toast.
- **Tests**: vitest for the row-render / sort-label / filter helpers.
- **Acceptance** (`Synthèse.md` §15 Administration): counts correct, both sorts correct, filter
  correct, no zero-vote media listed.

### Phase 6 - Hardening & release

- Degradation checks: rename `votes.json` to something unreadable -> API 503, Jellyfin unaffected,
  media pages still load (buttons just absent). Remove FileTransformation -> config page shows the
  warning, server fine.
- Compatibility pass on `keeporremove-test` with Intro Skipper + Media Bar + File Transformation +
  Jellyfin Enhanced installed alongside.
- README install section, screenshots, `docs/icon.png`.
- First `feat:` commit that triggers semantic-release -> `v1.0.0`, manifest published.
- **Acceptance**: fresh Jellyfin, add manifest URL, install from catalog, restart, vote, see results,
  uninstall, delete `Jellyfin.Plugin.KeepOrRemove/` data dir -> server byte-identical to before.

---

## Deployment setup (one-time, Phase 0)

1. **Create the repo**
   ```bash
   gh repo create Samuellct/Keep_or_Remove --public \
     --description "Temporary Jellyfin plugin: users vote keep/remove on library media to guide manual rotation."
   git init -b main && git add -A && git commit -m "chore: scaffold Keep or Remove plugin"
   git remote add origin git@github.com:Samuellct/Keep_or_Remove.git
   git push -u origin main
   ```

2. **`build.yaml`** (consumed by `jprm`)
   ```yaml
   name: "Keep or Remove"
   guid: "dbcf4f1f-bc0c-4681-b79a-cbd2294b2538"
   version: "0.0.0.0"
   targetAbi: "10.11.11.0"
   framework: "net9.0"
   owner: "Samuellct"
   overview: "Vote keep or remove on library media to help the admin decide what stays."
   description: "A temporary decision-support plugin: each user votes keep or remove on movies and series; the admin gets an aggregated read-only view for manual library rotation. Never modifies the library."
   category: "General"
   imageUrl: "https://raw.githubusercontent.com/Samuellct/Keep_or_Remove/main/docs/icon.png"
   artifacts:
     - "Jellyfin.Plugin.KeepOrRemove.dll"
   changelog: ""
   ```

3. **`manifest.json`** starts as `[ { ...metadata..., "versions": [] } ]`. `update-manifest.py` fills
   it on each release.

4. **CI workflow** (`.github/workflows/ci.yml`) - from Homepage, unchanged logic:
   - job `build-test`: setup-dotnet 9, `dotnet restore` / `build -c Release` / `test`, setup-node,
     `npm ci`, `npm test`.
   - job `release` (needs `build-test`): setup dotnet/python/node, `pip install jprm`,
     `npm install`, `npx semantic-release`. `permissions: contents: write`.
   - Trigger: `push` to `main` + `workflow_dispatch`.

5. **`.releaserc.json`** - from Homepage. `prepareCmd`:
   ```
   mkdir -p artifacts && python3 .github/scripts/set-plugin-version.py ${nextRelease.version} \
     && jprm plugin build . --version=${nextRelease.version}.0 --output=./artifacts \
     && python3 .github/scripts/update-manifest.py ${nextRelease.version}
   ```
   `set-plugin-version.py` `CSPROJ_PATH` -> `src/Jellyfin.Plugin.KeepOrRemove/Jellyfin.Plugin.KeepOrRemove.csproj`.

6. **Branch protection** (optional, matches Homepage practice): require `build-test` to pass on `main`.

7. **Dependabot** (`.github/dependabot.yml`) - npm + nuget + github-actions, weekly.

---

## Acceptance checklist (`Synthèse.md` §15, condensed)

- [ ] Vote create / modify (in-place, no duplicate) / delete
- [ ] `(UserId, ItemId)` uniqueness enforced
- [ ] Votes isolated between users
- [ ] Movie: vote, modify, aggregate
- [ ] Series: vote at series level; no season vote; no episode vote; child context resolves to series
- [ ] Admin: 👍 count, 👎 count, sort 👍 desc, sort 👎 desc, filter movies/series, zero-vote media absent
- [ ] Degradation: vote-fetch error does not block media view; injection error does not break Jellyfin Web; DB unavailable -> clean 503
- [ ] Runs alongside Intro Skipper / Media Bar / File Transformation / Jellyfin Enhanced
- [ ] Uninstall + delete data dir leaves the server unchanged
