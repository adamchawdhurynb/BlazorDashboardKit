# BlazorDashboardKit for the host app

The host app's dashboard (`/home`) is built on [BlazorDashboardKit](https://github.com/Kukks/BlazorDashboardKit),
an open-source library by Andrew Camilleri ("Kukks"). The host app needs a few changes the library
doesn't offer, so it uses this fork instead of the NuGet package. Upstream's own documentation is
in [README.md](README.md).

## Structure

```
Kukks/BlazorDashboardKit          the original library. We never push here.
        │  new releases (git tags v0.2.2, v0.3.0, ...)
        ▼
adamchawdhurynb/BlazorDashboardKit
   main       an untouched copy of Kukks's main.
   prod   Kukks's latest release we've taken, plus the host app's commits on top.
              The default branch, and what the host app builds.
        │
        ▼
The host app\src\BlazorDashboardKit-repo   a clone of this fork, on prod
```

The host app's `.csproj` references `src/BlazorDashboardKit-repo/src/BlazorDashboardKit/BlazorDashboardKit.csproj`,
so the host app builds the kit from source. Changes only flow from Kukks to us; nothing here is sent to
his repo.

## Where changes come from

**Kukks's releases.** He tags each release (`v0.3.0`, ...) when he bumps the library's version.
These are the updates we take.

**The host app's own commits** on `prod`, alongside Kukks's code:

| Commit | What it does | Used by the host app for |
|---|---|---|
| feat: make widget content inert in edit mode | Widgets' own links and buttons can't be clicked or tabbed to while the dashboard is being arranged | Arranging the dashboard |
| fix: stop re-renders wiping GridStack's classes; no shrink on load | The grid container's class is fixed in markup and switched in JS; animation starts after the first layout | No flashing or shrinking on load |
| fix: lock a widget in place instead of rebuilding the grid | Lock/unlock updates one widget instead of rebuilding the grid | `WidgetEditHeader`'s lock button |
| feat: compact the grid when leaving edit mode | Done slides widgets up into free space and saves the result | Done |
| feat: compact the grid live in edit mode | Widgets slide up into free space right after each drag, resize, add or remove, not only on Done | Arranging the dashboard |
| feat: let the host replace the whole dashboard toolbar | `ToolbarTemplate` / `ToolbarComponent` and `DashboardToolbarContext` | `DashboardToolbar` (the page title, dashboard switcher and links) |
| feat: add MaxDashboardNameLength | Renamed and imported names are cut to a limit | `MaxDashboardNameLength="30"` in `Home.razor` |
| ci: test the prod branch and watch for upstream releases, and ci: open a pull request for each upstream release | `.github/workflows/prod.yml` (below) | Taking Kukks's releases |

The C# changes have tests in `tests/BlazorDashboardKit.Tests`; the JS-only ones (the compaction
and animation) are checked on the dashboard itself. Keep new changes the same way: one commit per
change, with a test where one can be written. The fewer lines of Kukks's code we touch, the fewer
conflicts each update brings.

## How updates arrive

The **Prod fork** workflow (`.github/workflows/prod.yml`) runs every **Monday at 13:00 UTC**.
It compares Kukks's newest release tag with the ones `prod` already has. When there's a new one,
it merges the release into a new branch, `update/<tag>`, runs the kit's tests, and **opens a pull
request into `prod`** titled *"Update to upstream release vX.Y.Z"*.

**The pull request is never merged automatically.** Nothing changes in `prod`, or in the host app,
until someone clicks **Merge**. A ruleset on `prod` stops the workflow from pushing to it
directly.

If the release can't be merged automatically (it conflicts with our changes, the tests fail
afterwards, or it changes files under `.github/workflows`, which GitHub won't let a workflow push),
the workflow opens an **issue** instead: *"Upstream release vX.Y.Z needs merging by hand"*, saying
why. Either way, there's one PR or issue per release.

- To be emailed about these, click **Watch → Custom** on the fork's GitHub page and tick
  **Pull requests** and **Issues**.
- To check right away: **Actions → Prod fork → Run workflow**. The log says "Up to date" or "Behind".

The same workflow also runs the kit's tests whenever something is pushed to `prod`.

## How to update

### When there's a pull request

1. **Read what changed.** The PR links to Kukks's changes. If they touch `DashboardHost.razor`,
   `wwwroot/dashboard-interop.js` or `OverrideContexts.cs` (where our changes are), check the host app's
   dashboard with the PR's branch before merging. In `src/BlazorDashboardKit-repo`:
   `git fetch origin` then `git checkout update/v0.3.0`, run the host app, and
   `git checkout prod` afterwards.
2. **Click Merge** on the PR, or close it to skip that release.
3. **Pull it into the host app.** In `src/BlazorDashboardKit-repo`, run `git pull`, then rebuild the host app
   and check the dashboard (list below).

### When there's an issue instead

Merge the release by hand. In `src/BlazorDashboardKit-repo`, with `v0.3.0` standing for the release:

```
git checkout prod
git pull
git fetch upstream --tags
git merge v0.3.0
```

If git reports conflicts, it names the files. Fix each so it keeps both Kukks's new behaviour and
our change (in Visual Studio's merge editor, *Current* is ours and *Incoming* is Kukks's), then
`git add <file>` and `git commit`. `git merge --abort` gives up and puts everything back.

Then test and push:

```
dotnet test tests/BlazorDashboardKit.Tests/BlazorDashboardKit.Tests.csproj
git push
```

Close the issue with a note like "Merged v0.3.0 by hand".

### Checking the dashboard

After any update, build and run the host app and check:

- it loads without flashing or shrinking
- in Edit Dashboard you can drag, resize, lock, duplicate, remove and add widgets
- after a drag, resize or remove, widgets slide up into gaps straight away
- switching, new, rename, export, import and delete dashboard all work
