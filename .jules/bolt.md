## 2026-09-06 - Native OS Directory Enumeration and Direct SafeFileHandle Sizing
**Learning:** During recursive file system scans, checking reparse points via `File.GetAttributes(dir)` issues an extra `GetFileAttributesW` Win32 syscall for every directory encountered. Passing `EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true }` delegates filtering to the native OS enumerator (`WIN32_FIND_DATAW`), eliminating per-directory syscalls and LINQ allocations. Furthermore, querying file sizes via `RandomAccess.GetLength(handle)` on open handles (`DELETE | FILE_READ_ATTRIBUTES`) avoids `FileInfo` heap allocations and secondary file path metadata lookups.
**Action:** Prefer `EnumerationOptions` with `AttributesToSkip = FileAttributes.ReparsePoint` for directory scans and `RandomAccess.GetLength(handle)` when probing open file handles.

## 2026-09-05 - Winapp2.ini Parser Optimization
**Learning:** Parsing large INI files (like Winapp2.ini ~1.4MB with ~30,000 lines) using `string.Split('\r', '\n')` and compiled `Regex.IsMatch` creates significant memory allocation overhead (>7MB per parse) and high CPU latency (~30-35ms). Replacing `string.Split` with `MemoryExtensions.EnumerateLines()` over `ReadOnlySpan<char>` and `Regex` with direct `ReadOnlySpan<char>` prefix and ASCII digit validation reduces parse times by ~4x (~7.4ms) and eliminates line splitting / regex matching memory allocations.
**Action:** When parsing line-based text formats in .NET, prefer `ReadOnlySpan<char>` line enumeration and custom character validation methods over `string.Split()` and `Regex`.

## 2026-09-04 - Fast-pathing PathExpander string operations
**Learning:** In `PathExpander`, checking `path.IndexOf('%') >= 0` and `path.IndexOfAny(WildcardChars) < 0` before running variable replacement loops and string splitting bypasses expensive dictionary enumerations and array allocations for literal paths.
**Action:** Always check for character triggers (`%`, `*`, `?`) before applying string replacement routines or regex/splitting operations in path expansion logic.
