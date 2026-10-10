## 2026-03-31 - WinUI 3 ToggleSwitch and Input Control Accessibility Labels

**Learning:** Interactive controls in WinUI 3 that omit visual headers or content strings (such as `ToggleSwitch` items with empty `OnContent`/`OffContent` in `DataTemplate` cards, or input controls using `x:Uid` placeholder resources) lack accessible names for screen readers like Narrator and NVDA unless `AutomationProperties.Name` is explicitly specified (e.g. `AutomationProperties.Name="{x:Bind Name}"`).
**Action:** When creating visual cards with toggle switches or icon/placeholder-only inputs, always inspect screen reader accessibility and explicitly bind `AutomationProperties.Name` to the associated item's header/name.
