# TaskHost lifetime evidence

This records process observations for [dotnet/msbuild#14584](https://github.com/dotnet/msbuild/pull/14584).
It distinguishes reusable sidecars from pooled compatibility TaskHosts and explicitly requested, non-reusable TaskHosts.

## Method

Windows x64, 11 September 2026. The candidate is built from `b46fdf6c4d` plus the lifetime fixes under review, using SDK `11.0.100-rc.1.26420.103`. Both .NET and .NET Framework bootstrap layouts are used. The candidate negotiates packet version 6.

Each scenario uses a unique handshake salt. Tasks report their own PIDs. The harness inspects those PIDs, their parent PIDs, and their command lines. A server uses `/nodemode:8`; a TaskHost uses `/nodemode:2`. Exit checks wait up to ten seconds. Cleanup kills only processes recorded by that scenario, **after** the lifetime assertions.

The worker test replaces the worker provider's retention threshold with `int.MaxValue` through the component factory. This prevents unrelated processes on the machine from making the test lose its worker before it can test cross-build reuse. It does not change TaskHost ownership or shutdown behavior.

Raw records are in the session's `files\lifetime-e2e` directory. The process matrix is `run-process-matrix.ps1`; version-skew checks use `check-legacy-parent.ps1`. The table below records the first complete candidate run (`run-20260911-171252`) and its extension (`run-20260911-172422`), not timing averages. All sixteen process scenarios also passed against the Release build in `run-20260911-182353`.

After the review fixes, the final Release run (`run-20260911-200901`) passed all **eighteen** matrix scenarios. Final targeted tests passed **190** cases across both TFMs, with one expected platform skip. Nested-crash replacement and cross-version reconnection are recorded separately below.

## Observed process lifetimes

| Scenario | Expected | Observed |
|---|---|---|
| Two resident `-mt -nr:true` builds | Same server and sidecar remain live between builds; both exit after `build-server shutdown --msbuild`. | Server **31364**, sidecar **22024**, reused by both builds. Both exited. Builds took 6366 ms and 265 ms. |
| `-mt`, server disabled | Sidecar exits with the command-line owner. | Owner **16212**, sidecar **17688**. Both exited. |
| Ownership wave disabled at `18.12` | Reusable TaskHost remains pooled after its launcher exits. | Launcher **22560** exited; TaskHost **26792** remained live through the exit-check window. Cleaned up afterward. |
| Explicit `TaskFactory="TaskHostFactory"` | Each build's TaskHost exits; the resident server stays available. | Server **31600** reused. TaskHosts **32536** and **37632** each exited after their build. |
| Transient `-mt -nr:false` build | Separate server is used; server and TaskHost exit after the build. | Client **31824**, server **37148**, TaskHost **43504**. Server and TaskHost exited. |
| Transient build beside an existing resident | Transient does not stop or adopt resident processes. | Resident server/sidecar **31708/27792** stayed live while transient **22968/41392** exited. The resident pair then exited on shutdown. |
| Idle owner crash | Connected sidecar exits when its server is killed. | Server **39576** killed; sidecar **40516** exited without being killed by the harness. |
| Four idle-sidecar crashes | Server survives and replaces each dead sidecar. | Server **37860** survived children **34168**, **24556**, **35048**, and **33700**. Each was terminated separately; the next build completed with a replacement. |
| x64 Framework launcher, x86 task | Compatibility TaskHost remains pooled and reusable by another launcher. | Launcher PIDs **21532**, **39084** both used x86 TaskHost **21968**. TaskHost remained live after both builds. |
| Repeated `BuildManager.Dispose()` | Sidecars exit on disposal, before the hosting process exits. | Host **39532** disposed three managers. Children **28252**, **14504**, **35352** all exited while it remained live. |
| Same manager changes reuse from true to false | Retained idle sidecar is terminated, not returned to the pool. | Host **39396** terminated children **31164**, **23916**, **35848** in three rounds. |
| `BuildManager.ShutdownAllNodes()` | Connected sidecars exit while the host remains live. | Host **43504** terminated children **11916**, **27480**, **36984** in three rounds. |
| Worker recreation, then shutdown | Recreated `OutOfProcNode` retains the same sidecar; worker exit terminates it. | Worker/sidecar pairs **38228/32292**, **35156/41012**, **26272/37900** each survived two builds together, then both exited on shutdown. |
| Three overlapping transient builds | Three distinct servers and TaskHosts coexist; all exit after task release. | Simultaneous pairs **32892/30956**, **29856/14680**, **21304/23424**. All exited. Readiness files establish overlap; no fixed startup sleep is used. |
| Adopt a pooled TaskHost born with ownership disabled | A current parent can establish ownership explicitly without depending on the child's cached wave. | TaskHost **32644**, launched by **35740** with wave disabled, was reused by server **38000** with ownership enabled. It exited with server shutdown. |
| Owner crash during an active task | Connection loss initiates shutdown; cooperative task completion permits process exit. | Server **34976** killed while child **21040** waited. Child remained live while the task was blocked, then exited after release. Client reported failure. |
| `ShutdownAllNodes()` during an active build | An idle-node shutdown request must not interrupt an active TaskHost. | Host **25200** called it while children **38984**, **38920**, **33848** were blocked on readiness gates. Each child stayed live, completed after release, and exited on manager disposal. |
| Active TaskHost crashes | Build failure completes and handler registrations are removed. | Host **35732** observed active children **42760**, **31584**, **43744**. Each was killed separately; each submission completed with failure and the provider reported **0** handler registrations afterward. |
| Nested crash followed by a late callback reply | The nested build can create a replacement; the old task's reply cannot reach that replacement. | .NET: **13172** crashed, replacement **34020** served the nested and final tasks. Framework: **33980** crashed, replacement **13868** served both. Both regression tests passed. |

The process-matrix rows above passed. The active-task row is **not** proof that arbitrary hung task code can be forcibly terminated: TaskHost shutdown joins task threads.

## Version compatibility

An older parent can send `NodeBuildComplete(true)`. The boolean does not distinguish permission to pool from an instruction to retain the connection. Packet version 6 therefore adds explicit actions and retains the original one-boolean format for older peers.

| Pair | Expected | Observed |
|---|---|---|
| Installed Framework MSBuild **18.10.0-1.26379.9**, pre-fix TaskHost | Reproduce the ambiguous-completion failure. | Parent **37152**, TaskHost **27296**, launched with `/parentpacketversion:5`. Task executed, but parent did not exit within **20,058 ms**. Both were cleaned up after observation. |
| Same installed parent, fixed TaskHost | Parent completes; compatibility child pools. | Parent **17796** exited **0** in **918 ms**. TaskHost **31484** remained pooled. |
| Current Framework parent, current .NET TaskHost | Cross-runtime sharing remains supported. | Parent **29412** exited **0** in **2256 ms**. TaskHost **22276** remained pooled. |
| Current Framework parent, older SDK **10.0.104** TaskHost | Legacy wire format and pooling remain supported. | Parent **33288** exited **0** in **3090 ms**. TaskHost **27256** remained pooled. |
| Pooled child reconnects to a newer parent | Reuse is preserved when the connected parent differs from the launcher. | Old Framework parent **32616** launched TaskHost **39184** with protocol 5. Current Framework parent **29840** reused **39184**. Both builds exited **0**. |
| Released **18.9.6**, protocol **4 → 6** reconnection | Completion decoding and environment propagation work across the delta-format boundary. | Parents **42724**, **26336** reused TaskHost **468**. Both exited **0**; the task observed `first-parent`, then `second-parent`. |
| Current parent, protocol **6 → 4** reconnection | The same pooled child can return to a released older parent. | Parents **39116**, **24180** reused TaskHost **22356**. Both exited **0**; the task observed `first-parent`, then `second-parent`. |

The installed-parent cases use an ordinary non-SDK `.proj` with `Runtime="NET"` and explicit SDK task-host paths. They do not depend on an older MSBuild accepting a newer project's SDK.

## Cancellation and sender cleanup

Release E2E cancellation tests use a logger that requests cancellation only after the task reports readiness. `Cancel()` sets the task-scoped `AllowFailureWithoutError` flag; `Execute()` verifies it, writes a completion marker, and returns. The tests also require the TaskHost to exit.

| TaskHost | Client / TaskHost PIDs | Observed |
|---|---|---|
| .NET | **43356 / 40688** | Cancellation completed; TaskHost exited; zero logged errors. |
| .NET Framework | **38588 / 26680** | Cancellation completed; TaskHost exited; zero logged errors. |
| .NET Framework, separate AppDomain | **42928 / 26660** | Task confirmed AppDomain isolation. Cancellation completed; TaskHost exited; zero logged errors. |

Sender-thread termination is checked separately by `ClosingAnIdleConnectionStopsItsSender`: an EOF closes the connection, then the test joins the specific packet-drain thread. Total process thread counts are not used as proof of sender cleanup.

## Expert-review iterations

The first expert review found three additional gaps: terminal notification could race handler attachment, idle-node shutdown could interrupt an active build, and task-handler registrations could survive an active TaskHost failure. The fixes share synchronization between attachment and termination, restrict connected-node shutdown to an idle manager, and notify and remove all handlers on terminal failure. Deterministic attachment tests and the two readiness-gated process scenarios above pass on both TFMs.

The second review found that a late callback reply still used a reusable node key and could reach a replacement TaskHost. Each task now keeps the exact connection it acquired for callback replies, cancellation and detachment. The nested-crash regression above passed on both runtimes.

The third expert review found **no remaining substantive PR defects**. It checked the connection-bound sends, attachment/termination synchronization, handler cleanup, idle-only shutdown, protocol gating, provider lifetime, sender cleanup and cancellation context. This is a source-review result, not a claim that the full test suite is green.

## Limits and remaining validation

- The first full Release run inherited `MSBUILDDISABLENODEREUSE=1`; tests requiring reuse failed. Removing that override restored the server and coexistence tests. The portable-task test also needs desktop `MSBuild.exe` on `PATH`; it passed when that prerequisite was supplied.
- A subsequent full run executed 23,170 cases: 22,781 passed, 387 skipped, and two failed. One was the missing desktop-MSBuild prerequisite above. The other was `SolutionGeneratorEscapingProjectFilePaths(useNewParser: true)`, which found an already-loaded global project; it passes in isolation. This full-suite isolation failure is not counted as a passing result.
- These are Windows process observations. They do not replace Linux/macOS CI.
- Each row is a bounded scenario, not proof against every possible task implementation or operating-system failure.
- Compatibility-host pooling is intentional. Such a host is not required to exit with its launcher.
- An idle sidecar's immediate owner can be a worker rather than the top-level server.

The final full Release run completed **23,178** cases: **22,789 passed**, **387 skipped**, and **2 failed**. Both remaining failures are outside the changed lifecycle tests: the solution-generator global-project precondition and a terminal-logger output assertion. They are recorded as failures, not counted as passes. All lifecycle, version negotiation, cancellation, and AppDomain cases passed in that run.
