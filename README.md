# kilrkrow Launcher

Native **WPF** (`.NET 8`, same stack as [sideclip](https://github.com/kilrkrow/sideclip)) picker for public **kilrkrow** Windows tools. It is not a web/Electron shell and it does not auto-launch anything on startup.

Public catalog works **without a GitHub token**. An optional token field is stored only under `%LocalAppData%\KilrkrowLauncher\settings.json` and is never committed.

## Catalog rules

A public, non-fork, non-archived repo is listed only when **`GET /repos/{owner}/{repo}/releases/latest`** has at least one entry in **`assets[]`** that is a Windows payload:

| Asset | Included? |
| --- | --- |
| `*.exe` or `*.msi` | Yes |
| `*.zip` that **contains** a `*.exe` entry | Yes (zip is opened / central-directory listed) |
| GitHub source zipball / tarball (`zipball_url`) | **No** — those are not release assets |
| Source-only zip (`.cs`, `.md`, `*.exe.config`, no real `.exe`) | **No** |
| `.nupkg`, `.js`, `.css`, empty `assets[]` | **No** |
| Private repos (even if a token can see them) | **No** |
| This launcher repo (`kilrkrow-launcher`) | Skipped |

When several Windows assets exist, the picker prefers MSI, then setup exe, then an **app/gui** zip over a **cli** zip.

Verified 2026-09-19 against the live anonymous API: **win-service-buddy** `v0.2.0` qualifies (`wsbuddy-app-win-x64-v0.2.0.zip` contains `WinServiceBuddy.App.exe`). **sideclip** and **netpulse** have no latest release. **voltdesk** has a latest release with **empty** `assets[]` (source zipball does not count).

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
- Portable `.exe` / zip-with-exe → extract or copy under the portable root.

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

`dotnet test` is TFM `net8.0` and runs on Linux/macOS/Windows. The WPF project is `net8.0-windows`.

## Smoke notes (greenfield)

1. Cold start shows the picker (nothing auto-launches).
2. Refresh catalog **without** a token: at least **win-service-buddy** appears if GitHub is reachable.
3. An installed row: **Launch** starts it; a second Launch **focuses** the existing window.
4. Check a missing row and an installed row, then **Launch all**: only the installed row starts.
5. **Download & install** on a missing zip: progress appears; Cancel during download does not crash.
6. MSI/admin tools show UAC; the launcher stays unelevated.

Rate-limit 403/429 surfaces a wait message and points at the optional token setting.
