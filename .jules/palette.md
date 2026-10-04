## 2026-03-31 - Accessible Names for WinUI 3 Controls with Complex Children
**Learning:** In WinUI 3, controls like `CheckBox` or `ToggleSwitch` that contain complex layout panel children (e.g., `StackPanel`) or lack text headers do not automatically synthesize accessible names for UI Automation (UIA) screen readers (Narrator/NVDA).
**Action:** Always set `AutomationProperties.Name="{x:Bind ...}"` on interactive WinUI 3 controls when using custom child templates or panel layout children.
