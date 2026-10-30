## 2026-03-31 - WinUI 3 Accessible Names for Complex Content Controls

**Learning:** In WinUI 3, when `CheckBox` controls contain complex layout panel children (such as `StackPanel`) or `ToggleSwitch` controls have empty `OnContent`/`OffContent` strings, UI Automation cannot automatically derive an accessible label string. Screen readers like Narrator and NVDA only announce "Checkbox, unchecked" or "Toggle switch, off" without indicating which item or setting the control represents.
**Action:** Always bind `AutomationProperties.Name` explicitly (e.g. `AutomationProperties.Name="{x:Bind Name}"` or `AutomationProperties.Name="{x:Bind FolderName}"`) on `CheckBox` and `ToggleSwitch` controls whenever their visual `Content` is a panel layout or empty string.
