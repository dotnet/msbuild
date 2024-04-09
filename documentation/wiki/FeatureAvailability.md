# What is Feature Availablity?
Feature Availablity is an API that can tell you the availability status of the specific feature of the MSBuild engine. Feature is saved as a string and availability is an enum `FeatureStatus`:
*  `Undefined` - the availability of the feature is undefined (not in the list)
*  `Available` - the feature is available
*  `NotAvailable` - the feature is not available
*  `Preview` - the feature is in preview (not stable)

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