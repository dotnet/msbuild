## Localizable resource structure
- Neutral resources: [*resx](https://github.com/search?utf8=%E2%9C%93&q=repo%3AMicrosoft%2Fmsbuild+extension%3Aresx&type=Code&ref=advsearch&l=&l=)
- `Strings.shared.resx` is a shared resource and gets embedded into all msbuild dlls
- each neutral resource has a directory named `xlf` besides it which contains its localized strings in .xlf format
- there is one language per xlf
- the logical name for a resource is: `<Assembly Name>.<Neutral Resx File Name>.resources`. In the ResourceManager this appears as `<Assembly Name>.<Neutral Resx File Name>` (without the trailing `.resources`). For example, the `Microsoft.Build` assembly uses the `Microsoft.Build.Strings.resources` [logical resource name](https://github.com/Microsoft/msbuild/blob/master/src/XMakeBuildEngine/Microsoft.Build.csproj#L659) (the resource file is `Strings.resx`), and its corresponding [ResourceManager](https://github.com/Microsoft/msbuild/blob/master/src/XMakeBuildEngine/Resources/AssemblyResources.cs#L116) uses `Microsoft.Build.Strings`.

## How to edit a resource
- if you need to add / remove / update a resource, only do so in the neutral resx files. xlf files get automatically updated during localized builds.

## What a localized build does
- converts xlf files to localized resx files
- the localized resx files are generated into the `$(IntermediaryOutputPath)`
- produces satellite assemblies for each language
 - satellite assemblies are used even on English machines. This is for testing purposes, to ensure that English builds are not different than non English builds

## Process for interacting with the localization team
- 3 weeks cadence for master, initiated by loc team
- on demand for master / release branches, initiated by msbuild team

## Contributing a better translation
- send a PR with an updated `<target>` element of the xlf resource (do not include other non-localization changes)
- we will notify the localization team, which will then take over and review the PR
