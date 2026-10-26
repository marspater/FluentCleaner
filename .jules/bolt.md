## 2026-09-06 - Direct handle file size probing in CleaningService
**Learning:** Requesting `FILE_READ_ATTRIBUTES` (0x0080) in addition to `DELETE` during `CreateFileW` enables `RandomAccess.GetLength(handle)` directly on open `SafeFileHandle` instances, bypassing `FileInfo` heap allocations and eliminating duplicate `GetFileAttributesExW` Win32 syscalls for every scanned file.
**Action:** When probing files with open native file handles, combine `FILE_READ_ATTRIBUTES` into desired access to read file length via `RandomAccess.GetLength(handle)` without re-opening or re-statting paths.

## 2026-09-05 - Winapp2.ini Parser Optimization
**Learning:** Parsing large INI files (like Winapp2.ini ~1.4MB with ~30,000 lines) using `string.Split('\r', '\n')` and compiled `Regex.IsMatch` creates significant memory allocation overhead (>7MB per parse) and high CPU latency (~30-35ms). Replacing `string.Split` with `MemoryExtensions.EnumerateLines()` over `ReadOnlySpan<char>` and `Regex` with direct `ReadOnlySpan<char>` prefix and ASCII digit validation reduces parse times by ~4x (~7.4ms) and eliminates line splitting / regex matching memory allocations.
**Action:** When parsing line-based text formats in .NET, prefer `ReadOnlySpan<char>` line enumeration and custom character validation methods over `string.Split()` and `Regex`.

## 2026-09-04 - Fast-pathing PathExpander string operations
**Learning:** In `PathExpander`, checking `path.IndexOf('%') >= 0` and `path.IndexOfAny(WildcardChars) < 0` before running variable replacement loops and string splitting bypasses expensive dictionary enumerations and array allocations for literal paths.
**Action:** Always check for character triggers (`%`, `*`, `?`) before applying string replacement routines or regex/splitting operations in path expansion logic.
