# kilrkrow Launcher

Native **WPF** (`.NET 10`, same stack as [sideclip](https://github.com/kilrkrow/sideclip)) picker for public **kilrkrow** Windows tools. It is not a web/Electron shell and it does not auto-launch anything on startup.

Public catalog works **without a GitHub token**. The launcher downloads one static `catalog.json` (GitHub Pages). An optional token is stored only under `%LocalAppData%\KilrkrowLauncher\settings.json` and is used only if the manifest is unreachable (API fallback).

## Catalog (manifest)

Discovery runs **server-side** in `.github/workflows/catalog.yml` (every 30 minutes, or **Actions → Catalog → Run workflow**). `tools/CatalogBuilder` reuses `GitHubCatalogClient` + `WindowsAssetFilter` (zip-contains-exe unchanged) with `GITHUB_TOKEN`, then publishes `catalog.json` to GitHub Pages.

**Enable once (required for the happy path):** Repo **Settings → Pages → Build and deployment → Source: GitHub Actions**. Then run the Catalog workflow. The launcher GETs:

`https://kilrkrow.github.io/kilrkrow-launcher/catalog.json`

That GET is not counted against the GitHub REST rate limit. New tool releases appear within ~30 minutes, or immediately after a manual dispatch.

On startup the launcher shows the last cached manifest from `%LocalAppData%\KilrkrowLauncher\catalog.json`, then refreshes in the background (ETag / If-None-Match). If Pages is down, it falls back to the live API with ETag + on-disk zip-index cache keyed by asset URL.

The manifest `launcher` field is this repo's latest Windows release (self-update). If `launcher.tag` is newer than the running assembly, **Update launcher** downloads the asset and swaps/restarts.

## Catalog rules

A public, non-fork, non-archived repo is listed only when it has a GitHub **Release with a Windows asset**. Discovery walks the first page of **`GET /repos/{owner}/{repo}/releases`** (newest-first, drafts skipped) and picks the **newest tagged release** whose **`assets[]`** contain a Windows payload. If the newest tag is asset-less, an older qualifying release is used. Repos with no releases (or only empty / non-Windows assets) stay out:

| Asset | Included? |
| --- | --- |
| `*.exe` or `*.msi` | Yes |
| `*.zip` that **contains** a `*.exe` entry | Yes (zip is opened / central-directory listed) |
| GitHub source zipball / tarball (`zipball_url`) | **No** — those are not release assets |
| Source-only zip (`.cs`, `.md`, `*.exe.config`, no real `.exe`) | **No** |
| `.nupkg`, `.js`, `.css`, empty `assets[]` | **No** |
| Private repos (even if a token can see them) | **No** |
| This launcher repo (`kilrkrow-launcher`) | Skipped from **tools**; included under manifest `launcher` for self-update |

When several Windows assets exist, the picker prefers MSI, then setup exe, then an **app/gui** zip over a **cli** zip.

Verified 2026-09-19 against the live anonymous API: **win-service-buddy** `v0.2.0` qualifies (`wsbuddy-app-win-x64-v0.2.0.zip` contains `WinServiceBuddy.App.exe`). **voltdesk** newest tags (`v1.1.0` … `v1.0.3`) have empty `assets[]`; catalog uses **`v1.0.2`** (`VoltDesk.exe`). **sideclip** and **netpulse** have zero releases and stay out until Guy publishes a Release with a Windows asset.

Unit tests in `tests/KilrkrowLauncher.Tests` cover the filter with fixture zips so source-only archives cannot sneak in.

## Install detection

A row is **Installed** when any of these resolve to an `.exe`:

1. Portable staging: `%LocalAppData%\KilrkrowLauncher\apps\{repo}\**`
2. Well-known folders inferred from the repo / asset / zip exe names (`%LocalAppData%`, `%ProgramFiles%`, `%ProgramFiles(x86)%`)
3. Uninstall registry (`HKCU` / `HKLM` Uninstall keys: `DisplayName`, `InstallLocation`, `DisplayIcon`)
4. Start Menu `.lnk` targets under user and common Programs folders

Name hints are tokens of **4+ characters** from the repo, asset stem, and zip exe names (noise like `win`, `x64`, `app` is dropped) so a generic "app" does not match every shortcut.

## Launch policy (default)

**If a matching process is already running, focus its main window and do not start a second instance.** Tray-only processes (no HWND) are still treated as running. Documented here so a later "always spawn" mode would be an explicit change.

**Launch all** = checked rows **intersect** installed rows. Missing / error rows are never started.

## Install v1

`IInstallProvider` is the seam. v1 registers **`GitHubReleaseInstallProvider` only**.

- Download the chosen latest-release asset with progress and **Cancel** (cancel deletes the partial file).
- `.msi` → visible `msiexec /i` (no `/qn`, `/quiet`, `/passive`, no `Verb=runas`).
- Setup-named `.exe` → visible process; the installer's own manifest triggers UAC.
- Portable `.exe` / zip-with-exe → extract or copy under the portable root. The **primary exe picker** skips helpers (`createdump`, crashpad, uninstall, setup, vcredist, …) and prefers a filename that matches the repo / display name (Sideclip.zip with `createdump.exe` + `Sideclip.exe` launches Sideclip). Launch sets **WorkingDirectory** to the exe folder so self-contained .NET can load sibling DLLs. Failure shows the path and exception; it never claims success.

The launcher itself is `asInvoker` (`app.manifest`). It never silently elevates. MSI / admin setups show a normal UAC prompt.

**Chocolatey** is a future `IInstallProvider` (`Id = chocolatey`). It is not implemented in v1.

## UI

Haven / lab-dashboard glass: dark charcoal, frosted panels, restrained teal and gold. States: **Installed** (green), **Missing** (gold), **Running** (teal), **Error** (red). Real multi-size `Assets/launcher.ico` for the exe, window, and tray — not `SystemIcons.Application`.

ASCII `...` only in UI strings.

## Build

```powershell
dotnet test tests/KilrkrowLauncher.Tests/KilrkrowLauncher.Tests.csproj
# WPF publish requires Windows:
.\scripts\publish.ps1
```

`dotnet test` is TFM `net10.0` and runs on Linux/macOS/Windows. The WPF project is `net10.0-windows`. `global.json` pins SDK `10.0.100` with `rollForward: latestFeature` so installed 10.0.108 / 10.0.203 bands work.

## Smoke notes (greenfield)

1. Cold start shows the picker (nothing auto-launches).
2. Refresh catalog **without** a token: after Pages is enabled and Catalog is re-run, **win-service-buddy** and **voltdesk** (`v1.0.2`) appear from `catalog.json`. Sideclip/NetPulse stay out until they have a Release with a Windows asset.
3. An installed row: **Launch** starts it; a second Launch **focuses** the existing window.
4. Check a missing row and an installed row, then **Launch all**: only the installed row starts.
5. **Download & install** on a missing zip: progress appears; Cancel during download does not crash.
6. **Sideclip** smoke: Download & install the win-x64 zip, then Launch. Staging has both `createdump.exe` and `Sideclip.exe`; Launch must start **Sideclip.exe** with cwd = that folder (sibling DLLs). A minidump means the picker still chose createdump.
7. MSI/admin tools show UAC; the launcher stays unelevated.

If the manifest is missing (Pages not enabled yet) the status line falls back to the API; 403/429 then surfaces a wait message.
