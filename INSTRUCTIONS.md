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

**The host app's own commits.** Each is one commit on `prod`, on top of Kukks's release:

| Commit | What it does | Used by the host app for |
|---|---|---|
| feat: make widget content inert in edit mode | Widgets' own links and buttons can't be clicked or tabbed to while the dashboard is being arranged | Arranging the dashboard |
| fix: stop re-renders wiping GridStack's classes; no shrink on load | The grid container's class is fixed in markup and switched in JS; animation starts after the first layout | No flashing or shrinking on load |
| fix: lock a widget in place instead of rebuilding the grid | Lock/unlock updates one widget instead of rebuilding the grid | `WidgetEditHeader`'s lock button |
| feat: compact the grid when leaving edit mode | Done slides widgets up into free space and saves the result | Done |
| feat: let the host replace the whole dashboard toolbar | `ToolbarTemplate` / `ToolbarComponent` and `DashboardToolbarContext` | `DashboardToolbar` (the page title, dashboard switcher and links) |
| feat: add MaxDashboardNameLength | Renamed and imported names are cut to a limit | `MaxDashboardNameLength="30"` in `Home.razor` |
| ci: test the prod branch and watch for upstream releases | `.github/workflows/prod.yml` (below) | Knowing when Kukks releases |

Each has tests in `tests/BlazorDashboardKit.Tests`. Keep new changes the same way: one commit per
change, with a test. The fewer lines of Kukks's code we touch, the fewer conflicts each update brings.

## How we know an update is needed

The **Prod fork** workflow (`.github/workflows/prod.yml`) runs every **Monday at 13:00 UTC**.
It compares Kukks's newest release tag with the one `prod` is built on. When there's a newer
one, it **opens an issue** in the fork titled *"Upstream release vX.Y.Z is available"*, with a
link to what changed. It opens one issue per release.

- To be emailed about these issues, click **Watch → Custom → Issues** on the fork's GitHub page.
- To check right away: **Actions → Prod fork → Run workflow**. The log says "Up to date" or "Behind".

The same workflow also runs the kit's tests whenever something is pushed to `prod`.

You don't have to take every release straight away. Read what changed first, then decide.

## How to update

All commands run in `src/BlazorDashboardKit-repo`. `v0.3.0` stands for the new release's tag, and
`v0.2.2` for the one we're on now.

1. **Get Kukks's new release.**
   ```
   git checkout prod
   git pull
   git fetch upstream --tags
   ```
2. **Read what changed:** `git log --oneline v0.2.2..v0.3.0`, the compare link in the issue, and
   Kukks's `CHANGELOG.md`. Look out for changes to `DashboardHost.razor`,
   `wwwroot/dashboard-interop.js` and `OverrideContexts.cs`. That's where our commits are, so
   that's where conflicts will come from.
3. **Move our commits onto the new release.**
   ```
   git rebase v0.3.0
   ```
   This replays the host app's commits, one at a time, on top of Kukks's new code.
   - **If it stops on a conflict**, git names the files. Fix each so it keeps both Kukks's new
     behaviour and our change, then `git add <file>` and `git rebase --continue`. In Visual
     Studio's merge editor, *Current* is Kukks's new code and *Incoming* is our commit (the reverse
     of what you'd expect).
   - If Kukks has fixed the same thing one of our commits does, `git rebase --skip` drops ours.
   - `git rebase --abort` gives up and puts everything back as it was.
4. **Test.** Run `dotnet test tests/BlazorDashboardKit.Tests/BlazorDashboardKit.Tests.csproj`, then
   build and run the host app and check the dashboard:
   - it loads without flashing or shrinking
   - in Edit Dashboard you can drag, resize, lock, duplicate, remove and add widgets
   - Done slides widgets up into gaps
   - switching, new, rename, export, import and delete dashboard all work
5. **Push.** The rebase rewrote `prod`'s history, so it needs:
   ```
   git push --force-with-lease
   ```
6. **Close the issue** with a note like "Updated to v0.3.0".
