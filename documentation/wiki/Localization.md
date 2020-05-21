# Localizing MSBuild

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

## Localizing XSD "IntelliSense"

Code completion ("IntelliSense") for MSBuild project files is provided minimally in Visual Studio by XML Schema files like [`Microsoft.Build.CommonTypes.xsd`](https://github.com/microsoft/msbuild/blob/ba9a1d64a7abf15a8505827c00413156a3eb7f62/src/MSBuild/MSBuild/Microsoft.Build.CommonTypes.xsd). These files are English-only in the GitHub repo; their localization is managed in the Microsoft-internal `VS` repo.

### If there is a bug in XSD localization

File xsd localization bugs in this repo. The MSBuild team will coordinate with the Visual Studio localization team to redirect it appropriately.

### When an XSD has been updated

After updating an XSD in the GitHub repo, someone with internal access must update the copy in the `VS` repo. To do so:

1. Locally clone VS following the standard instructions.
2. Locally update your clone of the GitHub msbuild repo to include the merge of the change.
3. Start a new branch in the VS repository from the current working branch (probably `master`).
4. Copy from the msbuild path `src/MSBuild/MSBuild/*.xsd` to the VS path `src/xmake/XMakeCommandLine`.
5. Ensure that the commit message has a full link to the commit used to update the `.xsd` files, like `https://github.com/microsoft/msbuild/commit/ba9a1d64a7abf15a8505827c00413156a3eb7f62`.
6. Push and submit through the usual VS PR process, including the `MSBuild` team as reviewers.

Example PR doing this: https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequest/186890.
