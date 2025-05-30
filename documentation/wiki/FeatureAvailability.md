# What is Feature Availablity?
Feature Availability is an API that can tell you the availability status of the specific feature of the MSBuild engine. Feature names are represented by strings and availability is an enum `FeatureStatus` with the following values:
*  `Undefined` - the availability of the feature is undefined (the feature might or might not be supported by the current MSBuild engine - the feature is unknown to the feature availability checker, so it cannot be decided).
*  `Available` - the feature is available
*  `NotAvailable` - the feature is not available (unlike `Undefined`, the feature name is known to the feature availability checker and it knows the feature is not supported by current MSBuild engine)
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