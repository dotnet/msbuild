# Investigation TODO

This file is the resumable task log for the in-process parallel-build bottleneck investigation.

## Current status

- branch: `feature/parallel-bottleneck-scan`
- phase: Wave 2 consolidated stopping point
- scope: engine/framework/shared only
- mode: read-only source scan

## TODO items

- [x] Create working branch and commit the approved plan.
- [x] Create resumable TODO log in the repo.
- [x] Scaffold investigation output files and logbook locations.
- [x] Run Wave 1 broad scan with at most 4 concurrent subagents.
- [x] Fold Wave 1 results into `findings.md`.
- [x] Rank and normalize candidates into canonical bottlenecks.
- [x] Select Wave 2 deep-dive candidates.
- [x] Run Wave 2 deep dives with at most 4 concurrent subagents.
- [x] Write detailed reports for bottlenecks that justify escalation.
- [x] Write short closeout summaries in logbooks for candidates that do not justify a full report.
- [x] Produce `summary.md` grouped by stage with a short overall top-findings section.

## Active assignments

- No active deep dives. Current phase is consolidated and documented.

## Activity log

- Wave 1 broad scan launched with 4 concurrent scope agents.
- Shared-file consolidation is being kept in the main session to avoid edit conflicts.
- Wave 1 evaluation logbook completed and reviewed.
- Wave 1 logging logbook completed and reviewed.
- Wave 1 execution logbook completed and reviewed.
- Wave 1 framework logbook completed and reviewed.
- Wave 1 complete. Selected Wave 2 deep-dive set: `ProjectRootElementCache`, `ConditionEvaluator`, `BuildManager/Scheduler coordination`, and `LoggingService`.
- Wave 2 first batch launched with 4 concurrent deep dives.
- Wave 2 deep dive completed: `ProjectRootElementCache` (current judgment: medium-high, strongest on startup-burst/common-import serialization).
- Wave 2 deep dive completed: `ConditionEvaluator` (current judgment: medium, definite per-key serialization but likely selective rather than universal).
- Wave 2 deep dive completed: `BuildManager/Scheduler` (current judgment: medium-high, strongest as serialized control-plane / queue-backlog risk rather than lock convoy alone).
- Wave 2 deep dive completed: `LoggingService` (current judgment: medium, real single-lane serialization point but practical impact depends heavily on event volume and logger mix).
- Wave 2 second batch selected and launched: `ToolLocationHelper.s_locker`, `ResultsCache`, `BinaryLogger`, `CoreClrAssemblyLoader._guard`.
- Wave 2 deep dive completed: `BinaryLogger` (current judgment: medium-high when `/bl` is enabled, mostly as expensive logger work on the shared logging lane rather than raw lock contention alone).
- Wave 2 deep dive completed: `CoreClrAssemblyLoader._guard` (current judgment: medium-low, real coarse lock but mostly startup/plugin-initialization scoped and instance-local rather than process-global).
- Wave 2 deep dive completed: `ToolLocationHelper.s_locker` (current judgment: medium, real global-lock risk concentrated in SDK/platform/VS/framework-discovery APIs; less concerning for split-phase reference-assembly APIs).
- Wave 2 deep dive completed: `ResultsCache` (current judgment: medium, coarse shared cache lock with non-trivial work inside, but likely secondary to the BuildManager/scheduler control plane).
- Wave 2 third batch selected and launched: `Main-node SDK resolution funnel`, `BuildEventArgTransportSink` + `LogMessagePacket`, `ProjectImportsCollector`, `In-proc BuildRequestEngine`.
- Wave 2 deep dive completed: `BuildEventArgTransportSink` + `LogMessagePacket` (current judgment: low for pure one-process builds; mainly a medium multi-node/OOP transport tax rather than a top in-proc bottleneck).
- Wave 2 deep dive completed: `Main-node SDK resolution funnel` (current judgment: medium, real bursty shared wait via same-SDK `Lazy<SdkResult>` fan-in and cold manifest/resolver loading).
- Wave 2 deep dive completed: `In-proc BuildRequestEngine` (current judgment: medium, real localized serial coordinator per in-proc node, but weakened by multiple in-proc nodes in MT mode).
- Wave 2 deep dive completed: `ProjectImportsCollector` (current judgment: medium, mostly a serialized background import-capture pipeline and shutdown-tail cost under `/bl` rather than a large foreground lock bottleneck).
- Reached a strong stopping point after 12 deep dives. Remaining queued candidates are lower-priority follow-ups, not blockers for the current report set.

## Resume notes

- Reuse the four existing scope agents if available.
- Avoid tasks/`src\\Tasks\\**` in this phase.
- Keep detailed shared output edits centralized after logbooks are collected to avoid file conflicts.
- Deferred follow-up candidates, if a later phase is needed: `BuildEnvironmentHelper`, `FileMatcher` static glob cache, `Project-cache plugin initialization`, `LogMessagePacketBase.s_writeMethodCache`.
