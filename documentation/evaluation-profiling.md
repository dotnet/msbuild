# MSBuild evaluation profiling

MSBuild 15.6 and higher contains an evaluation profiler, which can help analyze which parts of a project (and any .targets/etc that it imports) are taking the most time to evaluate.

The profiler is enabled when passing `/profileevaluation:<filename>` as a command-line argument to MSBuild. After the build, the specified file will contain a profiler report. If the specified filename does not end in `.md`, it will be a tab-separated value (TSV) table suitable for loading into a spreadsheet. If the filename ends in `.md`, it will be a Markdown file that looks like the following:

Pass|File|Line #|Expression|Inc (ms)|Inc (%)|Exc (ms)|Exc (%)|#|Bug
---|---|---:|---|---:|---:|---:|---:|---:|---
Total Evaluation||||650|100%|17|2.7%|1|
Initial Properties (Pass 0)||||5|0.8%|5|0.8%|1|
Properties (Pass 1)||||360|55.4%|3|0.4%|1|
ItemDefinitionGroup (Pass 2)||||9|1.4%|0|0%|1|
Items (Pass 3)||||63|9.7%|1|0.2%|1|
Lazy Items (Pass 3.1)||||173|26.6%|29|4.5%|1|
UsingTasks (Pass 4)||||8|1.2%|8|1.2%|1|
Targets (Pass 5)||||15|2.3%|1|0.2%|1|
Properties (Pass 1)|MVC.csproj|0|`<Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk.Web" />`|351|54%|76|11.7%|1|
Items (Pass 3)|Microsoft.NETCore.App.props|8|`<PackageConflictPlatformManifests Include="$(MSBuildThisFileDirectory)Microsoft.NETCore.App.Platform...`|37|5.7%|37|5.7%|1|
Properties (Pass 1)|Microsoft.Common.CurrentVersion.targets|83|`<FrameworkPathOverride Condition="'$(FrameworkPathOverride)' == ''" >$([Microsoft.Build.Utilities.To...`|32|4.9%|32|4.9%|1|
Lazy Items (Pass 3.1)|Microsoft.NET.Sdk.DefaultItems.props|26|`<Compile Include="**/*$(DefaultLanguageSourceExtension)" Exclude="$(DefaultItemExcludes);$(DefaultEx...`|31|4.7%|31|4.7%|1|
Properties (Pass 1)|Microsoft.Common.targets|114|`<Import Project="$(CommonTargetsPath)"  />`|95|14.6%|23|3.5%|1|
Lazy Items (Pass 3.1)|Microsoft.NET.Sdk.Web.ProjectSystem.props|29|`<Content Include="wwwroot\**" CopyToPublishDirectory="PreserveNewest" Exclude="$(DefaultItemExcludes...`|17|2.6%|17|2.6%|1|
Properties (Pass 1)|Microsoft.Common.CurrentVersion.targets|92|`<TargetPlatformSdkPath Condition="'$(TargetPlatformSdkPath)' == ''" >$([Microsoft.Build.Utilities.To...`|15|2.3%|15|2.3%|1|
Lazy Items (Pass 3.1)|Microsoft.NET.Sdk.DefaultItems.targets|156|`Condition="'%(LinkBase)' != ''")`|12|1.9%|12|1.9%|1|
Properties (Pass 1)|Microsoft.Common.props|15|`Condition="'$(ImportByWildcardBeforeMicrosoftCommonProps)' == ''")`|12|1.9%|12|1.9%|1|
Properties (Pass 1)|Microsoft.Common.props|63|`<Import Project="$(MSBuildProjectExtensionsPath)$(MSBuildProjectFile).*.props" Condition="'$(ImportP...`|18|2.8%|12|1.8%|1|
Properties (Pass 1)|Microsoft.CSharp.targets|168|`<Import Project="$(CSharpTargetsPath)"  />`|164|25.2%|11|1.7%|1|
Properties (Pass 1)|MVC.csproj.nuget.g.targets|7|`<Import Project="$(NuGetPackageRoot)netstandard.library\2.0.0\build\netstandard2.0\NETStandard.Libra...`|14|2.2%|11|1.7%|1|
Lazy Items (Pass 3.1)|Microsoft.NET.Sdk.DefaultItems.props|30|`<None Include="**/*" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)"  />`|10|1.6%|10|1.6%|1|
Properties (Pass 1)|Sdk.props|29|`<Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"  />`|56|8.6%|10|1.5%|1|
Items (Pass 3)|Microsoft.Common.CurrentVersion.targets|368|`Condition="'$(OutputType)' != 'winmdobj' and '@(_DebugSymbolsIntermediatePath)' == ''")`|9|1.4%|9|1.4%|1|
ItemDefinitionGroup (Pass 2)|Microsoft.Common.CurrentVersion.targets|1661|`<ProjectReference ><!-- Target to build in the project reference; by default, this property is blank...`|8|1.3%|8|1.3%|1|
Lazy Items (Pass 3.1)|Microsoft.Common.CurrentVersion.targets|369|`<_DebugSymbolsOutputPath Include="@(_DebugSymbolsIntermediatePath-&gt;'$(OutDir)%(Filename)%(Extensi...`|8|1.2%|8|1.2%|1|
Lazy Items (Pass 3.1)|Microsoft.NET.Sdk.Web.ProjectSystem.props|32|`<Content Include="**\*.json" CopyToPublishDirectory="PreserveNewest" Exclude="$(DefaultItemExcludes)...`|8|1.2%|8|1.2%|1|
Properties (Pass 1)|MVC.csproj.nuget.g.targets|9|`<Import Project="C:\Program Files\dotnet\sdk\NuGetFallbackFolder\microsoft.extensions.configuration....`|8|1.2%|7|1.1%|1|
Lazy Items (Pass 3.1)|Microsoft.NET.Sdk.Web.ProjectSystem.props|39|`<Compile Remove="wwwroot\**"  />`|7|1.1%|7|1.1%|1|
Lazy Items (Pass 3.1)|Microsoft.NET.Sdk.Web.ProjectSystem.props|31|`<Content Include="**\*.cshtml" CopyToPublishDirectory="PreserveNewest" Exclude="$(DefaultItemExclude...`|7|1.1%|7|1.1%|1|
Lazy Items (Pass 3.1)|Microsoft.NET.Sdk.Web.ProjectSystem.props|30|`<Content Include="**\*.config" CopyToPublishDirectory="PreserveNewest" Exclude="$(DefaultItemExclude...`|7|1.1%|7|1.1%|1|
Lazy Items (Pass 3.1)|Microsoft.NET.Sdk.DefaultItems.props|27|`<EmbeddedResource Include="**/*.resx" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFold...`|7|1.1%|7|1.1%|1|
Properties (Pass 1)|Sdk.targets|41|`<Import Project="$(LanguageTargets)"  />`|171|26.2%|7|1%|1|
