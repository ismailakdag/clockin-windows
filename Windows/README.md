# Clockin for Windows

This is the Windows-native port of Clockin. It uses WPF on .NET 8, WinForms `NotifyIcon` for the taskbar tray, Win32 `RegisterHotKey` for global shortcuts, and a borderless `Topmost` WPF window for the pinned timer.

## Build

From the repository root:

```powershell
dotnet build Windows\Clockin.Windows\Clockin.Windows.csproj -c Release
.\Windows\build-windows.ps1
```

The self-contained executable is written to `dist\windows\Clockin.exe`.

## Windows behavior

- `Ctrl+Alt+I`: clock in / resume
- `Ctrl+Alt+P`: pause / resume
- `Ctrl+Alt+O`: clock out
- `Ctrl+Alt+E`: show the main window
- Tray close behavior matches macOS: closing the main window hides it while the timer continues.
- Local data is stored under `%APPDATA%\Clockin\clockin.json`, with automatic backups under `%APPDATA%\Clockin\Backups`.
- Mac JSON backups can be imported. Date values written by Swift's default `JSONEncoder` (seconds since 2001) and ISO dates are both accepted.

The Windows port intentionally uses system sound aliases for focus chimes because macOS `NSSound` names do not map one-to-one to Windows. Radio playback uses WPF `MediaPlayer` and the same Radio Paradise stream as the Mac app.
