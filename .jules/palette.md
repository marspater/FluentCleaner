## 2026-03-31 - Accessible Names on WinUI 3 DataTemplate Controls
**Learning:** In WinUI 3 `DataTemplate` items, `CheckBox` controls with child layout panels (e.g., `StackPanel` containing icons/text) and `ToggleSwitch` controls with empty `OnContent`/`OffContent` do not expose default accessible names to screen readers (Narrator/NVDA).
**Action:** Always bind `AutomationProperties.Name="{x:Bind ...}"` directly on interactive controls inside `DataTemplate` items.
