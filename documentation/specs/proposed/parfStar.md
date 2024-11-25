# One Pager Template
*The objective of this one-pager is to provide clear and concise information about a feature or project to foster inclusion and collaboration by enabling anyone who is interested to learn the essential information, such as the project's goals, motivation, impact, and risks. The points and questions in each section are illustrative, they are not intended as a definite list of questions to be answered.*

## Description
 - Short summary of the feature or effort.

New features and fixes for .NET 10 investment.
 - Running it nicely [1 dev week]
 - Report is working [2 dev weeks]
 - Performance tests for new features - making it easier to make these / docs. [3 dev days per feature]
   - BuildCheck perf tests
 - Check Stability of perf tests / fixing those - so there is not a lot of noise around tests [2 dev weeks]
   - Should be done through multiple iterations on a test
   - Report needs to be reworked
 - We can maybe have more `msbuild.exe`. but not VS api [1 dev week]
 - Timeboxed collect and act on feedback from our team. Includes perf investigations that needs to be conducted. [1-2 dev months depending on dev feedback / requests]
 - Add more tests: check logger performance with different verbosities, buildCheck. [1 dev week]

possible directions for perfStar / next versions:
 - More measurements. Like, dotnet counters tool. [3 dev weeks]
 - Trace collection, when something is on automatically turn it on. [2-3 dev weeks]
 - Report improvements: 
   - Comparison between different versions that is not main [2 dev weeks]
   - Automatic detection of perf issues. Don't have someone look at the report to have news about regression. [1 dev month]
 - Run VS API tests? There are some problems that we would need for VS specific tests [4 dev weeks - optional feature]

## Goals and Motivation
 - What are we trying to achieve and why? 
Go fast

## Impact
Guidance: This document should not be an OKR. Let's keep the impact general here.
Questions to consider:
 - What is the impact? 
 - Who will benefit from the feature?
 - How does success look like?
 - Are there any metrics or key results that could be quantified? 

We can be sure to go faster
Enable MSBuild team to track performance, identify low hanging fruits to improve performance, and watch new feature peformance.

## Stakeholders
Questions to consider:
 - Who are the stakeholders? 
 - For projects with concrete stakeholders, once the project is done how does a successful handover to the stakeholder look like? 
Us.


## Risks
Questions to consider:
 - Will the effort or feature cause breaking changes for existing consumers? 
 - Are there any unknowns that might derail the effort? 
 - Are there any dependencies we don’t have control over? 
 - Is there a hard deadline that needs to be met? 
 - What threatens completion of the feature? 
 - What is the impact of failing to deliver the feature?

Dependencies: Crank, which is owned by ASP.NET team. This can cause problems with the machine set-up since they are responsible for this. 

Deadlines:No hard deadlines. Just nice to have for the team.
Threat: Randomization for the team. Security issues that come up.

Impact of delivery failure. Less numbers in performance improvement for MSBuild features.


## Cost
Questions to consider:
 - What is the estimated cost of the end-to-end effort? 
 - How accurate is the cost? 

## Plan
 - High-level milestones, with estimate effort for tasks. 