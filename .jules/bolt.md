## 2026-09-06 - RandomAccess File Size Querying & Native Reparse Filtering
**Learning:** Querying file size via `new FileInfo(path).Length` when a `SafeFileHandle` is already open creates unnecessary `FileInfo` heap allocations and redundant `GetFileAttributesExW` Win32 syscalls per file. Using `RandomAccess.GetLength(handle)` reads the length directly from the open handle. Additionally, passing `EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint }` directly to `Directory.EnumerateDirectories` offloads junction skipping natively to the OS directory enumerator (`FindFirstFileExW`), eliminating per-directory `File.GetAttributes` syscalls and LINQ allocations.
**Action:** When working with open `SafeFileHandle` instances, use `RandomAccess.GetLength` instead of instantiating `FileInfo`. When filtering directory enumerations for reparse points, pass `EnumerationOptions` directly to `Directory.EnumerateDirectories`.

## 2026-09-05 - Winapp2.ini Parser Optimization
**Learning:** Parsing large INI files (like Winapp2.ini ~1.4MB with ~30,000 lines) using `string.Split('\r', '\n')` and compiled `Regex.IsMatch` creates significant memory allocation overhead (>7MB per parse) and high CPU latency (~30-35ms). Replacing `string.Split` with `MemoryExtensions.EnumerateLines()` over `ReadOnlySpan<char>` and `Regex` with direct `ReadOnlySpan<char>` prefix and ASCII digit validation reduces parse times by ~4x (~7.4ms) and eliminates line splitting / regex matching memory allocations.
**Action:** When parsing line-based text formats in .NET, prefer `ReadOnlySpan<char>` line enumeration and custom character validation methods over `string.Split()` and `Regex`.

## 2026-09-04 - Fast-pathing PathExpander string operations
**Learning:** In `PathExpander`, checking `path.IndexOf('%') >= 0` and `path.IndexOfAny(WildcardChars) < 0` before running variable replacement loops and string splitting bypasses expensive dictionary enumerations and array allocations for literal paths.
**Action:** Always check for character triggers (`%`, `*`, `?`) before applying string replacement routines or regex/splitting operations in path expansion logic.
