# Palette's Journal

## 2026-03-31 - Accessibility on WinUI 3 controls with x:Uid
**Learning:** In WinUI 3 resw resource files, controls localized via x:Uid can specify accessible names using `[Uid].[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name` and tooltips using `[Uid].[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip`. Icon-only buttons lacking proper labels are invisible to screen readers, and adding x:Uid entries in Resources.resw provides localized screen reader accessibility without polluting XAML layout.
**Action:** Always verify icon-only buttons in WinUI XAML views and map `[Uid].[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name` and `ToolTipService.ToolTip` in Resources.resw.
