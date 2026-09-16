## 2026-03-31 - WinUI 3 CheckBox Controls with Child Panels Need Explicit ARIA Labels
**Learning:** In WinUI 3, when a `CheckBox` control uses a layout container (like `StackPanel`) as its `Content` instead of a simple text string, screen readers (Narrator/NVDA) cannot automatically extract a accessible text label. Focus lands on the checkbox but announces only "Check box un-checked" without the item name.
**Action:** Always set `AutomationProperties.Name="{x:Bind ...}"` on `CheckBox` controls whenever `Content` contains a layout panel or custom visual elements.
