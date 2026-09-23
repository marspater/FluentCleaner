## 2026-03-31 - WinUI 3 CheckBox Accessible Name Binding
**Learning:** In WinUI 3 XAML, when a `CheckBox` contains panel layout children (such as `StackPanel` or `Grid`) instead of plain text content, screen readers (Narrator, NVDA) cannot automatically infer the accessible name of the checkbox control and announce only "Unchecked, CheckBox".
**Action:** Always set `AutomationProperties.Name="{x:Bind ...}"` directly on any `CheckBox` element that contains child layout containers to ensure accessible screen reader labels.
