# Evaluation cache: start in memory

**Decision date: 2026-09-07**

For the [evaluation-cache epic](https://github.com/dotnet/msbuild/issues/14234),
we decided to try an in-memory evaluation cache in MSBuild Server first.
Losing the cache when the server exits, normally after 15 minutes idle, is acceptable.
Whether saving it to disk would be worthwhile remains an open question.

We expect detecting filesystem changes to be the largest invalidation cost.
The [observation prototype](https://github.com/dotnet/msbuild/pull/14940)
[measured metadata-only validation (last-write time + file size)](https://github.com/dotnet/msbuild/pull/14940#issuecomment-5560255488)
at only 5.8-7.5% of fresh evaluation time on OrchardCore and Roslyn: about **93% less time**.
These measurements cover filesystem checks only, not a complete cache hit.
Metadata polling scales with the number of recorded paths and is unlikely to be our production approach.

The [ProjectInstance reuse prototype](https://github.com/dotnet/msbuild/pull/14857)
reduced no-op server build time by about **15-18%** on SmallGraph and OrchardCore.Mvc.Web
using a zero-cost accepting validator.
Together, these separate measurements make in-memory caching worth pursuing;
the benefit with complete invalidation still needs to be measured.
