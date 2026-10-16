## 2026-03-31 - Explicit AutomationProperties.Name on WinUI Controls with Custom Templates
**Learning:** In WinUI 3, `CheckBox` controls wrapping layout panels (`StackPanel`) or `ToggleSwitch` controls with empty `OnContent`/`OffContent` do not expose a reliable automated Accessible Name to screen readers (Narrator/NVDA).
**Action:** Always set `AutomationProperties.Name` explicitly on interactive controls that use child panels or custom/empty content templates.
