# FluentCleaner Agent Rules

## Platform

FluentCleaner is a native WinUI 3 Windows desktop application.

Target:
- .NET 10
- Windows 10/11
- WinUI 3 / Windows App SDK
- x64 and arm64

The Jules execution environment may be Linux-based and therefore cannot run or visually validate the WinUI desktop application.

## Validation hierarchy

1. Never claim the GUI is working based only on Linux-side compilation.
2. Use repository tests for service/business logic validation.
3. Use Windows GitHub Actions as the authoritative build validation.
4. Treat Windows CI failures as blocking.
5. Do not replace Windows-specific APIs with cross-platform abstractions merely to make Jules/Linux happy.
6. Do not weaken or remove WinUI/Windows dependencies to accommodate the agent environment.

## Commands

Preferred restore:
`dotnet restore FluentCleaner.slnx`

Tests:
`dotnet test FluentCleaner.slnx --configuration Release -p:Platform=x64`

Windows build:
`dotnet build FluentCleaner.slnx --configuration Release -p:Platform=x64`

Windows ARM64 build:
`dotnet build FluentCleaner.slnx --configuration Release -p:Platform=arm64`

## Startup debugging

When investigating application startup failures:
- inspect Program.cs
- inspect App.xaml.cs
- inspect MainWindow.xaml
- inspect MainWindow.xaml.cs
- inspect application logs
- never assume "process exists" means the UI successfully launched

Do not hide startup exceptions or mark fatal startup exceptions as handled merely to keep the process alive.

## Change discipline

Before modifying startup/bootstrap code:
1. Identify the exact failure point.
2. Make the smallest deterministic change.
3. Preserve Windows-specific behavior.
4. Run tests.
5. Verify Windows CI.
6. Do not perform unrelated refactoring.

## Production rule

A change is not considered production-ready merely because:
- the code parses,
- Linux-side restore succeeds,
- unit tests pass.

For WinUI/UI changes, Windows build/runtime verification is required.

---

## Architectural & Repository Guidelines

### Project Layout & Solution Files
- `FluentCleaner/`: Native WinUI 3 desktop application (`net10.0-windows10.0.19041.0`).
- `FluentCleaner.Tests/`: Unit tests project (`net10.0-windows10.0.19041.0`) containing 106+ unit tests with xUnit, FluentAssertions, and Moq.
- Dual Solution Files: Both `FluentCleaner.slnx` (XML-based format used by GitHub Actions CI and modern Visual Studio) and `FluentCleaner.sln` (standard format) exist in the root. Keep both solution files in sync whenever projects are added or removed.

### Design System & XAML Styling
- Use design tokens from `Themes/FluentStyles.xaml` (`FC_CardBrush`, `FC_CardStroke`, `FC_TextSecondaryBrush`, `FC_SubtleIconButton`, `FC_Badge*`, etc.).
- Never hardcode raw hex colors in views; always reference centralized theme resources to support high contrast and light/dark theme switching.
- Iconography strictly uses `Segoe Fluent Icons` glyphs.

### Performance & Memory Sensitivity
- INI ruleset parsing (`FileKeyEntry.cs`, `RegKeyEntry.cs`, `ExcludeKeyEntry.cs`) parses 30,000+ line INI files (`Winapp2.ini`).
- Always preserve zero-allocation `ReadOnlySpan<char>` parsing routines when reading or transforming INI entries. Avoid unnecessary intermediate string allocations or array splits in parsing hot-paths.

### Security & Process Execution
- FluentCleaner supports elevated and non-elevated operation. Check elevation via `SecurityUtils.IsElevated()`.
- Process invocations (e.g. DISM, winget, PowerShell CLI modules in `CliCleanerModule.cs` and `CliDebloatModule.cs`) must always use `UseShellExecute = false`, validate arguments to prevent command injection, and avoid running unvalidated dynamic scripts.

### Localization & Resource Strings
- Localized strings are stored in `FluentCleaner/Strings/{locale}/Resources.resw` (`en-US`, `de-DE`, `lt-LT`, etc.).
- When adding UI strings, define entries in `en-US/Resources.resw` first.
- Avoid orphaned or unused resource keys that can trigger `makepri.exe` `PRI263` compilation warnings.

### Git & Commit Hygiene
- Keep `.gitignore` clean: do not commit temporary `*.log`, `bin/`, `obj/`, or generated PRI files.
- Use semantic commit messages with emoji prefixes (e.g. `✨ Feature:`, `⚡ Perf:`, `🎨 UI:`, `🔒 Security:`, `🐛 Fix:`, `🧹 Chore:`, `📝 Docs:`).
