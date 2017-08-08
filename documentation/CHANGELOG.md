# MSBuild 15.5

## Changes since MSBuild 15.3

* Enabled source-server support pointing to MSBuild's GitHub sources (#2107). Thanks, @KirillOsenkov!
* Define `$(VisualStudioVersion)` by default in more situations (fixed #2258).
* Reduced memory allocation and GC pressure in many situations (#2267, #2271, #2284, #2288, #2293, #2294, #2300)
* Fixed an error that occurred when logging false import conditions (#2261)