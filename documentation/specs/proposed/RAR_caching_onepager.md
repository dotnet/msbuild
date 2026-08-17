## RAR caching
RAR(Resolving of Assembly references) is an optimization for the step in 
every build where we need to gather the graph of assembly references and pass
them to the compiler. This process is highly cacheable as the references
don’t change all that often. Currently we have some limited caching in
place however the way nodes are assigned work results in frequent cache
misses.

### Goals and motivations

1ES team wants to isolate their File I/O related to the RAR caching which is causing
issues to their debugging efforts. This is mostly due to the fact that MSBuild is pulling
files from all nodes at once which results in a tangled mess of IO that is hard to debug.

Our motivation is a possible performance gain however we’re fine with
the change as long as the impact is not negative.

### Impact

The only impact we’re concerned about is the performance. There will be
a tension between the gains from caching and costs due to the IPC from
the process that will act as the cache repository. We need to ensure
that this balance will be a net positive performance wise.

### Stakeholders

1ES team, Tomas Bartonek, Rainer Sigwald

1ES team will provide the initial cache implementation. We will review
their PRs and do the performance evaluations. Handover will be
successful if nothing breaks and we meet our performance requirements
(no regression or better still an improvement).

### Risks

Some time ago Roman Konecny estimated the RAR caching to not be worth it
performance wise. 1ES team claims to have created an implementation that
will either improve or not change the performance. We need to validate
this claim and push back in case we find performance regression.
Thorough testing will be needed especially to ensure the performance
is not impacted.

The risk is having to figure out a different way to help 1ES team to
isolate their File I/Os if the caching hurts the performance. This could
result in a larger project requiring more involvement on our side.

### Cost

Week for reviewing the provided PR. Additional two weeks for performance
testing conditional on the Perfstar infrastructure being functional.
Some communication overhead

## Plan

1ES team creates the PR wih the RAR cache implementation.

We review the PR with a special emphasis on the performance side of
things.
Then we merge the changes. There is no expected follow up beyond the
usual maintenance for our codebase.
