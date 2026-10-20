# BlazorDashboardKit for the host app

The host app's dashboard (`/home`) is built on [BlazorDashboardKit](https://github.com/Kukks/BlazorDashboardKit),
an open-source library by Andrew Camilleri ("Kukks"). The host app needs a few changes the library
doesn't offer, so it uses this fork instead of the NuGet package:
https://github.com/adamchawdhurynb/BlazorDashboardKit

This file covers how the fork is laid out, how it stays up to date with Kukks's repo, and how to
take a new release. Upstream's own documentation is in [README.md](README.md).

## How it fits together

```
Kukks/BlazorDashboardKit          the original library. We never push here.
        │  new releases (git tags v0.2.2, v0.3.0, ...)
        ▼
adamchawdhurynb/BlazorDashboardKit
   main       an untouched copy of Kukks's main. Nobody commits to it.
   prod   Kukks's latest release we've taken, plus the host app's commits on top.
              This is the default branch, and what the host app builds.
        │  a git clone, checked out on prod
        ▼
The host app\src\BlazorDashboardKit-repo
```

- In the host app, `src/BlazorDashboardKit-repo` is a normal git clone of this fork. The host app itself isn't
  in git, so this folder is the only part that is.
- The host app's `.csproj` references `src/BlazorDashboardKit-repo/src/BlazorDashboardKit/BlazorDashboardKit.csproj`,
  so the host app builds the kit from source: edit it in Visual Studio like any other project in the solution.
- Changes only ever flow from Kukks to us. Nothing we do here is sent to Kukks's repo.

## The host app's changes

Each change is one commit on `prod`, on top of Kukks's release:

| Commit | What it does | Used by the host app for |
|---|---|---|
| feat: make widget content inert in edit mode | Widgets' own links and buttons can't be clicked or tabbed to while the dashboard is being arranged | Arranging the dashboard |
| fix: stop re-renders wiping GridStack's classes; no shrink on load | The grid container's class is fixed in markup and switched in JS; animation starts after the first layout | No flashing or shrinking on load |
| fix: lock a widget in place instead of rebuilding the grid | Lock/unlock updates one widget instead of rebuilding the grid | `WidgetEditHeader`'s lock button |
| feat: compact the grid when leaving edit mode | Done slides widgets up into free space and saves the result | Done |
| feat: let the host replace the whole dashboard toolbar | `ToolbarTemplate` / `ToolbarComponent` and `DashboardToolbarContext` | `DashboardToolbar` (the page title, dashboard switcher and links) |
| feat: add MaxDashboardNameLength | Renamed and imported names are cut to a limit | `MaxDashboardNameLength="30"` in `Home.razor` |
| ci: test the prod branch and watch for upstream releases | `.github/workflows/prod.yml`, see below | Knowing when Kukks releases |

Each has tests in `tests/BlazorDashboardKit.Tests`. Run them with
`dotnet test tests/BlazorDashboardKit.Tests/BlazorDashboardKit.Tests.csproj`.

Keep it that way when changing the kit: one commit per feature or fix, with a test, and new files
rather than edits to upstream's files wherever you can (as with this file and `prod.yml`). The
fewer lines of Kukks's code we touch, the fewer conflicts each update brings.

## Who can do what

The fork is public, so **anyone can clone it and read it, with or without a GitHub account.** You
only need access to **push** (commit to `prod`, take an update, or change settings):

- **The `adamchawdhurynb` account** owns the fork and can do everything.
- **Anyone else pushes with their own GitHub account**, once `adamchawdhurynb` adds them under the
  fork's **Settings → Collaborators** and they accept the invitation. Don't share the
  `adamchawdhurynb` login.

### Using your own account in this clone

The clone on the original dev machine has the owner's username written into its remote URL, so git
asks for that account:

```
git remote -v
origin    https://adamchawdhurynb@github.com/adamchawdhurynb/BlazorDashboardKit.git
```

If you're a collaborator pushing from that folder, switch the URL to your own username (run it in
`src/BlazorDashboardKit-repo`):

```
git remote set-url origin https://<your-github-username>@github.com/adamchawdhurynb/BlazorDashboardKit.git
```

The next push opens a GitHub sign-in window; sign in as yourself. Windows remembers it for that
URL. A fresh clone of the plain URL (`https://github.com/adamchawdhurynb/BlazorDashboardKit.git`)
uses whichever GitHub account Windows already has saved.

**"Permission to adamchawdhurynb/BlazorDashboardKit.git denied to <someone>" (error 403)** means git
signed in with an account that has no push access, usually a different GitHub account Windows had
saved. Fix the URL as above, or get that account added as a collaborator.

## Setting up the folder on a new machine

Do this if `src/BlazorDashboardKit-repo` is missing, or if it arrived as a plain copy without its
`.git` folder (`git status` there says "not a git repository"). From the host app's `src` folder:

```
git clone https://github.com/adamchawdhurynb/BlazorDashboardKit.git BlazorDashboardKit-repo
cd BlazorDashboardKit-repo
git remote add upstream https://github.com/Kukks/BlazorDashboardKit.git
git fetch upstream --tags
```

The folder name matters: the host app's project reference expects `src/BlazorDashboardKit-repo`. The
clone opens on `prod`, since that's the default branch. If it doesn't, run `git checkout prod`.

You only need the `upstream` remote if you'll be taking Kukks's updates. To just build the host app, the
clone alone is enough.

## Changing the kit

1. Edit files under `src/BlazorDashboardKit-repo` in Visual Studio, as part of the the host app solution.
   - JS and CSS in the kit's `wwwroot` can be served stale after a plain build. Rebuild, and
     hard-refresh the browser (Ctrl+F5) when checking.
2. Run the kit's tests (command above), and add one for the change.
3. Commit and push from `src/BlazorDashboardKit-repo`:
   ```
   git add -A
   git commit -m "fix: what changed and why"
   git push
   ```
   The **Prod fork** workflow then runs the tests on GitHub (see the fork's **Actions** tab).

Before starting, run `git pull` in case someone else pushed. If an update was taken since
your last pull, see [After someone else takes an update](#after-someone-else-takes-an-update).

## Knowing when Kukks releases

Kukks marks each release with a git tag such as `v0.3.0`. His publish workflow creates it when the
version in the csproj changes. He doesn't create GitHub "Releases", so GitHub's
*Watch → Releases* never fires for this repo.

Instead, `.github/workflows/prod.yml` (the **Prod fork** workflow) runs every
**Monday at 13:00 UTC**. It compares Kukks's newest `v*` tag with the release `prod` is built on.
If there's a newer one, it **opens an issue** in the fork titled
*"Upstream release vX.Y.Z is available"*, with a link to what changed and the commands to update.
It opens one issue per release, however many Mondays pass.

- **To be emailed about these issues**, open the fork on GitHub, click **Watch → Custom**, and tick
  **Issues**. This works with any GitHub account, collaborator or not. The owner is notified anyway.
- **To check right away:** fork → **Actions** → **Prod fork** → **Run workflow**, on `prod`.
  The *Compare with the newest upstream release* step's log says "Up to date" or "Behind".
- **Without GitHub:** in `src/BlazorDashboardKit-repo`, run
  ```
  git fetch upstream --tags
  git describe --tags --abbrev=0 --match "v*" HEAD     # the release we're built on
  git tag -l "v*" --sort=-v:refname | head -n1         # Kukks's newest release
  ```

You don't have to take every release straight away. Read what changed first, then decide.

## Taking a new release

You need push access (see [Who can do what](#who-can-do-what)). Below, `v0.3.0` stands for the new
release's tag. All commands run in `src/BlazorDashboardKit-repo`.

1. **Start clean and current.**
   ```
   git status                 # should say "nothing to commit"; commit or stash anything first
   git checkout prod
   git pull
   git fetch upstream --tags
   ```
2. **Read what changed:** `git log --oneline v0.2.2..v0.3.0` (use the release we're on now), the
   compare link in the issue, and `CHANGELOG.md` after step 3. Look out for changes to
   `DashboardHost.razor`, `wwwroot/dashboard-interop.js` and `OverrideContexts.cs`: that's where
   our commits are, and where conflicts will come from.
3. **Move our commits onto the new release.**
   ```
   git rebase v0.3.0
   ```
   This replays the host app's commits, one at a time, on top of Kukks's new code.
   - If it finishes without complaint, go to step 4.
   - **If it stops on a conflict**, git names the files. Open each in Visual Studio's merge editor.
     During a rebase its labels are the reverse of what you'd expect: *Current* is Kukks's new code,
     and *Incoming* is our commit being replayed. Keep both: Kukks's new behaviour *and* our change.
     Then:
     ```
     git add <the files you fixed>
     git rebase --continue
     ```
     Repeat until it finishes. If upstream has made one of our commits unnecessary (Kukks fixed the
     same thing), `git rebase --skip` drops ours.
   - **To give up and go back to how things were:** `git rebase --abort`. Nothing is lost.
4. **Test the kit.**
   ```
   dotnet test tests/BlazorDashboardKit.Tests/BlazorDashboardKit.Tests.csproj
   ```
   Fix failures before going on. A failing test of ours usually means Kukks changed something our
   commit relies on.
5. **Test the host app.** Build the the host app solution, run it, sign in, and on the dashboard check:
   it loads without flashing or shrinking; Edit Dashboard → drag, resize, lock, duplicate and
   remove widgets; Add Widget; Done (widgets slide up into gaps); the title drop-down switches
   dashboards; New Dashboard; Rename (30-character limit), Export, Import and Delete.
6. **Push.** The rebase rewrote `prod`'s history, so a plain push is refused:
   ```
   git push --force-with-lease
   ```
   `--force-with-lease` refuses if someone else pushed in the meantime, so you can't overwrite their
   work. If that happens, rebase again from step 1.
7. **Close the issue** in the fork with a note like "Updated to v0.3.0".
8. **Optional:** on the fork's GitHub page, switch to the `main` branch and click **Sync fork** to
   bring it level with Kukks's `main`. Nothing depends on it; it just keeps the copy tidy.

### After someone else takes an update

The rebase rewrote history, so on any **other** clone `git pull` fails or offers a confusing
merge. If you have no unpushed work of your own:

```
git fetch origin
git reset --hard origin/prod
```

(This discards uncommitted changes in that folder, so commit or copy anything you want first.) If you
do have unpushed commits, move them across instead with `git rebase origin/prod`.

## The Prod fork workflow

`.github/workflows/prod.yml` has two jobs:

| Job | Runs when | Does |
|---|---|---|
| test | something is pushed to `prod` | Builds and runs the kit's tests |
| upstream-release | Mondays 13:00 UTC, or **Run workflow** | The release check above |

Each run uses one job and skips the other, so a skipped job is normal. Kukks's own workflows
(`ci.yml`, `publish.yml`, `pages.yml`) only run on `main`, which we never push to, so they stay idle.

Settings this relies on (fork → **Settings**):

- **General → Default branch** is `prod`. GitHub only runs scheduled workflows on the default branch.
- **General → Features → Issues** is on, so the release check can open issues.
- **Actions → General**: actions are limited to this account's and GitHub's own, and the
  workflow token is read-only by default. `prod.yml` asks for the issue permission it needs
  itself.

If the Monday check seems to have stopped: GitHub pauses scheduled workflows in a repo that has
had no activity for 60 days. The **Actions** tab shows a banner with a button to turn it back on.

## Going back to Kukks's package

If Kukks's library ever covers everything the host app needs, the fork can go. Compare our commits with
his changelog, then switch The host app's `.csproj` from the project reference to
`<PackageReference Include="BlazorDashboardKit" Version="..." />`, delete
`src/BlazorDashboardKit-repo`, and archive the fork (**Settings → Archive this repository**).
