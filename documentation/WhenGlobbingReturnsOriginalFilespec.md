# When globbing returns original filespec

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

## Recursive directory link checks

The optimized globber avoids full link-target resolution for ordinary directories by querying fresh attributes on fully qualified Windows drive paths without wildcards. Windows reports the entry's own reparse-point flag, even when a parent directory is a link. On a stable filesystem, an entry without that flag has no entry-link target to resolve. Reparse points and inconclusive attribute queries still use the existing final-target resolver and lexical ancestor check.

This path restriction is conservative scope, not a claim that the attribute API cannot handle UNC paths or that drive letters identify local disks: mapped drives also have drive letters. UNC, device and relative paths, other platforms, and wildcard paths retain the resolver route. The probe does not reuse enumeration attributes or cache its result. Separate filesystem observations are not atomic and do not provide a traversal-security guarantee.

This is intended to preserve behavior, so it does not add a new ChangeWave. It remains on the existing optimized globbing route governed by Wave 18.11; legacy routing and its opt-out are unchanged. Deterministic probe-failure tests do not establish real ACL-denial coverage. Junctions and mapped network drives also require additional environment-specific coverage.
