# Localizable resource structure
- Neutral resources:
  -  https://github.com/Microsoft/msbuild/blob/master/src/Utilities/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeTasks/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeCommandLine/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeTasks/ManifestUtil/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeBuildEngine/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/Shared/Strings.shared.resx
- each neutral resource has a directory named `xlf` besides it which contains its localized strings in .xlf format
- there is one language per xlf
- the resource logical name (what you specify in `ResourceManager`'s constructor) is: `<AssemblyName>.<NeutralResxName>.resources`

# How to edit a resource
- if you need to add / remove / update a resource, only do so in the neutral resource. xlf files get update automatically during localized builds.

# What a localized build does
- updates xlf files from their corresponding neutral resx
- convert xlf files to localized resx files
- the localized resx files are generated into the `IntermediaryOutputPath`
- produces satellite assemblies for each language

# Doing a localized build
-  currently only supported on windows
- `build /t:build /p:LocalizedBuild=true`
- to test localized builds, use `build /t:build /p:LocalizedBuild=true;LocalizedTestBuild=true`. This replaces all resources with `!resource_id!english_resource!localized_resource!`
  - to replace the test string with a string of your own choice, use `build /t:build /p:LocalizedBuild=true;LocalizedTestBuild=true;LocalizedTestString=foo`

# Process for interacting with the localization team
- 2-3 weeks before a VS release we need to ping the Microsoft localization team to update the xlf files with the latest changes in the neutral resources
- before pinging the loc team, we do a localized build and commit the xlf changes
- this will be the ONLY time we do localized builds and commit xlf changes. Otherwise, if we commit xlfs while the loc team is translation, we might get races.
