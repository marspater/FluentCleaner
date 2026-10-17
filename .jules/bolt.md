## 2026-09-06 - Native EnumerationOptions for Directory Traversal
**Learning:** Calling `File.GetAttributes(dir)` or `try { (sub.Attributes & FileAttributes.ReparsePoint) }` inside recursive directory loops issues a separate `GetFileAttributesExW` Win32 syscall and throws managed `UnauthorizedAccessException` instances for every subdirectory. Passing `EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true }` directly to `Directory.EnumerateDirectories` / `GetDirectories` delegates filtering to the native OS enumerator, eliminating thousands of per-directory Win32 syscalls and LINQ allocations.
**Action:** Always pass `EnumerationOptions` with `AttributesToSkip = FileAttributes.ReparsePoint` and `IgnoreInaccessible = true` when scanning directory trees in .NET instead of manual per-directory `File.GetAttributes` calls.

## 2026-09-05 - Winapp2.ini Parser Optimization
**Learning:** Parsing large INI files (like Winapp2.ini ~1.4MB with ~30,000 lines) using `string.Split('\r', '\n')` and compiled `Regex.IsMatch` creates significant memory allocation overhead (>7MB per parse) and high CPU latency (~30-35ms). Replacing `string.Split` with `MemoryExtensions.EnumerateLines()` over `ReadOnlySpan<char>` and `Regex` with direct `ReadOnlySpan<char>` prefix and ASCII digit validation reduces parse times by ~4x (~7.4ms) and eliminates line splitting / regex matching memory allocations.
**Action:** When parsing line-based text formats in .NET, prefer `ReadOnlySpan<char>` line enumeration and custom character validation methods over `string.Split()` and `Regex`.

## 2026-09-04 - Fast-pathing PathExpander string operations
**Learning:** In `PathExpander`, checking `path.IndexOf('%') >= 0` and `path.IndexOfAny(WildcardChars) < 0` before running variable replacement loops and string splitting bypasses expensive dictionary enumerations and array allocations for literal paths.
**Action:** Always check for character triggers (`%`, `*`, `?`) before applying string replacement routines or regex/splitting operations in path expansion logic.
