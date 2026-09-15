## 2026-03-31 - Explicit AutomationProperties.Name on WinUI 3 CheckBoxes with Panel Content
**Learning:** In WinUI 3, when a `CheckBox` control contains layout panel children (e.g., `StackPanel`) rather than direct text `Content`, screen readers like Narrator and NVDA cannot automatically derive an accessible label from child UI elements.
**Action:** Always set `AutomationProperties.Name="{x:Bind ...}"` explicitly on `CheckBox` elements when using complex or custom templates.
