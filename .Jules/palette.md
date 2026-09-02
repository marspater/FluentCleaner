# Palette's Journal

## 2026-03-30 - Accessible Icon Buttons with x:Uid in WinUI 3
**Learning:** Icon-only buttons in WinUI 3 require both tooltips for visual users and `AutomationProperties.Name` for screen reader accessibility. Mapping these in `.resw` resource files via `x:Uid` (`[Uid].[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip` and `[Uid].[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name`) ensures full accessibility and localized support without inline XAML clutter.
**Action:** When adding `x:Uid` to icon-only buttons in WinUI 3 XAML, always define both `ToolTipService.ToolTip` and `AutomationProperties.Name` entries in `Resources.resw`.
