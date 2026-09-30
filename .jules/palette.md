## 2026-03-31 - WinUI 3 CheckBox Accessible Names with Custom Content
**Learning:** In WinUI 3, when a `CheckBox` contains layout panel children (such as `StackPanel` or `Grid`) instead of plain text string `Content`, screen readers (Narrator/NVDA) cannot infer a label and announce only "Unchecked, Checkbox" without reading the item name.
**Action:** Explicitly set `AutomationProperties.Name="{x:Bind ...}"` on any `CheckBox` containing complex XAML child elements.
