# MSBuild Custom Cultures Support

## Overview

The `EnableCustomCulture` property provides an opt-in mechanism for handling custom culture-specific resources in MSBuild projects. This feature allows for greater control over which directories are treated as culture-specific resources during the build process.

## Purpose

In some projects, directory names that match culture name patterns might not actually be culture resources. This can cause issues with resource compilation and deployment. This feature flag enables:

1. Control over whether custom culture detection is enabled
2. Fine-grained configuration of which directories should be excluded from culture-specific resource processing

## Usage

### Enabling the Feature

To enable the custom cultures feature, set the `EnableCustomCulture` property `true`.

```xml
<PropertyGroup>
  <EnableCustomCulture>true</EnableCustomCulture>
</PropertyGroup>
```

### Excluding Specific Directories

When the feature is enabled, you can specify directories that should not be treated as culture-specific resources using the `NonCultureResourceDirectories` property:

```xml
<PropertyGroup>
  <NonCultureResourceDirectories>long;hash;temp</NonCultureResourceDirectories>
</PropertyGroup>
```

In this example, directories named "long", "hash", or "temp" will not be processed as culture-specific resources and the assemblied inside of them will be skipped, even if their names match culture naming patterns. Globbing is not supported.

## Additional Notes

- This feature does not affect the standard resource handling for well-known cultures.
- The feature is designed to be backward compatible - existing projects without the feature flag will behave the same as before.
- Performance impact is minimal, as the exclusion check happens only during the resource discovery phase of the build.