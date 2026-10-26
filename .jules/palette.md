## 2026-03-29 - Accessible Screen Reader Names for WinUI CheckBoxes and ToggleSwitches
**Learning:** In WinUI 3, CheckBox controls with composite layout panel children (e.g. StackPanels with icons or secondary text) and ToggleSwitch controls with empty OnContent/OffContent are silent or uninformative when navigated with screen readers (Narrator/NVDA) unless AutomationProperties.Name is explicitly assigned.
**Action:** Always specify AutomationProperties.Name="{x:Bind Name}" (or explicit descriptive text) on CheckBox and ToggleSwitch controls with panel content or cleared toggle text.
