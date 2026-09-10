## 2026-03-31 - WinUI Accessible Name & ToolTip Patterns
**Learning:** Interactive controls without explicit `AutomationProperties.Name` rely solely on inner text strings, which may not adequately communicate intent to screen readers or when icons are present. Adding `ToolTipService.ToolTip` and explicit `AutomationProperties.Name` enhances desktop accessibility and hover clarity.
**Action:** Always verify `AutomationProperties.Name` and `ToolTipService.ToolTip` on action buttons and control inputs in WinUI XAML pages.
