## 2026-09-06 - SafeFileHandle File Size Query Optimization via RandomAccess
**Learning:** In hot file analysis routines that open a native Win32 `SafeFileHandle` (such as probing file deletability with `CreateFileW`), calling `new FileInfo(path).Length` creates unnecessary `FileInfo` heap allocations and issues redundant file path lookup syscalls (`GetFileAttributesExW`). Requesting `FILE_READ_ATTRIBUTES` (`0x0080`) access in `CreateFileW` enables using `RandomAccess.GetLength(handle)` directly on the open handle, bypassing heap allocations and duplicate OS path resolutions.
**Action:** When a `SafeFileHandle` is already open, include `FILE_READ_ATTRIBUTES` in access rights and query file length using `RandomAccess.GetLength(handle)` instead of instantiating `FileInfo`.

## 2026-09-05 - Winapp2.ini Parser Optimization
**Learning:** Parsing large INI files (like Winapp2.ini ~1.4MB with ~30,000 lines) using `string.Split('\r', '\n')` and compiled `Regex.IsMatch` creates significant memory allocation overhead (>7MB per parse) and high CPU latency (~30-35ms). Replacing `string.Split` with `MemoryExtensions.EnumerateLines()` over `ReadOnlySpan<char>` and `Regex` with direct `ReadOnlySpan<char>` prefix and ASCII digit validation reduces parse times by ~4x (~7.4ms) and eliminates line splitting / regex matching memory allocations.
**Action:** When parsing line-based text formats in .NET, prefer `ReadOnlySpan<char>` line enumeration and custom character validation methods over `string.Split()` and `Regex`.

## 2026-09-04 - Fast-pathing PathExpander string operations
**Learning:** In `PathExpander`, checking `path.IndexOf('%') >= 0` and `path.IndexOfAny(WildcardChars) < 0` before running variable replacement loops and string splitting bypasses expensive dictionary enumerations and array allocations for literal paths.
**Action:** Always check for character triggers (`%`, `*`, `?`) before applying string replacement routines or regex/splitting operations in path expansion logic.
