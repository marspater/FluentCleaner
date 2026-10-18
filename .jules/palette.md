## 2026-03-31 - Accessible Name Bindings for Custom Child Layouts in WinUI 3

**Learning:** In WinUI 3, when controls like `CheckBox` contain layout panel children (such as `StackPanel`) or `ToggleSwitch` has empty `OnContent`/`OffContent`, screen readers (Narrator/NVDA) fail to automatically extract an accessible label, reciting only "Unchecked, Checkbox" or "Toggle switch off".
**Action:** Always set `AutomationProperties.Name="{x:Bind ...}"` directly on `CheckBox` and `ToggleSwitch` controls when using custom child layouts or hidden content labels in `DataTemplate` items.
