## 2025-05-20 - Path Variable Expansion Short-Circuiting

**Learning:** `PathExpander.ExpandVariables` is called repeatedly across thousands of cleaner rules during detection and scanning. Iterating through all environment variable tokens (`_vars`) on every call causes unnecessary string scans/allocations for literal paths (without `%`) and redundant `Environment.GetFolderPath` queries on instantiation.
**Action:** Make `_vars` static readonly and check `if (path.IndexOf('%') >= 0)` upfront to bypass variable replacement loops and OS environment variable lookup for literal paths. Break early once no `%` characters remain.
