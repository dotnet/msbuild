## Localizable resource structure
- Neutral resources:
  -  https://github.com/Microsoft/msbuild/blob/master/src/Utilities/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeTasks/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeCommandLine/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeTasks/ManifestUtil/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeBuildEngine/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/Shared/Resources/Strings.shared.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/OrcasEngine/Resources/Strings.resx
  -  https://github.com/Microsoft/msbuild/blob/master/src/XMakeConversion/Resources/Strings.resx
- `Strings.shared.resx` is a shared resource and gets embedded into all msbuild dlls
- each neutral resource has a directory named `xlf` besides it which contains its localized strings in .xlf format
- there is one language per xlf
- the logical name for a resource (what you specify in `ResourceManager`'s constructor) is: `<Assembly Name>.<Neutral Resx File Name>.resources`

## How to edit a resource
- if you need to add / remove / update a resource, only do so in the neutral resource. xlf files get automatically updated during localized builds.

## What a localized build does
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

## Syncing the XLF files from their corresponding neutral resx files
- `build /t:build /p:SyncXlf=true` syncs the xlf files but does not do a localized build
- can be called in tandem with a localized build: `build /t:build /p:LocalizedBuild=true /p:SyncXlf=true`

## Process for interacting with the localization team
- 2-3 weeks before a VS release the MSBuild team needs to ping the Microsoft localization team to update the xlf files with the latest changes in the neutral resources
- before pinging the loc team, we sync the xlf files on a dev machine and commit the changes
- this will be the ONLY time we sync xlf files and commit the changes. Otherwise, if we commit xlfs while the loc team is translating (between their checkout and merged PR), we might get races and loose resource updates.

## Contributing better translation
- send a PR with an updated `<target>` element of the xlf resource (do not include other non-localization changes)
- we will notify the localization team, which will then take over and review the PR
