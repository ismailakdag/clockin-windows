# Clockin for Windows

Clockin is a native Windows desktop time tracker built with WPF and .NET 8. It keeps the Mac version's core workflow while using Windows-native tray, global shortcuts, custom borderless windows, a resizable pinned timer, levels, badges, reports, themes, CSV import and local backups.

## Build

From the repository root:

```powershell
dotnet build Windows\Clockin.Windows\Clockin.Windows.csproj -c Release
.\Windows\build-windows.ps1
```

The self-contained executable is written to `dist\windows\Clockin.exe`.

## Controls

- `Ctrl+Alt+I`: clock in / resume
- `Ctrl+Alt+P`: pause / resume
- `Ctrl+Alt+O`: clock out
- `Ctrl+Alt+E`: show Clockin
- The main window can be hidden to the Windows tray.
- Tray-only mode uses the Windows notification-area icon beside the system clock; the pinned timer can be dragged and remembers its position.

## Data and import

Local data is stored under `%APPDATA%\Clockin`, with automatic backups under `%APPDATA%\Clockin\Backups`. The importer supports Clockin JSON backups, approved timecard paste, and CSV timecards with duplicate comparison before import.

The pinned timer supports Compact, Money, Goal and All modes, per-mode resizing, live earnings, TRY conversion, goal progress, averages and focus radio controls. Settings include themes, theme-specific typography, pay/rate schedules, focus chimes, mascot modes and tray fields.

See [Windows/README.md](Windows/README.md) for Windows-specific implementation notes.
