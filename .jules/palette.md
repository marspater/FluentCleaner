## 2026-03-30 - Keyboard Focus Visibility for Hover-Only Action Controls
**Learning:** Action menu buttons with `Opacity="0"` that only toggle visibility on `PointerEntered`/`PointerExited` remain invisible when focused via keyboard navigation (Tab key).
**Action:** Always add `GotFocus` and `LostFocus` event handlers (or update opacity on focus) to ensure interactive elements become visible when receiving keyboard focus.
