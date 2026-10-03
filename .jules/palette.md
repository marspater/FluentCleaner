## 2026-03-30 - Accessible CheckBox labels for complex WinUI layout children
**Learning:** In WinUI 3, when a `CheckBox` control contains complex panel children (such as `StackPanel` or `Grid`) instead of a plain string content, Windows UI Automation (Narrator/NVDA) fails to infer an accessible name from child `TextBlock` elements and reads "CheckBox, unchecked" or "CheckBox, checked".
**Action:** Always set `AutomationProperties.Name="{x:Bind ...}"` explicitly on `CheckBox` controls whenever child markup contains panel containers or multiple child elements.
