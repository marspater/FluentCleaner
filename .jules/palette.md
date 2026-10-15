## 2025-05-18 - Accessibility Names for Custom WinUI 3 Item Templates
**Learning:** In WinUI 3 desktop controls rendered inside `DataTemplate` items (such as `ListView` items), `CheckBox` controls containing child layout panels (e.g. `StackPanel`) and `ToggleSwitch` controls with empty `OnContent`/`OffContent` properties are rendered without accessible text labels by screen readers (Narrator/NVDA).
**Action:** Always explicitly set `AutomationProperties.Name="{x:Bind ...}"` on `CheckBox` and `ToggleSwitch` controls within data templates to ensure screen readers announce the item name.
