## 2026-03-30 - WinUI 3 ToggleSwitch & Custom CheckBox Accessible Names
**Learning:** In WinUI 3, when `ToggleSwitch` controls are used headerless with empty `OnContent=""` and `OffContent=""`, or when `CheckBox` controls wrap complex layout containers (such as `StackPanel`), UI Automation / screen readers (Narrator / NVDA) cannot automatically infer accessible names.
**Action:** Always explicitly declare `AutomationProperties.Name` on headerless `ToggleSwitch` controls and custom `CheckBox` elements to ensure screen readers announce meaningful labels to users.
