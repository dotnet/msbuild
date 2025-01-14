# Evaluation performance investigations
In the current effort to improve performance of MSBuild, we identified evaluation as one of the focus areas of this effort. Evaluation is the first step when loading or building, and it determines references, how projects are connected and what needs to be build. Because of this it runs in every MSBuild scenario, from solution load and design-time builds in Visual Studio, to up-to-date builds or full builds in VS or on the command line.

## Description
Current performance state of evaluation is mostly unkown, as it is not measured in any ways by the team. As such, we are unsure which specific areas can be improve. The investigation about this is necessary so we can identify weaknesses, and possible fixes.

 - We could do profiling
 - Jit compilation of MSBuild itself. 
 - We could cache at eval

Constraint - needs to work as it does today, but faster. We may be able to break some edge cases.

## Goals and Motivation
We are trying to make evaluation phase of the build more performant, since it is almost always executed any performance gain becomes noticeable. A performant evaluation phase would decrease build times in general, in CI cases it frees up resources, and in individual cases it can increase dev-loop performance by making up-to-date and incremental builds go faster.

In this moment we are still in investigation phase, the obejective is to add code markers, so we can idetentify low hanging fixes, and improvement areas when testing builds within PerfStar.

## Risks
One of the big risks is accidentally changing the current behaviour of evaluation. One of the constraints of improvement is that evaluation has the same behavior, with the exception of edge cases where we can sometimes change it.

## Plan
The plan for evaluation at the moment is to add more code markers during execution so we can use PerfStar to have a detailed view of how long each part of evaluation phase takes.

Larger changes to the evaluation are possible and under consideration for future iterations, like trying to cache the evaluation result in MSBuild. However we are focusing on investigation and performance gains with less work at the moment.