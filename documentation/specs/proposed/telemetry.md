# Telemetry 

We want to implement telemetry collection for VS/MSBuild.exe scenarios where we are currently not collecting any data. VS OpenTelemetry initiative provides a good opportunity to use their infrastructure and library. 
There is some data we collect via SDK which we want to make accessible.

## Goals and Motivation 

We have no data about usage of MSBuild customers in VS.
This limits us in prioritization of features and scenarios to optimize performance for. 
Implementing telemetry via VS OpenTelemetry initiative would give us comprehensive insight into how MSBuild is used in all scenarios. 

Goal: To have relevant data in that is actionable for decisions about development. Measuring real world performance impact of features (e.g. BuildCheck). Easily extensible telemetry infrastructure if we want to measure a new datapoint.

## Impact 
- Better planning of deployment of forces in MSBuild by product/team management.
- Customers can subscribe to telemetry locally to have data in standardized OpenTelemetry format

## Stakeholders 
- Jan (Krivanek|Provaznik) design and implementation of telemetry via VS OTel. Mariana - using data we already have from SDK.
- MSBuild Team+Management – want insights from builds in VS
- VS OpenTelemetry team – provide support for VS OpenTelemetry collector library, want successful adoption 
- SourceBuild – consulting and approving usage of OpenTelemetry 
- Chet – representing customers who want to monitor their builds locally

### Successful handover
- Shipped to Visual Studio
- Data queryable in Kusto
- Dashboards
- Customers are able to monitor with OpenTelemetry collector of choice (can be cut)

## Risks 
- Performance regression risks - it's another thing MSBuild would do and if the perf hit would be too bad it would need mitigation effort.
- It introduces a closed source dependency for VS and MSBuild.exe distribution methods which requires workarounds to remain compatible with SourceBuild policy (conditional compilation/build). 
- Using a new VS API - might have gaps
- Instrumenting data that would ultimately prove uninteresting.
- Potential additional costs and delays due to compliance with SourceBuild/VS data.

## Cost 
5 months of .5 developer's effort ~ 50 dev days (dd)

20-30dd JanPro OTel design + implementation, 10-15dd JanK design + implementation, 5-10dd Mariana/someone getting available data in order/"data science"/dashboards + external documentation

Uncertainties:
It’s an exploratory project for VS OpenTelemetry, we'll be their first OSS component, so there might come up issues. SourceBuild compliance could introduce delays.

## Plan 
- Prototyping the libraries/mechanism for collecting telemetry data (month 1) 10dd

- Defining usful data points (month 1) 5dd

- Design and approval of hooking VSTelemetry collectors and OTel collectors  (month 2) 10dd

- Formalizing, agreeing to sourcebuild and other external requirements (month 2) 2dd

- Instrumenting MSBuild with defined datapoints (month 3) 10dd

- Creating dashboards/insights (month 4) 5dd

- Documenting for customers how to hook their own telemetry collection (month 4) 3dd

- Buffer for discovered issues (VSData Platform, SourceBuild, OpenTelemetry) and more investments (month 5) 5dd

 