# MSBuild Release Checklist {{THIS_RELEASE_VERSION}}

## At any time

- [ ] Create a new issue to track the release checklist, with this checklist copied into the issue.
  - [ ] Replace `{{PREVIOUS_RELEASE_VERSION}}` with the previous release version, for example `17.9`
  - [ ] Replace `{{THIS_RELEASE_VERSION}}` with the current release version, for example `17.10`
  - [ ] Replace `{{NEXT_VERSION}}` with the next release version, for example `17.11`
- [ ]  Create `vs{{THIS_RELEASE_VERSION}}` branch
- [ ]  Create darc channel for `VS {{NEXT_VERSION}}` if it doesn't already exist \
`darc add-channel --name "VS {{NEXT_VERSION}}"`
- [ ]  Ping internal "First Responders" Teams channel to get the new channel made available as a promotion target (e.g. dotnet/arcade#12150): {{URL_OF_CHANNEL_PROMOTION_PR}}

## At release time

- [ ] If the release is being cut more than a few days before the VS-side snap, do these two steps. Otherwise check them off.
  - [ ]  Modify the VS insertion so that it flows from MSBuild `vs{{THIS_RELEASE_VERSION}}` to VS `main` [in the MSBuild-release-branch release definition](https://dev.azure.com/devdiv/DevDiv/_release?definitionId=1319&view=mine&_a=releases) Edit -> Schedule set under Artifacts -> disable toggle
AND
  - [ ]  Disable automated run of [the MSBuild-main-branch release definition](https://dev.azure.com/devdiv/DevDiv/_release?definitionId=2153&view=mine&_a=releases) (because our {{NEXT_VERSION}} builds don't have a place to go in VS yet)
- [ ]  Remove the `main` to old release channel ({{THIS_RELEASE_VERSION}}) default channel \
`darc delete-default-channel --repo https://github.com/dotnet/msbuild --branch main --channel "VS {{THIS_RELEASE_VERSION}}"`
- [ ]  Associate the `main` branch with the next release channel \
`darc add-default-channel  --channel "VS {{THIS_RELEASE_VERSION}}" --branch main --repo https://github.com/dotnet/msbuild`
- [ ]  Check subscriptions for the forward-looking channel `VS {{NEXT_VERSION}}` and update as necessary (for instance, SDK's `main` branch should usually be updated, whereas release branches often should not be \
`darc get-subscriptions --exact --source-repo https://github.com/dotnet/msbuild --channel "VS {{THIS_RELEASE_VERSION}}"`
- [ ]  Update channel VS {{THIS_RELEASE_VERSION}} to VS {{NEXT_VERSION}} for the sdk main subscription and any others from the previous step
`darc update-subscription --id sdk_main_branch_id`
- [ ]  Ensure that the current release channel `VS {{THIS_RELEASE_VERSION}}` is associated with the correct release branch\
`darc get-default-channels --source-repo https://github.com/dotnet/msbuild --branch vs{{THIS_RELEASE_VERSION}}` \
if it is not, `darc add-default-channel  --channel "VS {{THIS_RELEASE_VERSION}}" --branch vs{{THIS_RELEASE_VERSION}} --repo https://github.com/dotnet/msbuild`
- [ ]  If the branch was created before the fork: fast-forward merge the correct commit (the one that is currently inserted to VS main) to the `vs{{THIS_RELEASE_VERSION}}` branch \
e.g.: `git push upstream 2e6f2ff7ea311214255b6b2ca5cc0554fba1b345:refs/heads/vs17.10` \
_(This is for the case where we create the branch too early and want it to be based actually on a different commit. If you waited until a good point in time with `main` in a clean state, just branch off and you are done. The branch should point to a good, recent spot, so the final-branding PR goes in on top of the right set of commits.)_
- [ ]  Update the branch merge flow in `dotnet/versions` to have the currently-in-servicing branches: {{URL_OF_VERSIONS_PR}}
- [ ]  Fix OptProf data flow for the new vs{{THIS_RELEASE_VERSION}} branch
  - [ ] Manually run [OptProf data collection](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=17389) pipeline for vs{{THIS_RELEASE_VERSION}} ('Run pipeline' in upper right)
  - [ ] Run the [official build](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434) for vs{{THIS_RELEASE_VERSION}} without OptProf (set `SkipApplyOptimizationData` variable in 'Advanced options' section of the 'Run pipeline' menu to `true`)
  - [ ] Run the [official build](https://devdiv.visualstudio.com/DevDiv/_build?definitionId=9434) for vs{{THIS_RELEASE_VERSION}} with no extra customization - OptProf should succeed now
- [ ]  Create {{NEXT_VERSION}} branding PR (in main): {{URL_OF_NEXT_VERSION_BRANDING_PR}}
- [ ]  Create {{THIS_RELEASE_VERSION}} localization ticket: https://aka.ms/ceChangeLocConfig (requesting to add localization for {{THIS_RELEASE_VERSION}}): {{URL_OF_LOCALIZATION_TICKET}}
https://ceapex.visualstudio.com/CEINTL/_workitems/edit/957875 (DONE)
- [ ]  Enable {{THIS_RELEASE_VERSION}} localization - by setting [`EnableReleaseOneLocBuild`](https://github.com/dotnet/msbuild/blob/vs{{THIS_RELEASE_VERSION}}/.vsts-dotnet.yml) to `true`
- [ ]  Disable {{PREVIOUS_RELEASE_VERSION}} localization -  by setting [`EnableReleaseOneLocBuild`](https://github.com/dotnet/msbuild/blob/vs{{PREVIOUS_RELEASE_VERSION}}/.vsts-dotnet.yml) to `false` clarify with @JanKrivanek
- [ ]  Merge {{NEXT_VERSION}} branding PR
- [ ]  Create and merge PR including public API baseline package version change (see https://github.com/dotnet/msbuild/pull/8116#discussion_r1049386978): #8949
- [ ]  When VS main snaps to {{THIS_RELEASE_VERSION}} and updates its version to {{NEXT_VERSION}}, modify the VS insertion so that it flows from MSBuild main to VS main.
- [ ]  Create {{PREVIOUS_RELEASE_VERSION}} localization ticket: https://aka.ms/ceChangeLocConfig (requesting to remove localization for {{PREVIOUS_RELEASE_VERSION}})
{{URL_OF_LOCALIZATION_DISABLE_TICKET}}
- [ ]  Remove MSBuild main from the experimental VS insertion flow.
- [ ]  Update the [release-branch insertion release definition](https://dev.azure.com/devdiv/DevDiv/_releaseDefinition?definitionId=2153&_a=definition-variables) to have `InsertTargetBranch` `rel/d{{THIS_RELEASE_VERSION}}`.
- [ ]  Turn [the release pipeline](https://dev.azure.com/devdiv/DevDiv/_release?definitionId=2153&view=mine&_a=releases) back on.
- [ ]  Prepare final branding PR for `vs{{THIS_RELEASE_VERSION}}`: {{URL_OF_FINAL_BRANDING_PR}}
- [ ]  Merge final branding to `vs{{THIS_RELEASE_VERSION}}` branch
- [ ]  Update perfstar MSBuild insertions configuration: [example PR](https://dev.azure.com/devdiv/DevDiv/_git/dotnet-perfstar/pullrequest/522843): {{URL_OF_PERFSTAR_PR}}
- [ ] Note down the build (will be helpful for requesting nuget packages publishing): {{URL_OF_BUILD}}
- [ ] Get M2 or QB approval as necessary per the VS schedule
- [ ]  Merge to VS (babysit the automatically generated VS insertion PR https://devdiv.visualstudio.com/DevDiv/_git/VS/pullrequests for the MSBuild commit noted in above step): {{URL_OF_VS_INSERTION}}
- [ ] Update the PackageValidationBaselineVersion to the latest released version ({{THIS_RELEASE_VERSION}}.0) - this might require temporary addition of the [build artifacts feed](https://github.com/dotnet/msbuild/blob/29397b577e3ec0fe0c7650c3ab0400909655dc88/NuGet.config#L9) as the new version is not yet added to the official feeds (this is post release). This can trigger a high severity CG error (https://eng.ms/docs/cloud-ai-platform/devdiv/one-engineering-system-1es/1es-docs/secure-supply-chain/how-to-securely-configure-package-source-files) - however it should be fine to keep this temporary feed untill the release.

## ASAP On/After GA:

Timing based on the [(Microsoft-internal) release schedule](https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/10097/Dev17-Release).

- [ ]  Push packages to nuget.org (not currently automated, contact dnceng - search "Publish MSBuild 17.6 to NuGet.org" email subject for template).
- [ ]  Publish docs: submit reference request at https://aka.ms/publishondocs
  - Click on the link labeled *Request – Reference Publishing*
  - You can use existing [ticket](https://dev.azure.com/msft-skilling/Content/_workitems/edit/183613) as a reference
- [ ] Remove the temporarily added [build feed from `nuget.config`](https://github.com/dotnet/msbuild/blob/29397b577e3ec0fe0c7650c3ab0400909655dc88/NuGet.config#L9) if it was added in the `Update the PackageValidationBaselineVersion` step
- [ ]  Update `main` subscriptions to the new channel (this can be done before or after release - depending on when the source repos from our previous - VS {{THIS_RELEASE_VERSION}} - channle start to publish in the next - VS {{NEXT_VERSION}} - channel) \
`darc get-subscriptions --exact --target-repo https://github.com/dotnet/msbuild --target-branch main`
- [ ]  Create the {{THIS_RELEASE_VERSION}} release
  - [ ]  Create tag (can be done upfront)
  ```
  git checkout <commit noted above>
  git tag v{{THIS_RELEASE_VERSION}}.3
  git push upstream v{{THIS_RELEASE_VERSION}}.3
  ```
  - [ ]  Create Release in Github with `Create Release from Tag` GH option (https://github.com/dotnet/msbuild/releases/new?tag=v17.9.3) - the release notes can be prepopulated (`Generate Release Notes`)
