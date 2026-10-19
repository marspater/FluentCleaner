# Palette's Journal - Critical UX & Accessibility Learnings

## 2026-03-30 - WinUI 3 Accessible Names for Complex/Empty Content Controls
**Learning:** In WinUI 3, `CheckBox` controls containing layout panel children (e.g. `StackPanel`) or `ToggleSwitch` controls with empty `OnContent`/`OffContent` and no `Header` fail to announce accessible labels to screen readers (Narrator/NVDA).
**Action:** Always set `AutomationProperties.Name` (bound via `{x:Bind}` or static text) directly on `CheckBox` and `ToggleSwitch` controls whenever content is empty or contains child layout panels.
