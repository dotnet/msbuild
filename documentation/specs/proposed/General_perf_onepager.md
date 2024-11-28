# General performance improvements
MSBuild is the main tool used for building .NET projects. It is used by Visual Studio, the .NET CLI, and other build tools. The performance of MSBuild is critical to the productivity of developers. This document outlines our goals to improve overall performance of MSBuild.

## Goals and Motivation

We are aiming for:
 - Searching opportunities to improve the build times.
 - Reducing MSBuild overhead in the Visual Studio IDE.
 - Collecting data to understand the current performance bottlenecks.

This document does not cover specific performance improvements, but rather outlines the general goals and motivation for the performance improvements.


## Impact
    
    - Opening project/solution, branch switching and other operations using MSBuild code in VS should be less impacted by the MSBuild.
    - Overall build times should be reduced. 
    - Even a small improvement can save a lot of time and computing resources across all builds done daily worldwide.

## Stakeholders

    - Chet Hust (PM) - as a customer advocate
    - David Kean - as a VS performance expert

## Risks

    - Performance improvements might not be as significant as expected.
    - We can break existing functionality while making changes.
    - We can thow away some ideas due to technical limitations or complexity/improvements ratio.

## Cost

Performance improvements are a long-term investment. We need to balance the cost of the improvements with the expected benefits.
We will need to invest time in collecting data, analyzing the data, and implementing improvements.

Our goal in this scope is to find small and medium size opprotunities. Bigger functionalities such as evaluation caching and RAR caching are described in separate documents.

## Plan
    
    - Collect data on the current performance bottlenecks.
    - Identify opportunities for improvements.
    - Implement improvements in time-boxed iterations.
