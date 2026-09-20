# Palette's Journal

## 2026-03-31 - WinUI 3 CheckBox Accessibility with Complex Child Layouts
**Learning:** In WinUI 3, when a `CheckBox` control contains layout panel children (e.g., `StackPanel` containing icons and text blocks) instead of a simple string `Content`, WinUI's UI Automation peer does not automatically synthesize an accessible name from child controls. As a result, screen readers (Narrator/NVDA) cannot announce the item's label during keyboard focus or touch navigation.
**Action:** Always explicitly set `AutomationProperties.Name="{x:Bind ...}"` directly on `CheckBox` elements whenever their content contains complex controls or panel templates.
