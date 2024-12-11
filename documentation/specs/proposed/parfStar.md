# PerfStar
PerfStar is a performance tracking and investigation tool for MSBuild. PerfStar infrastructure captures performance measurements of main MSBuild branch on schedule and allows us to request experimental runs and collect performance of the new changes. The first version of this project is being finalized, with some fixes necessary to run it automatically and according to prerequisites.

## Goals and Motivation
MSBuild currently does not have a lot of performance data outside of Visual Studio performance tracking, which has a lot of variables that are beyond the team's control. PerfStar enables us to measure our performance without interference of elements that the team does not own. As such, we can measure the performance of in-development features and how it will impact build times, as well as have concrete numbers when working on performance improvement tasks.

## Impact
Perfstar's impact is focused on the team. We will be able to track performance with concrete numbers. Because of that the team will be able to take more informed decisions about taking performance improvement work, as well as future and implementation of new features.

## Risks
The risks associated with our dependencies is about Crank, which is owned by the ASP.NET team and we use it to help us with machine setup to run the performance tests. 

## Plan
Investiment for .NET 10:
 1. Making PerfStar execute automatically the way the design doc indicates
    - Around 1 dev week.
2. The PowerBI reporting is working and updating the new information
   - Around 2 dev weeks.
3. New performance tests for new features, and writting docs on how to write those tests. Next feature planned for tests: BuildCheck.
   - Around 3 dev days per feature.
4. Analyze stability of performance tests, and fix any noise found. This will be done through multiple iterations of the same test in the same machine, as well as updating the PowerBI report to handle the new data.
   - Around 2 dev weeks.
5. Add more tests using `msbuild.exe` for build instead of `dotnet build`.
   - Around 1 dev week.
6. Timeboxed collection of feedback from our team, as well as performance investigations that can derive from those.
   - 1 - 2 dev month depending on feedback and requests for improvement from the team.
7. Add more test cases. For example, build time with different verbosity levels.
   - Around 1 dev week.

There are more improvements form PerfStar, but these are not established for .NET 10 yet as they depend on the team's feedback to PerfStar.
1. Add more measurements, like dotnet counter tool.
   - Around 3 dev weeks.
2. Trace collection when specific features are turned on for the test.
   - Around 2 - 3 dev weeks.
3. Report improvements:
   - Compare performance numbers between two different iterations that are not from `main` branch. Around 2 dev weeks.
   - Automatic detection of performance issues, so we don't need to check the reports to see regressions. Around 1 dev month.
   - Run MSBuild API tests, so we can check performance of calls relating to Visual Studio builds. Around 1 dev month.