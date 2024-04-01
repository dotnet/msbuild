(adapted from #9484)

Preparation for the release:

- [ ]  Create `vs17.10` branch
- [ ]  Modify the VS insertion so that it flows from MSBuild vs17.10 to VS main [here](https://dev.azure.com/devdiv/DevDiv/_release?definitionId=1319&view=mine&_a=releases) Edit -> Schedule set under Artifacts -> disable toggle
AND
- [ ]  Disable automated run of https://dev.azure.com/devdiv/DevDiv/_release?definitionId=2153&view=mine&_a=releases (because our 17.11 builds don't have a place to go in VS yet)
- [ ]  Create darc channel for `VS 17.11` if it doesn't already exist \
`darc add-channel --name "VS 17.11"`
- [ ]  Ping internal "First Responders" Teams channel to get the new channel made available as a promotion target (e.g. https://github.com/dotnet/arcade/issues/12150): https://github.com/dotnet/arcade/pull/12989
IT SEEMS TO BE DONE https://github.com/dotnet/arcade/pull/14260
- [ ]  Remove the `main` to old release channel default channel \
`darc delete-default-channel --repo https://github.com/dotnet/msbuild --branch main --channel "VS 17.9"`
- [ ]  Associate the `main` branch with the next release channel \
`darc add-default-channel  --channel "VS 17.10" --branch main --repo https://github.com/dotnet/msbuild`
- [ ]  Check subscriptions for the current channel `VS 17.11` and update as necessary (for instance, SDK's `main` branch should usually be updated, whereas release branches often should not be \
`darc get-subscriptions --exact --source-repo https://github.com/dotnet/msbuild --channel "VS 17.11"`
- [ ]  Update channel VS 17.9 to VS 17.10 for the sdk main subscription
`darc update-subscription --id sdk_main_branch_id
- [ ]  Ensure that the current release channel `VS 17.10` is associated with the correct release branch\
`darc get-default-channels --source-repo https://github.com/dotnet/msbuild --branch vs17.10` \
if it is not, `darc add-default-channel  --channel "VS 17.10" --branch vs17.10 --repo https://github.com/dotnet/msbuild`
- [ ]  Fast-forward merge the correct commit (the one that is currently inserted to VS main) to the `vs17.10` branch \
e.g.: `git push upstream 2e6f2ff7ea311214255b6b2ca5cc0554fba1b345:refs/heads/vs17.10` _Note the commit for future steps_
_This is for the case where we create the branch too early and want it to be based actually on a different commit
If you waited till good point in time with main in a clean state - you just branch off and you are done
The branch should point to a good, recent spot, so the final-branding PR goes in on top of the right set of commits._
- [ ]  Update the branch merge flow in `dotnet/versions` to have the currently-in-servicing branches (pending review https://github.com/dotnet/versions/pull/951)
- [ ]  Fix OptProf data flow for the new vs17.10 branch
   - Run manually [OptProf](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=17389) pipeline for vs17.10 ('Run pipeline' in upper right)
   - Run the [MSBuild pipeline](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434) for vs17.10 without OptProf (set `SkipApplyOptimizationData` variable in 'Advanced options' section of the 'Run pipeline' menu to `true`)
   - Run the [MSBuild pipeline](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434) for vs17.10 with no extra customization - OptProf should succeed now
- [ ]  Create 17.11 branding PR (in main)
- [ ]  Create 17.10 localization ticket: https://aka.ms/ceChangeLocConfig (requesting to add localization for 17.10)
https://ceapex.visualstudio.com/CEINTL/_workitems/edit/957875 (DONE)
- [ ]  Enable 17.10 localization - by setting [`EnableReleaseOneLocBuild`](https://github.com/dotnet/msbuild/blob/vs17.10/.vsts-dotnet.yml) to `true`
- [ ]  Disable 17.9 localization -  by setting [`EnableReleaseOneLocBuild`] (https://github.com/dotnet/msbuild/blob/vs17.9/.vsts-dotnet.yml) to `false` clarify with @JanKrivanek
- [ ]  Merge 17.11 branding PR
- [ ]  Create and merge PR including public API baseline package version change (see https://github.com/dotnet/msbuild/pull/8116#discussion_r1049386978): #8949
- [ ]  When VS main snaps to 17.10 and updates its version to 17.11, modify the VS insertion so that it flows from MSBuild main to VS main.
- [ ]  Create 17.9 localization ticket: https://aka.ms/ceChangeLocConfig (requesting to remove localization for 17.9)
https://ceapex.visualstudio.com/CEINTL/_workitems/edit/936778
- [ ]  Remove MSBuild main from the experimental VS insertion flow.
- [ ]  Update the [release-branch insertion release definition](https://dev.azure.com/devdiv/DevDiv/_releaseDefinition?definitionId=2153&_a=definition-variables) to have `InsertTargetBranch` `rel/d17.10`.
- [ ]  Turn [the release pipeline](https://dev.azure.com/devdiv/DevDiv/_release?definitionId=2153&view=mine&_a=releases) back on.
- [ ]  Prepare final branding PR for `vs17.10`
- [ ]  Merge final branding to `vs17.10` branch
- [ ]  Update perfstar MSBuild insertions configuration: [example PR](https://dev.azure.com/devdiv/DevDiv/_git/dotnet-perfstar/pullrequest/522843)
- [ ] Note down the build (will be helpful for requesting nuget packages publishing): (https://devdiv.visualstudio.com/DevDiv/_build/results?buildId=8436672&view=results)
- [ ] Get QB approval (RAINER)
- [ ]  Merge to VS (babysit the automatically generated VS insertion PR https://devdiv.visualstudio.com/DevDiv/_git/VS/pullrequests for the MSBuild commit noted in above step): https://devdiv.visualstudio.com/DevDiv/_git/VS/pullrequest/518456 (RAINER)
- [ ] ~Update the PackageValidationBaselineVersion to the latest released version (17.10.0) - this might require temporary addition of [build artifacts feed](https://github.com/dotnet/msbuild/blob/29397b577e3ec0fe0c7650c3ab0400909655dc88/NuGet.config#L9) as the new version is not yet added to the official feeds (this is post release). This can trigger a high severity CG error (https://eng.ms/docs/cloud-ai-platform/devdiv/one-engineering-system-1es/1es-docs/secure-supply-chain/how-to-securely-configure-package-source-files) - however it should be fine to keep this temporary feed untill the release.~

ASAP On/After GA (based on release schedule (internal) https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/10097/Dev17-Release):

- [ ]  Push packages to nuget.org (not currently automated, contact dnceng - search "Publish MSBuild 17.6 to NuGet.org" email subject for template).
- [ ]  Publish docs: submit reference request at https://aka.ms/publishondocs
  - Click on the link labeled *Request – Reference Publishing*
  - You can use existing [ticket](https://dev.azure.com/msft-skilling/Content/_workitems/edit/183613) as a reference
- [ ] ~Remove the temporarily added [build feed from `nuget.config`](https://github.com/dotnet/msbuild/blob/29397b577e3ec0fe0c7650c3ab0400909655dc88/NuGet.config#L9) if it was added in the `Update the PackageValidationBaselineVersion` step~
- [ ]  Update `main` subscriptions to the new channel (this can be done before or after release - depending on when the source repos from our previous - VS 17.10 - channle start to publish in the next - VS 17.11 - channel) \
`darc get-subscriptions --exact --target-repo https://github.com/dotnet/msbuild --target-branch main`
- [ ]  Create the 17.10 release
  - [ ]  Create tag (can be done upfront)
  ```
  git checkout <commit noted above>
  git tag v17.10.3
  git push upstream v17.10.3
  ```
  - [ ]  Create Release in Github with `Create Release from Tag` GH option (https://github.com/dotnet/msbuild/releases/new?tag=v17.9.3) - the release notes can be prepopulated (`Generate Release Notes`)
