# Evaluation input observation

MSBuild can record the external inputs consumed by a project evaluation by setting
`MSBUILDRECORDEVALUATIONINPUTS=1` before the process starts. Recording is disabled by
default and does not reuse, cache, or otherwise change evaluation results.

The recorded manifest is internal engine state associated with the evaluated
`Project` or `ProjectInstance`. It contains:

- the evaluation identity, including project path, global and command-line
  properties, toolset state, working directory, culture, and parser configuration;
- files and directories that evaluation read, enumerated, or probed, including
  authoritative timestamp and length observations for project XML;
- environment and registry reads made through property functions;
- immutable snapshots of SDK resolver inputs and results; and
- a reason when an input cannot be observed safely enough for future reuse.

Recording is conservative. In-memory or dirty project XML, symbolic links, host or
process-wide file-system caches, partial evaluation, volatile or unknown property
functions, item timestamp metadata, registry reads, failed SDK resolution, and
evaluation diagnostics make a manifest non-cacheable. This status is observation
metadata only; evaluation continues with its existing behavior.

The switch is intended for diagnostics and development of later evaluation-cache
layers. It does not enable an evaluation cache or provide a compatibility contract
for consuming the internal manifest.
