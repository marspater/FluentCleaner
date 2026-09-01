## 2025-05-18 - High-performance Winapp2 INI parsing with ReadOnlySpan<char>

**Learning:** `Winapp2.ini` is a large file (~30,000 lines). Parsing it previously used string splitting (`content.Split(new[] { '\r', '\n' })`) and Compiled Regex matches (`Regex.IsMatch`) for key types, which allocated tens of thousands of intermediate string objects and regex match state objects on the heap. Replacing string splitting and regexes with line-by-line `ReadOnlySpan<char>` slicing and span-based key/value parsing reduced INI parse time by ~2.2x to 4x while eliminating thousands of GC allocations.

**Action:** For large text/INI parsing operations in C#, avoid `Regex` and `string.Split()`. Use `ReadOnlySpan<char>` slicing and custom character/prefix checks to achieve zero-allocation parsing.
