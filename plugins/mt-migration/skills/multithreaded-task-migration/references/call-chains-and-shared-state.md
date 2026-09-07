# Call chains, analyzers, and shared state

Use this reference when task-local edits can hide defects behind bases, helper calls, abstractions, caches, or engine-owned objects.

## Build the relevant graph

Start from the affected task's constructors/initializers, parameter setters when relevant, `Execute`, overridden lifecycle methods, callbacks, cancellation, and cleanup. Audit inherited implementations as well as methods in the diff.

Follow each relevant input to every consumer. Record the input representation, directory/environment it relies on, and the point where that contract changes. A conversion at three use sites does not repair the fourth.

For interface, delegate, virtual, or dependency-injected calls, identify the implementation used by the scenario. A call to `IFileSystem` or a command factory can consume a filesystem path without a visible `System.IO` call in the task body.

Use the earliest semantically valid resolution boundary. Keep optional-input guards and exception handling intact. When an abstraction intentionally accepts a relative path plus an explicit base, preserve that contract rather than blindly changing the argument.

Stop following a branch when its relevant behavior is established by source or a versioned API contract. If the implementation is unavailable, report the boundary and the missing fact; do not invent a leaf or recursively review every possible implementation in the ecosystem.

## Bases are part of the task

The concrete attribute is non-inheritable, but base code still executes in the selected host. A base can own file inputs, environment-dependent defaults, static initialization, or a helper that reaches I/O.

Where a shared base owns the contract, plumbing its supported `TaskEnvironment` through that base can fix all relevant derived tasks without duplicating conversion. Do not hide an existing base property or add a second environment object accidentally.

Do not assert that every unannotated base is invisible to analysis. Direct-rule scope depends on interfaces/attributes and configuration, and the current analyzer also has a transitive pass. Inspect both the base and the configured analysis behavior.

## Leaf categories are investigation prompts

| Category | Inspect |
| --- | --- |
| Process CWD/environment | Whether the value is project-sensitive, when it is captured, and whether the supplied environment should be used |
| Filesystem consumers | Direct I/O plus assembly metadata, XML, archives, certificates, images, assembly loading, and wrapper APIs |
| Abstractions | Actual interface/delegate implementation, explicit base arguments, returned path representation |
| Failure handlers | Whether a catch means failure, fallback, or a semantic answer; actual exception/filter match |
| Child processes | Executable lookup, working directory, command/response-file semantics, environment overrides, cancellation ownership |
| Shared runtime state | Static initialization, mutable caches, registrations, assembly loads, global streams/settings |
| Nested tasks | Construction-time dependencies, environment injection, BuildEngine/HostObject and lifetime |

Check overloads. A string can be XML content, an element name, or a path; a stream overload usually moves path responsibility to the code that opened the stream. Do not flag all string arguments to a type as filesystem inputs.

Likewise, killing the build host or changing process-wide thread-pool policy is not equivalent to cancelling an owned child process through an established ToolTask cleanup path. Identify the process and side effect rather than using the API name as a severity rule.

## Use analyzer evidence accurately

Check the analyzer package/source version, configured scope, suppressions, generated-code handling, and the rule that actually ran. Do not install/enable a new analyzer or change warning policy without authority.

The inspected MSBuild analyzer includes direct and transitive analysis. `SharedAnalyzerHelpers.ResolveFilePathTypes` already includes XML and archive types beyond the old core-I/O list. `TransitiveCallChainAnalyzer` builds a compilation-wide graph and has a bounded traversal.

Therefore, old claims that XML/ZIP calls never produce diagnostics, that all unannotated bases are excluded, or that transitive analysis does not exist are not valid universal rules.

Static analysis is useful evidence within its model, not a proof of complete runtime safety. Examine separately compiled helpers, runtime implementations of abstractions, cache lifetimes, compound operations, and observable exception behavior when the configured analysis does not establish them.

Preserve these concrete limitations without unsupported incident counts or blanket claims that analyzer results have no value.

## Static and memoized state

For each cached result, identify:

- The owner and lifetime: invocation, project, build, thread node, process, or longer-lived host.
- Every input that affects the result: project directory, relevant environment, options, tool/runtime version, filesystem state.
- Whether the key captures those inputs and whether comparison/normalization preserves the contract.
- Whether values are immutable/shareable, and how they are invalidated and disposed.
- Whether creating a value performs a side effect or failure memoization.

A `ConcurrentDictionary` does not fix an incomplete key, a value captured from the first caller's CWD, stale data, or side effects in its value factory. Do not replace every cache with one mechanically.

Failure caching is not inherently forbidden. It is safe only under the same key, lifetime, and invalidation argument as success caching. A missing tool/file in one environment must not automatically become a cached failure for another environment.

## Registered task objects: concurrency is not just static fields

`IBuildEngine4.GetRegisteredTaskObject` and `RegisterTaskObject` reach an engine-owned cache. A task can share state without declaring any static field itself.

The inspected `RegisteredTaskObjectCacheBase` uses `TryGetValue` and **`TryAdd`**, not replacement assignment. Registration returns no success flag to the task. A losing registration does **not** overwrite the winning entry; do not reason as if it does.

The following sequence is still not one atomic computation:

```text
instance A: get -> missing
instance B: get -> missing
instance A: compute / perform side effect
instance B: compute / perform side effect
instance A: register -> one value is retained
instance B: register -> existing value remains
```

| What is cached | Required correctness argument |
| --- | --- |
| Pure immutable value | Complete key, acceptable duplicate computation, equivalent result, no leaked resources |
| Resource-owning value | Ownership/disposal of a candidate that is not retained, and lifetime of the retained value |
| Exactly-once side effect | Atomic decision/action or another proven protocol; concurrent storage alone is insufficient |
| Failure/default answer | Correct environment/key, lifetime, invalidation, and consumers of the cached failure |

Inspect the caller's behavior after registration: does it keep using its own candidate or retrieve the retained object? Do not assume the engine owns a candidate it did not retain.

Build lifetime and AppDomain/process lifetime differ. The inspected build cache is instance-owned; the AppDomain cache is static. Determine the actual hosting/cache scope before claiming "once per build" across processes or nodes.

Use an existing synchronization/ownership protocol when one applies. Do not invent a public `GetOrAddRegisteredTaskObject` API, assume value factories run once, or prescribe a lock without knowing which instances share it.

## Choosing isolation instead of annotation

If a concrete task cannot preserve its intended shared-process behavior, retaining an unannotated route is legitimate. Record why, such as an unresolved exactly-once requirement or process-global dependency.

Removing the attribute is not by itself proof of singleton execution, correction of an incomplete cache key, or elimination of cross-process side effects. Evaluate the actual scheduling/hosting contract and any remaining changes.

## Finding evidence

A confirmed finding needs a reachable chain, actual input/host conditions, an effect on behavior, and a repair tied to that effect. For example:

```text
Task.Execute -> base helper -> path-consuming API
Input: project-relative file; process CWD differs from the project directory.
Effect: the observed/derived probe inspects the wrong file or takes a matching fallback.
Evidence: responsible call sites plus the relevant assertion or implementation contract.
```

A potential hazard without established reachability or impact belongs in a clearly labeled unresolved-boundary note, not an automatic blocker.

## Source landmarks

Resolve these in the reviewed checkout: `src\TaskAnalyzer\MultiThreadableTaskAnalyzer.cs`, `TransitiveCallChainAnalyzer.cs`, `SharedAnalyzerHelpers.cs`; `src\Shared\RegisteredTaskObjectCacheBase.cs`; and `src\Build\BackEnd\Components\RequestBuilder\TaskHost.cs` registration callbacks. The actual task bases, factories, and cache consumers complete the argument.
