## 2026-09-08 - Developer Cleanup Directory Tree Enumeration Optimization
**Learning:** In recursive directory scanners (`DeveloperCleanupViewModel`), performing per-directory `File.GetAttributes` syscalls to filter reparse points (junctions/symlinks) and iterating over `List<string>` targets creates significant I/O latency and CPU string allocation overhead. Passing `EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true }` directly to `Directory.EnumerateDirectories` / `DirectoryInfo.EnumerateDirectories` delegates reparse point skipping natively to the OS kernel enumerator, and using `HashSet<string>(StringComparer.OrdinalIgnoreCase)` provides $O(1)$ target name matching.
**Action:** Always delegate reparse point filtering to OS `EnumerationOptions` during directory tree walks and use case-insensitive `HashSet<string>` for target folder matching.

## 2026-09-05 - Winapp2.ini Parser Optimization
**Learning:** Parsing large INI files (like Winapp2.ini ~1.4MB with ~30,000 lines) using `string.Split('\r', '\n')` and compiled `Regex.IsMatch` creates significant memory allocation overhead (>7MB per parse) and high CPU latency (~30-35ms). Replacing `string.Split` with `MemoryExtensions.EnumerateLines()` over `ReadOnlySpan<char>` and `Regex` with direct `ReadOnlySpan<char>` prefix and ASCII digit validation reduces parse times by ~4x (~7.4ms) and eliminates line splitting / regex matching memory allocations.
**Action:** When parsing line-based text formats in .NET, prefer `ReadOnlySpan<char>` line enumeration and custom character validation methods over `string.Split()` and `Regex`.

## 2026-09-04 - Fast-pathing PathExpander string operations
**Learning:** In `PathExpander`, checking `path.IndexOf('%') >= 0` and `path.IndexOfAny(WildcardChars) < 0` before running variable replacement loops and string splitting bypasses expensive dictionary enumerations and array allocations for literal paths.
**Action:** Always check for character triggers (`%`, `*`, `?`) before applying string replacement routines or regex/splitting operations in path expansion logic.
