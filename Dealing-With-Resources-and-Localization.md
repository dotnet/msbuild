## Localizable resource structure
- Neutral resources:
  -  https://github.com/Microsoft/msbuild/blob/master/src/Utilities/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeTasks/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeCommandLine/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeTasks/ManifestUtil/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeBuildEngine/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/Shared/Resources/Strings.shared.resx
- `Strings.shared.resx` is a shared resource and gets embedded into all msbuild dlls
- each neutral resource has a directory named `xlf` besides it which contains its localized strings in .xlf format
- there is one language per xlf
- the resource logical name (what you specify in `ResourceManager`'s constructor) is: `<AssemblyName>.<NeutralResxName>.resources`

## How to edit a resource
- if you need to add / remove / update a resource, only do so in the neutral resource. xlf files get automatically updated during localized builds.

## What a localized build does
- updates xlf files from their corresponding neutral resx
- converts xlf files to localized resx files
- the localized resx files are generated into the `$(IntermediaryOutputPath)`
- produces satellite assemblies for each language
 - satellite assemblies are used even on English machines. This is for testing purposes, to ensure that English builds are not different than non English builds

## Doing a localized build
-  currently only supported on windows
- `build /t:build /p:LocalizedBuild=true`
- to test localized builds, use `build /t:build /p:LocalizedBuild=true;LocalizedTestBuild=true`. This replaces all resources with `!resource_id!english_resource!localized_resource!`
  - to replace the test string with a string of your own choice, use `build /t:build /p:LocalizedBuild=true;LocalizedTestBuild=true;LocalizedTestString=foo`
  - testing does not work with the English satellite assemblies because they are directly copied from the neutral resources and do not have corresponding xlf files

## Process for interacting with the localization team
- 2-3 weeks before a VS release the MSBuild team needs to ping the Microsoft localization team to update the xlf files with the latest changes in the neutral resources
- before pinging the loc team, we do a localized build and commit the xlf changes
- this will be the ONLY time we do localized builds and commit xlf changes. Otherwise, if we commit xlfs while the loc team is translating (between their checkout and merged PR), we might get races and loose resource updates.
