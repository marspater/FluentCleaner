# Changelog

All notable changes to **FluentCleaner** are documented in this file.

---

## [Unreleased] - 2026-07-30

### 🔒 Security & Hardening
- **Command Injection Fixes**: Hardened process launch arguments and path validation in `ToolsPage.xaml.cs`, Explorer process initiation, and post-clean script execution commands.
- **CodeQL Tuning**: Streamlined static analysis rules to remove redundant security-extended query conflicts.

### ⚡ Performance & Responsiveness
- **Asynchronous Disk Info**: Offloaded synchronous `DriveInfo` disk space querying to background threads to prevent UI hangs.
- **View Model Cache Optimization**: Replaced repeated `SelectMany` tree evaluations in view models with a cached flat list for faster rule rendering and selection.
- **Non-Blocking File I/O**: Converted synchronous file checks in `CustomEntryService`, `CliDebloatModule`, and `CleaningService` to non-blocking async calls.

### 🤖 AI Assistance (Groq Integration)
- **Smart Rule Explainer**: Integrated Groq (`llama-3.3-70b-versatile`) to provide plain-English explanations of Winapp2 cleaner entries using real system paths and registry keys.
- **Automated Script & Rule Generation**: Added AI-powered PowerShell script generation and custom Winapp2 INI entry creation from natural language descriptions.
- **API Key Diagnostics & Localization**: Added instant key validation and localized system prompts based on Windows UI culture.

### 🧹 Code Health & Logging
- **Swallowed Exception Logging**: Resolved silent/empty `catch` blocks across `PathExpander`, `CleaningService`, `SettingsPageViewModel`, `SilentRunner`, `ToolsPage`, `NewCleanerDialog`, and `AppSettings.cs` by logging tracebacks.
- **Compiler Nullability Cleanups**: Fixed CS8602, CS8604 nullability warnings and reference comparison issues in core services.
- **Automated Unit Testing**: Expanded `FluentCleaner.Tests` suite with test coverage for `Winapp2Parser.Parse` and `AiExplainer` error handling.

### 🛠️ Tooling, Build & IDE Compatibility
- **WinAppSDK Upgrade**: Configured WinAppSDK 2.3.1 bootstrapper and updated git remote origins (`marspater/FluentCleaner`).
- **Dual Solution Maintenance**: Added standard `FluentCleaner.sln` alongside `FluentCleaner.slnx` with multi-project references (`FluentCleaner` + `FluentCleaner.Tests`) for seamless loading across Visual Studio, VS Code, and JetBrains Rider.
- **Markdown & Workspace Configs**: Added `.vscode/settings.json` markdownlint configuration to eliminate spurious IDE warnings.
