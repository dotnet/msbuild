# What is Feature Availablity?
Feature Availability is an API that can tell you the availability status of the specific feature of the MSBuild engine. Feature names are represented by strings and availability is an enum `FeatureStatus` with the following values: `Undefined`, `Available`, `NotAvailable`, `Preview`.

# How to use?
## API
In `Microsoft.Build.Framework` use `FeatureStatus Features.CheckFeatureAvailability(string featureName)` to get the feature availability.

## Command line switch
Use `/featureavailability`(`-featureavailability`) or `/fa` (`-fa`) switches.

## Property function `CheckFeatureAvailability`
Use `string CheckFeatureAvailability(string featureName)` property function.
```xml
<PropertyGroup>
  <FeatureAvailability>$([MSBuild]::CheckFeatureAvailability('FeatureA'))</FeatureAvailability>
</PropertyGroup>
```

# Current Features
See [Framework.Features.cs](https://github.com/dotnet/msbuild/blob/main/src/Framework/Features.cs)