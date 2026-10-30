# When globbing returns original filespec

File specifications are matched by the internal `GlobPattern` implementation, not regular
expressions. File enumeration, item matching, and `MSBuildGlob` share the same matcher.
It preserves historical casing, recursive-directory captures, and filename rules
(including `*.*` and trailing dots). The legacy regex implementation is retained only
in test and benchmark code as a compatibility oracle.

Original itemspec is returned when:
- illegal filespec contains
	- both wildcards and escaped wildcards (`%2a`, `%3f`)
	- illegal file chars
	- `...`
	- a `:` anywhere but the second character
	- a `..` after a wildcard
	- a path fragment which contains `**` and other characters (e.g. `/**f/`)
- Any IO related exception is thrown during file walking: https://github.com/dotnet/msbuild/blob/c1d949558b4808ca9381d09af384b66b31cde2b2/src/Shared/ExceptionHandling.cs#L125-L140
  - System.UnauthorizedAccessException and System.Security.SecurityException from directory enumeration (Directory.EnumerateFileSystemEntries) are ignored, and the files / directories which cause it are excluded from the results.
