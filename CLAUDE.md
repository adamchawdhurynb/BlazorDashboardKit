# BlazorDashboardKit — project guide

Host-agnostic Blazor RCL: a draggable/resizable GridStack dashboard of pluggable widgets. Consumer supplies persistence (`IDashboardStore`) and access policy (`IWidgetAccessControl`). Library = `src/BlazorDashboardKit`; demo = `samples/SampleApp` (Server `/`, WASM `/wasm`, static SSR `/ssr`); tests = `tests/BlazorDashboardKit.Tests` (bUnit + xUnit).

## Build / run / test

- Test: `dotnet test tests/BlazorDashboardKit.Tests/BlazorDashboardKit.Tests.csproj`
- Run sample: `dotnet run --launch-profile http --no-build` in `samples/SampleApp/SampleApp` → http://localhost:5287
- **Editing RCL static assets** (`src/BlazorDashboardKit/wwwroot/*.css|*.js`): plain incremental build serves STALE assets. Use `dotnet build samples/SampleApp/SampleApp/SampleApp.csproj -c Debug -t:Rebuild`, then `curl` the served `_content/...` file to confirm; browsers also cache it (cache-bust when verifying).
- bUnit constraint: never `JSInterop.SetupModule` (documented VSTest-host hang). To assert JS interop, inject a fake `IJSRuntime` into `DashboardJsInterop`.

## E2E smoke test checklist

Run after any change to the dashboard host, widgets, interop, or CSS. Exercise on the **Server** page at minimum; spot-check WASM and SSR for render-mode parity. **Keep this list current: add a case when a feature is added; revise a case when behavior changes.**

- [ ] All pages load without console errors: SampleApp `/` (Server), `/wasm`, `/ssr`; StandaloneWasm `/` + `/usage`.
- [ ] No kit asset tags in the host page; before any dashboard renders, `GridStack`/`dashboard.css`/`gridstack.min.css` are ABSENT; once a dashboard becomes interactive the kit injects them lazily (verify a no-dashboard page never loads them).
- [ ] **Add Widget** dropdown opens with NO Bootstrap JS loaded; widgets grouped by category; closes on select.
- [ ] Adding a widget renders it with real size/position (GridStack instance present; `bdk-grid-static` cleared; item rect non-zero, a grid cell not full-width).
- [ ] **Actions** dropdown opens; Export downloads JSON; Import loads a JSON file (lock state preserved round-trip); menu closes after each.
- [ ] Drag a widget (edit mode) and resize it; positions/sizes persist after Done + reload.
- [ ] Edit mode compacts live: dropping a widget below a gap, or shrinking/removing one above others, slides widgets up, animated, right after the drop/resize/remove (not only on Done); Done then moves nothing.
- [ ] Remove (✕) needs a 2-step confirm (✓/↩); confirming deletes the widget and the grid re-lays-out. Cancel aborts.
- [ ] Delete dashboard needs a 2-step confirm (Confirm delete / Cancel).
- [ ] A widget's own controls (e.g. Notes "Edit") are clickable — NOT overlapped/hijacked by the edit header's ✕.
- [ ] Configure (gear) opens the panel rendering the widget's **config component**; editing + Save persists and reflects in the widget; original config untouched on Cancel. Widget with null `ConfigComponentType` shows "no configuration" + no Save.
- [ ] A widget with `RequiresConfiguration` opens its config before the first save.
- [ ] Debug label hidden by default; appears only with `ShowDebugInfo="true"`.
- [ ] Edit ⇄ Done toggles edit affordances; Done persists; reload shows the persisted dashboard.
- [ ] Rename the dashboard title; it persists across reload.
- [ ] Multiple dashboards: "+ Dashboard" adds one; selector switches (grid rebuilds); Delete removes (last one protected); active selection persists.
- [ ] `/ssr` renders read-only (no edit affordances) with a usable CSS-fallback layout — no 0×0 collapse, no crash; `AlwaysInteractive` widgets render read-only there too.
- [ ] StandaloneWasm: an interactive widget (Counter) increments and the value survives reload (config-persisted).
- [ ] Lock toggle (🔒/🔓 in the edit header) pins a widget: it can't be dragged/resized; state persists across reload.
- [ ] Duplicate (⧉) adds an independent copy (own id, deep-cloned config, unlocked, auto-placed); editing one doesn't affect the other.
- [ ] Resizing a widget honors the descriptor's Min/Max column/row size (gs-min/max-* emitted).
- [ ] `GridOptions` (e.g. Columns=6) changes the grid layout; defaults match built-in behaviour when omitted.
- [ ] Kit does NOT auto-switch on OS dark (a light host page keeps a light widget card); dark only when `--bdk-*` tokens are set.
- [ ] Widget header actions (grip, lock, configure ⚙, duplicate ⧉, remove ✕) are ALL visible without any icon font.
- [ ] Override seams work: a `…Template` and a `…Component` each replace the kit default for header/empty/unavailable/error/picker/config-shell; precedence Template > Component > default; contexts' action callbacks flow.
- [ ] Picker is filtered by `IWidgetAccessControl` when one is registered.
- [ ] Publish: a `<Version>` bump on `main` triggers `publish.yml` → NuGet + GitHub Packages + `v<version>` tag (no-op if the tag exists). `pages.yml` deploys the StandaloneWasm demo on push to `main`.

## Version bumps & releasing

- The version is `<Version>` in `src/BlazorDashboardKit/BlazorDashboardKit.csproj`. Publishing is automatic: `publish.yml` builds, pushes to NuGet.org + GitHub Packages, and tags `v<version>` on push to `main` — but only when no `v<version>` tag exists yet (tag-based idempotency). So **bumping `<Version>` on `main` is what ships a release.**
- When asked to bump the version: find the diff since the last release — the last `v<version>` tag (`git describe --tags --abbrev=0`) or the last "bump"/version-change commit in the csproj.
- ALWAYS update root `CHANGELOG.md` in the same change — never bump `<Version>` without a changelog entry.
  - The file follows Keep a Changelog with an `## [Unreleased]` section: on bump, rename `[Unreleased]` to `## [x.y.z] - YYYY-MM-DD` (today's date) and open a fresh empty `[Unreleased]`.
  - Summarize since the last release under `### Added` / `### Fixed` / `### Changed` (call out breaking changes explicitly) and dependency updates.
- No submodules in this repo — there is no SDK/submodule diff to fold in (unlike sibling BTCPay-plugin repos).

## Conventions

- Bugs/behavior changes: failing test first (TDD), root-cause before fixing (no symptom patches).
- No hardcoded BTCPay theme tokens in the library — use CSS custom properties with sane fallbacks so it is themable (and reusable in BTCPay later); sample supplies its own theme.
