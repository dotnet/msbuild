# Evaluation performance investigations
In the current effort to improve performance of MSBuild, we identified evaluation as one of the focus areas of this effort. Evaluation is the first step when loading or building, and it determines references, how projects are connected and what needs to be build. Because of this it runs in every MSBuild scenario, from solution load and design-time builds in Visual Studio, to up-to-date builds or full builds in VS or on the command line.

## Description

A measured breakdown of a full evaluation now exists in
[evaluation-cost-breakdown.md](../../evaluation-cost-breakdown.md), together with a reusable harness
under [`src/MSBuild.Benchmarks/Analysis`](../../../src/MSBuild.Benchmarks/Analysis/README.md).

The headline results for a restored `dotnet new console`:

* A cold evaluation costs about 90 ms and allocates 13.6 MB; a warm one costs 5 ms and allocates
  1.5 MB. Cold and warm are effectively different problems.
* A cold evaluation is dominated by *acquiring* project XML (47%) and SDK resolution (21%), not by
  the evaluation passes themselves.
* Opening handles and querying file attributes costs roughly six times as much as reading the bytes.
* About a fifth of a cold evaluation is garbage collection, caused by allocating 13.6 MB to read
  1.4 MB of XML.

The prioritized list of optimization opportunities is at the end of that document.

## Goals and Motivation
We are trying to make evaluation phase of the build more performant, since it is almost always executed any performance gain becomes noticeable. A performant evaluation phase would decrease build times in general, in CI cases it frees up resources, and in individual cases it can increase dev-loop performance by making up-to-date and incremental builds go faster.

In this moment we are still in investigation phase, the objective is to make the markers we have in code more accessible to the team, so we can idetentify low hanging fixes, and improvement areas when testing builds within PerfStar.

Constraint - needs to work as it does today, but faster. We may be able to break some edge cases.

## Risks
One of the big risks is accidentally changing the current behaviour of evaluation. One of the constraints of improvement is that evaluation has the same behavior, with the exception of edge cases where we can sometimes change it.

## Plan
Markers now cover import resolution (`EvaluateImport`) and target registration (`ReadTargetElements`,
`ComputeTargetMappings`) in addition to the existing pass, document-load, parse, glob and SDK
resolution markers, so PerfStar and any `dotnet-trace` capture can attribute evaluation time by
phase. See [event-source.md](../event-source.md) for the full list.

The measured breakdown points at caching rather than at the evaluation passes: loading and parsing
project XML plus SDK resolution is roughly three quarters of a cold evaluation, and a warm evaluation
is 18x faster purely because those results are reused. Larger changes along those lines, such as
persisting the parsed construction model or SDK resolution results across processes, are the
highest-value follow-ups.
