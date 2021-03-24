# Managing tools with their own parallelism in MSBuild

MSBuild supports building projects in parallel using multiple processes. Most users opt into `Environment.ProcessorCount` parallelism at the MSBuild layer.

In addition, tools sometimes support parallel execution. The Visual C++ compiler `cl.exe` supports `/MP[n]`, which parallelizes compilation at the translation-unit (file) level. If a number isn't specified, it defaults to `NUM_PROCS`.

When used in combination, `NUM_PROCS * NUM_PROCS` compiler processes can be launched, all of which would like to do file I/O and intense computation. This generally overwhelms the operating system's scheduler and causes thrashing and terrible build times.

As a result, the standard guidance is to use only one multiproc option: MSBuild's _or_ `cl.exe`'s. But that leaves the machine underloaded when things could be happening in parallel.

## Design

`IBuildEngine` will be extended to allow a task to indicate to MSBuild that it would like to consume more than one CPU core (`RequestCores`). These will be advisory only — a task can still do as much work as it desires with as many threads and processes as it desires.

A cooperating task would limit its own parallelism to the number of CPU cores MSBuild can reserve for the requesting task.

`RequestCores(int requestedCores)` will always return a positive value, possibly less than the parameter if that many cores are not available. If no cores are available at the moment, the call blocks until at least one becomes available. The first `RequestCores` call made by a task is guaranteed to be non-blocking, though, as at minimum it will return the "implicit" core allocated to the task itself. This leads to two conceptual ways of adopting the API. Either the task calls `RequestCores` once, passing the desired number of cores, and then limiting its parallelism to whatever the call returns. Or the task makes additional calls throughout its execution, perhaps as it discovers more work to do. In this second scenario the task must be OK with waiting for additional cores for a long time or even forever if the sum of allocated cores has exceeded the limit defined by the policy.

All resources acquired by a task will be automatically returned when the task's `Execute()` method returns, and a task can optionally return a subset by calling `ReleaseCores`. Additionally, all resources will be returned when the task calls `Reacquire` as this call is a signal to the scheduler that external tools have finished their work and the task can continue running. It does not matter when the resources where allocated - whether it was before or after calling `Yield` - they will all be released. Depending on the scheduling policy, freeing resources on `Reacquire` may prevent deadlocks.

The exact core reservation policy and its interaction with task execution scheduling is still TBD. The pool of resources explicitly allocated by tasks may be completely separate, i.e. MSBuild will not wait until a resource is freed before starting execution of new tasks. Or it may be partially or fully shared to prevent oversubscribing the machine. In general, `ReleaseCores` may cause a transition of a waiting task to a Ready state. And vice-versa, completing a task or calling `Yield` may unblock a pending `RequestCores` call issued by a task.

## Example 1

In a 16-process build of a solution with 30 projects, 16 worker nodes are launched and begin executing work. Most block on dependencies to projects `A`, `B`, `C`, `D`, and `E`, so they don't have tasks running holding resources.

Task `Work` is called in project `A` with 25 inputs. It would like to run as many as possible in parallel. It calls

```C#
int allowedParallelism = BuildEngine8.RequestCores(Inputs.Count); // Inputs.Count == 25
```

and gets up to `16`--the number of cores available to the build overall.

While `A` runs `Work`, projects `B` and `C` run another task `Work2` that also calls `RequestCores` with a high value. Since `Work` in `A` has reserved all cores, the calls in `B` and `C` may return only 1, indicating that the task should not be doing parallel work. Subsequent `RequestCores` may block, waiting on `Work` to release cores (or return).

When `Work` returns, MSBuild automatically returns all resources reserved by the task to the pool. At that time blocked `RequestCores` calls in `Work2` may unblock.

## Implementation

The `RequestCores` and `ReleaseCores` calls are marshaled back to the scheduler via newly introduced `INodePacket` implementations. The scheduler, having full view of the state of the system - i.e. number of build requests running, waiting, yielding, ..., number of cores explicitly allocated by individual tasks using the new API - is free to implement an arbitrary core allocation policy. In the initial implementation the policy will be controlled by a couple of environment variables to make it easy to test different settings.
