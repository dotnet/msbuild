# Evaluation snapshot cache foundations

## Status

This document describes the bounded project-instance snapshot cache foundation. The component is not
registered with the build engine and cannot affect normal project evaluation yet. Activation, checked
validation, build-manager lifecycle wiring, and request reuse are deferred to later work.

## Candidate identity

`ProjectInstanceSnapshotCacheKey` identifies a candidate snapshot. Identity includes the normalized
project path; global property names, values, and command-line provenance; effective and explicit
toolset selection; sub-toolset and toolset fingerprints; load settings; interactive and node-count
settings; startup and working directories; culture and UI culture; engine and disabled change-wave
values; environment and parser fingerprints; and the tools path.

The key owns a copy of global properties. Property names and toolset names use MSBuild's
case-insensitive semantics, property values remain case-sensitive, and paths use the platform path
comparer. Key equality only finds a candidate; it never grants permission to reuse a snapshot.

## Entry ownership and validation

Each `ProjectInstanceSnapshotCacheEntry` pairs an immutable `ProjectInstanceSnapshot` with immutable
validation data. `EvaluationInputsSnapshotValidationData` retains the layer-1 input manifest and
accounts for cache-owned file, environment, SDK, and registry payloads. All size arithmetic saturates
through the shared `RetainedSizeEstimator`.

The validator contract is fail-closed. The only production validator in this layer rejects every
candidate. Tests may inject another validator to exercise the standalone component, but no engine path
uses the component.

## Memory and lifecycle

The cache has a 256 MiB default retained-payload budget, configurable for direct component creation
through `MSBUILDPROJECTINSTANCESNAPSHOTCACHEMAXBYTES`. Admission includes the owned key, snapshot, and
validation manifest. Entries larger than the budget are rejected, replacing an existing equivalent
entry removes it, and least-recently-used entries are evicted until a new entry fits.

All lookup, mutation, accounting, and counters are synchronized. `Clear` and component shutdown release
all retained entries. `BuildComponentType.ProjectInstanceSnapshotCache` exists so tests and later
integration can create the component, but no default factory registration or `BuildManager` wiring is
present in this layer.
