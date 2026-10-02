## 2026-03-31 - WinUI 3 CheckBox Accessibility with Complex Child Layouts
**Learning:** In WinUI 3 desktop applications, when a `CheckBox` control contains complex panel children (like a `StackPanel` with icons and `TextBlock`s) instead of plain text, UI Automation (Narrator and NVDA) defaults to announcing "Check box, unchecked" without reading the inner text.
**Action:** Always set `AutomationProperties.Name` directly on `<CheckBox ...>` whenever its `Content` is a layout container or panel.
