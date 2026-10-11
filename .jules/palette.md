# Palette's Journal - UX & Accessibility Learnings

## 2026-03-30 - Accessible Name Binding for ToggleSwitch Controls
**Learning:** In WinUI 3, `ToggleSwitch` controls with empty `OnContent` and `OffContent` labels (`OnContent="" OffContent=""`) are read as unlabelled toggles by screen readers (Narrator/NVDA). Binding `AutomationProperties.Name="{x:Bind Name}"` directly to the item name in data templates enables screen readers to announce the specific entity being toggled without altering visual layout.
**Action:** Always check `ToggleSwitch` controls and icon-only interactive elements in XAML templates and ensure `AutomationProperties.Name` is explicitly set via `x:Bind` or resource strings.
