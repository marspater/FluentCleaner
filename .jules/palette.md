## 2026-03-31 - WinUI 3 CheckBox and ToggleSwitch Screen Reader Accessibility

**Learning:** In WinUI 3, when `CheckBox` controls contain complex layout panel children (such as a `StackPanel` wrapping text and warning icons) or `ToggleSwitch` controls have empty `OnContent`/`OffContent` strings without a `Header`, screen readers (such as Windows Narrator or NVDA) cannot automatically infer the accessible name. This leaves vision-impaired users without screen reader context when navigating interactive item lists.
**Action:** Always explicitly bind `AutomationProperties.Name="{x:Bind ...}"` to `CheckBox` and `ToggleSwitch` elements embedded in `DataTemplate` items within `ListView` or `ItemsControl` templates.
