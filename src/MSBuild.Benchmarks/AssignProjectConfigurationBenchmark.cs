// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Xml;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class AssignProjectConfigurationBenchmark
{
    private ITaskItem[] _projectReferences = null!;
    private string _currentProject = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        XmlDocument document = new();
        document.LoadXml(OrchardCoreSolutionConfigurationContents);

        XmlNodeList projectConfigurations = document.GetElementsByTagName("ProjectConfiguration");
        _projectReferences = new ITaskItem[projectConfigurations.Count];

        for (int i = 0; i < projectConfigurations.Count; i++)
        {
            XmlElement projectConfiguration = (XmlElement)projectConfigurations[i]!;
            string absolutePath = projectConfiguration.GetAttribute("AbsolutePath");
            TaskItem projectReference = new(absolutePath);
            projectReference.SetMetadata("Project", projectConfiguration.GetAttribute("Project"));
            _projectReferences[i] = projectReference;
        }

        _currentProject = _projectReferences[0].ItemSpec;
    }

    [Benchmark]
    public int ExecuteOrchardCoreSolutionConfiguration()
    {
        AssignProjectConfiguration task = new()
        {
            BuildEngine = NoOpBuildEngine.Instance,
            ProjectReferences = _projectReferences,
            CurrentProject = _currentProject,
            CurrentProjectConfiguration = "Debug",
            CurrentProjectPlatform = "AnyCPU",
            SolutionConfigurationContents = OrchardCoreSolutionConfigurationContents,
            AddSyntheticProjectReferencesForSolutionDependencies = true,
            OnlyReferenceAndBuildProjectsEnabledInSolutionConfiguration = true,
            ShouldUnsetParentConfigurationAndPlatform = true,
            ResolveConfigurationPlatformUsingMappings = false,
        };

        if (!task.Execute())
        {
            throw new InvalidOperationException("AssignProjectConfiguration failed for the OrchardCore solution configuration fixture.");
        }

        return task.AssignedProjects.Length + task.UnassignedProjects.Length;
    }

    private sealed class NoOpBuildEngine : IBuildEngine
    {
        public static readonly NoOpBuildEngine Instance = new();

        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => string.Empty;

        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => true;
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public void LogErrorEvent(BuildErrorEventArgs e) { }
        public void LogMessageEvent(BuildMessageEventArgs e) { }
        public void LogWarningEvent(BuildWarningEventArgs e) { }
    }

    // Generated from OrchardCMS/OrchardCore OrchardCore.slnx at f852c64988db0af60446a970e41482177caba697.
    // The absolute path prefix is root-dependent; this fixture was generated from /tmp/orchardcore-fixture/OrchardCore.slnx.
    // SHA-256 of the XML content: 1e4fc5a6be72129bfd7f5e7e0cd177b58120ef95371c3de7c1f768caac9e6959.
    private const string OrchardCoreSolutionConfigurationContents = """
<SolutionConfiguration>
  <ProjectConfiguration Project="{62ED3456-04DA-BD75-5B69-A29FD188945B}" AbsolutePath="/tmp/orchardcore-fixture/src/docs/OrchardCore.Docs.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{1809B5F3-4C06-BBE7-02AF-8120097DBC40}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Cms.Web/OrchardCore.Cms.Web.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E98D6936-404F-C4E1-301A-E2D0AC0D59D5}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Mvc.Core/OrchardCore.Mvc.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{4A152A87-1A6F-1CF5-54BA-B198454849F1}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.AdminMenu/OrchardCore.AdminMenu.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{31A13B1F-141C-1810-3746-0966248E0083}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Alias/OrchardCore.Alias.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{6ADBD134-C297-932C-7CA2-9346AFCD62F2}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.ArchiveLater/OrchardCore.ArchiveLater.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{2AF6FF9C-00B8-C497-0DF8-1407D65C9222}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Autoroute/OrchardCore.Autoroute.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{D239C450-2377-0AE5-28DA-186CC1E528DA}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.AzureAI/OrchardCore.AzureAI.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{F9597E02-B144-FCE5-0066-86F3F51FE078}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.ContentFields/OrchardCore.ContentFields.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{3CBA2583-C1F8-7F11-2E4D-6272EB2F860F}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.ContentLocalization/OrchardCore.ContentLocalization.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E5AC3651-A54C-B6C0-BCBD-1597F8529889}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.ContentPreview/OrchardCore.ContentPreview.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{70A73232-ED35-895B-F2F2-C29E7BC69921}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Contents/OrchardCore.Contents.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{77133BC1-DE92-73CC-6764-DB961BA52F31}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.ContentTypes/OrchardCore.ContentTypes.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{8B08F441-60BA-9D1E-0532-88AB6CD9FB1D}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.CustomSettings/OrchardCore.CustomSettings.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{A282AB05-6E21-7FAF-4C4E-D15947A1AA00}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Demo/OrchardCore.Demo.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{FDBAFA77-76B6-B900-27DD-D408D8708AAF}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Elasticsearch/OrchardCore.Elasticsearch.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{DAA6D963-D53A-97C8-F35E-F91B90392E1B}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Feeds/OrchardCore.Feeds.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{434877DC-5D16-5E43-E4BD-5AFC230F643F}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Flows/OrchardCore.Flows.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{DDB0FD3D-23F5-7C0D-E0CB-7CCE55B2FBEC}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Forms/OrchardCore.Forms.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{0C6495CD-FC54-4EB7-E259-DEACFDE2AC43}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.HomeRoute/OrchardCore.HomeRoute.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{32CFD92D-FE04-3653-200A-4641E40CB08A}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Html/OrchardCore.Html.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{9D5AB8EA-146C-15C8-5D6D-D0959545DE41}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Indexing/OrchardCore.Indexing.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{4EB3A027-B127-12C1-6800-DFCBF7E1CAD7}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Layers/OrchardCore.Layers.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{8D584347-D5CB-11B3-359B-10155451EF6A}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Liquid/OrchardCore.Liquid.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{3369E09E-5282-E8BB-09D7-F42FE276A051}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Lists/OrchardCore.Lists.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{67E356FF-CF85-D24B-FA20-96A50A44B209}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Lucene/OrchardCore.Lucene.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{170FC3C6-9268-BD8C-E2BF-C6319B242BCA}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Markdown/OrchardCore.Markdown.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{8F4A18D7-C6D0-80DF-4AE4-B2A8683627C1}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Media.AmazonS3/OrchardCore.Media.AmazonS3.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{23E14A28-E9B8-E237-E6B6-7E495DC1D032}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Media.Azure/OrchardCore.Media.Azure.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{588C4AFA-5364-E283-A3DD-47213EC4E1DE}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Media.Indexing.OpenXML/OrchardCore.Media.Indexing.OpenXML.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{821E2270-7A67-82C7-3192-09FDB9FAF644}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Media.Indexing.Pdf/OrchardCore.Media.Indexing.Pdf.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{5D125424-1FC0-290C-6A65-C6024A5B162A}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Media/OrchardCore.Media.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{F6A9FD5F-0121-F583-28DB-270A2D90AABB}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Menu/OrchardCore.Menu.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{FBD111E2-63D7-8ADB-0AFD-E891C3C08037}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Placements/OrchardCore.Placements.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{74A885B2-C702-DCCF-EC07-CD575F5448DF}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.PublishLater/OrchardCore.PublishLater.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{5EA07538-4D8E-732A-1A04-DAD013EA8537}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Queries/OrchardCore.Queries.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{CC72D65A-9674-6F82-A826-1DFDD2C39E6C}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Rules/OrchardCore.Rules.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{84ACBB0F-AFC4-0C20-862D-B08559243094}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Search/OrchardCore.Search.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E6478939-99FC-5994-5B97-6989F034BD4D}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Seo/OrchardCore.Seo.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{21DB4C8A-1128-FA6C-7B29-AFFA6223C3DC}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Shortcodes/OrchardCore.Shortcodes.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{AD2CDEC9-9610-40BC-6979-80CF2469F522}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Sitemaps/OrchardCore.Sitemaps.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{8297BE04-14CE-CDFD-691A-06B705B7B53A}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Spatial/OrchardCore.Spatial.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{658504F1-C6F1-14C7-57E8-9B7BC7336B8F}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Taxonomies/OrchardCore.Taxonomies.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{7B5AA404-38F2-0606-6AF6-4432E7CE544D}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Templates/OrchardCore.Templates.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{DD550974-C8D7-A8F6-35AB-EF42A7BC47F6}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Title/OrchardCore.Title.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{43F03A0D-5CCF-ACD3-D879-77019ECE53CC}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Widgets/OrchardCore.Widgets.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{361B290B-F167-0F9B-923D-047CBA21707A}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.XmlRpc/OrchardCore.XmlRpc.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{166D45CD-125B-D7CA-E524-228CF9C8F525}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Admin/OrchardCore.Admin.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{13450CED-BE34-3CE4-E909-D7B1BB7BC341}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.AdminDashboard/OrchardCore.AdminDashboard.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{4F3DFE7A-E40B-65B1-441C-D4A6F3908D98}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Apis.GraphQL/OrchardCore.Apis.GraphQL.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E6F810E3-996D-B583-DFE5-02B83CA3689C}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.AuditTrail/OrchardCore.AuditTrail.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{7F179247-4389-19D2-61A6-153C0F0C1118}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.AutoSetup/OrchardCore.AutoSetup.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{22D74BE2-78C2-B0FD-28E0-7DD9737F7853}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.BackgroundTasks/OrchardCore.BackgroundTasks.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{EC9131FD-3785-8465-8A0C-440820566A94}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Cors/OrchardCore.Cors.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{39E7E40E-B244-B747-610E-804FE1BFBD83}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.DataLocalization/OrchardCore.DataLocalization.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{A19DAFED-705E-7603-F3DC-65162E87B3C8}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.DataProtection.Azure/OrchardCore.DataProtection.Azure.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{D653DD16-9A08-AC39-6488-AB7124702E7C}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Deployment.Remote/OrchardCore.Deployment.Remote.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{ECC3380B-5983-6836-4B53-C653960EB702}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Deployment/OrchardCore.Deployment.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{EFF218AB-20B3-3FBC-4328-F62893EA816B}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Diagnostics/OrchardCore.Diagnostics.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{D8D90781-F5B4-4611-21D5-5F7E3FAE3B11}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.DynamicCache/OrchardCore.DynamicCache.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{A1F0F21C-D38A-896C-668A-95BEA7B9691F}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Email.Azure/OrchardCore.Email.Azure.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{2552CD4D-6DDA-B8F4-3910-7DC697D0B82F}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Email.Smtp/OrchardCore.Email.Smtp.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{B0B9DF32-33F1-A31D-3FF9-5A362C11944B}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Email/OrchardCore.Email.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{6A9937A9-1744-93BF-18D3-C813AEEAF0BF}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Facebook/OrchardCore.Facebook.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{29F28741-D0CE-2104-7A24-EB82585E9FB2}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Features/OrchardCore.Features.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{3EEDD7D3-B8FE-4C7A-699D-5F3DE55864A9}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.GitHub/OrchardCore.GitHub.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{28BD32B1-4BBB-AD38-2B06-458849C22575}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Google/OrchardCore.Google.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{2A4B4929-7F92-3CAD-433E-9B13CA3925C4}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.HealthChecks/OrchardCore.HealthChecks.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{3C4195D9-E225-C4AD-9940-0E350853D8C0}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Https/OrchardCore.Https.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{D83B6895-5D9F-4696-1BBB-8A1A67ECA955}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Localization/OrchardCore.Localization.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{22150B4B-9AE6-5594-03E4-4A0680DF3735}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Microsoft.Authentication/OrchardCore.Microsoft.Authentication.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{43DF07E4-CA0C-402C-ED20-565CE0F107BA}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.MiniProfiler/OrchardCore.MiniProfiler.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{CDEF584C-BE3E-DFE8-3B60-530880A0CCDE}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Navigation/OrchardCore.Navigation.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{1B58CECC-2331-8CC8-9383-E0409C98AC5D}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Notifications/OrchardCore.Notifications.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{61043286-8877-0372-168E-C470F50948C6}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.OpenId/OrchardCore.OpenId.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{16A157E9-262C-244E-542A-1FF81134EF3C}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.ReCaptcha/OrchardCore.ReCaptcha.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{C3C8C89A-B6F3-15A2-9F54-20DC5846CFD4}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Recipes/OrchardCore.Recipes.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{750BEE40-887F-584F-7F86-D56106C8BD00}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Redis/OrchardCore.Redis.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{9B8214F1-0CA8-E420-5294-C5B6B3CE7344}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Resources/OrchardCore.Resources.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{CF214EB3-0356-0DFD-64A5-DC0A5866341E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.ResponseCompression/OrchardCore.ResponseCompression.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{A0C68073-EC93-B2E3-E74F-C4E2D2B08D48}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.ReverseProxy/OrchardCore.ReverseProxy.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{200EE23B-E1B6-5E07-07AC-A957AD23A71C}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Roles/OrchardCore.Roles.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{AF4FE839-60DD-DCB2-DD4D-1CCCF6209AB4}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Scripting/OrchardCore.Scripting.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{A6714166-83F3-CEE7-02F9-248CA643B067}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Security/OrchardCore.Security.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{4D4FB1A4-61F9-B9C1-CB0A-B33200FCCE8B}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Settings/OrchardCore.Settings.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{BC22C854-A5B6-91B8-B7E7-70BDCA98CAC4}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Setup/OrchardCore.Setup.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{22015850-7EAA-99DB-C229-0676AF20626C}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Sms.Azure/OrchardCore.Sms.Azure.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{874C7216-D29C-3A3A-3BFF-C5C5F3D33A1E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Sms/OrchardCore.Sms.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{BEF8ED42-563B-AB82-C9B6-03259CDBDE34}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Tenants/OrchardCore.Tenants.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{26EFEC53-1FE2-0A59-00F1-450C131021CF}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Themes/OrchardCore.Themes.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{C4BE9145-C66A-01A8-C742-4991E5978042}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Twitter/OrchardCore.Twitter.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{8A2F7E29-3356-9461-24D9-4DA7E89B133E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.UrlRewriting/OrchardCore.UrlRewriting.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{92F2BF0E-4748-A636-E96C-3D81848251BD}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Users/OrchardCore.Users.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{D9B113AE-62C5-7EA8-751C-5EE523E2F3E1}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Workflows/OrchardCore.Workflows.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{2350200E-2001-04D6-5058-4D5AC737B24F}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Mvc.Web/OrchardCore.Mvc.Web.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{A2A342CF-AECA-4415-43C4-27544791F90F}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Modules/OrchardCore.Mvc.HelloWorld/OrchardCore.Mvc.HelloWorld.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{0E9333A1-9B9D-A397-25D3-D746EDBABAE3}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Themes/SafeMode/SafeMode.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{7D9460B5-18E6-13BF-4A07-84D369EABC01}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Themes/TheAdmin/TheAdmin.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E6E1A26C-6993-D7F7-B1E3-81F9651CB149}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Themes/TheAgencyTheme/TheAgencyTheme.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{F74928F3-3DDB-5662-661B-A2FB1877247C}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Themes/TheBlogTheme/TheBlogTheme.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{153F844B-7424-9000-4E51-44FBC6052FC8}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Themes/TheComingSoonTheme/TheComingSoonTheme.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{89DCEB35-2CD6-844C-6380-587C846AA8C9}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore.Themes/TheTheme/TheTheme.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{F4DF038E-1939-9CA2-34C7-B19A63EC1D74}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Abstractions/OrchardCore.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{9B0BF34E-DDF1-CFBC-E46F-D9537CAE471B}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Admin.Abstractions/OrchardCore.Admin.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{76A91AC1-F693-6464-84DC-7A5F4F21FB79}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.AdminMenu.Abstractions/OrchardCore.AdminMenu.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{AA09304B-2E3D-BAB0-1386-074603BC05DB}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Apis.GraphQL.Abstractions/OrchardCore.Apis.GraphQL.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{ABF31028-E6B8-AD4E-761D-55902008BD03}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Apis.GraphQL.Client/OrchardCore.Apis.GraphQL.Client.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{C7DB4463-60B1-91FA-F4A4-5A8C8775B55F}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.AuditTrail.Abstractions/OrchardCore.AuditTrail.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{8D6D34D5-AA76-21FF-A3D6-0C02AE6005C3}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Autoroute.Core/OrchardCore.Autoroute.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{547FFE8C-E83C-770B-E1A9-A843A88DD549}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.AzureAI.Core/OrchardCore.AzureAI.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{53FCA1CF-C79A-4FC1-FC71-B5605B7D0B20}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Configuration.KeyVault/OrchardCore.Configuration.KeyVault.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{5F1B849A-6348-45CF-B577-AC04957C849B}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ContentFields.Core/OrchardCore.ContentFields.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{818258E6-584A-46D5-83C0-5DE5D0F09A40}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ContentLocalization.Abstractions/OrchardCore.ContentLocalization.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{77ACF4A4-2C42-AE9D-2EF0-320F038AE389}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ContentManagement.Abstractions/OrchardCore.ContentManagement.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{D8D3DF66-0F0B-EAC7-5154-1337A75E89D4}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ContentManagement.Display/OrchardCore.ContentManagement.Display.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{B7C45518-01F9-FD9E-7E8D-12576008DE9C}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ContentManagement.GraphQL/OrchardCore.ContentManagement.GraphQL.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{F50461E1-1169-15E8-1C7F-4F1E6902FFBD}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ContentManagement/OrchardCore.ContentManagement.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{BAEE8F2A-83AD-6097-CF25-B8006197ED66}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ContentPreview.Abstractions/OrchardCore.ContentPreview.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{4DEF3EFF-53E3-CA79-BAE2-E7953813ABB4}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Contents.Core/OrchardCore.Contents.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{B5EBC41D-BC0D-86EF-48C9-AB1DB7CCDA54}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Contents.TagHelpers/OrchardCore.Contents.TagHelpers.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{56B70B5D-6CA1-CFF4-681A-FD05BF7645FC}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ContentTypes.Abstractions/OrchardCore.ContentTypes.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{48B66362-4FEC-FCA3-A792-05FFBECDE9DA}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Data.Abstractions/OrchardCore.Data.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{74B54E45-067E-33F9-EFB1-BCE05E506C76}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Data.YesSql.Abstractions/OrchardCore.Data.YesSql.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{F1552432-3FFF-C4B9-4667-FBED6AC75D0B}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Data.YesSql/OrchardCore.Data.YesSql.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{85A5AE51-231D-108B-A27F-49C4BBDC4CC5}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Data/OrchardCore.Data.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{832D798C-5194-EA31-15B7-9750334511D6}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Deployment.Abstractions/OrchardCore.Deployment.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{EC37878E-3B6B-1266-63D9-563FB795DBFE}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Deployment.Core/OrchardCore.Deployment.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{776C397F-B36F-9305-EC22-35CCC4766F79}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.DisplayManagement.Abstractions/OrchardCore.DisplayManagement.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{33E6BD70-18C9-5FB6-32B9-A7190955A178}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.DisplayManagement.Liquid/OrchardCore.DisplayManagement.Liquid.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{2F9D4B50-77DC-3527-710A-6A0B2B2CE864}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.DisplayManagement/OrchardCore.DisplayManagement.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{7587EE15-DDBE-38C0-46F3-43094523A433}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.DynamicCache.Abstractions/OrchardCore.DynamicCache.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{09EA149E-697C-E90A-E94D-8B901D734245}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Elasticsearch.Core/OrchardCore.Elasticsearch.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{459839A1-21EA-E3E2-E0B3-1C81A1C6313A}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Email.Abstractions/OrchardCore.Email.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{B2E5B044-1937-12D7-C66A-9855BD493AF7}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Email.Core/OrchardCore.Email.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{1FDF4B4E-817E-CBC5-9745-C9AC99EFD537}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Features.Core/OrchardCore.Features.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{05EFA773-EF0B-69F8-F5E9-75DD20FFD1EE}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Feeds.Abstractions/OrchardCore.Feeds.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{57CCF561-8851-806B-1587-33D08F59205F}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Feeds.Core/OrchardCore.Feeds.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{AFD75A2F-A312-EA09-7BAD-C33D0D060404}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.FileStorage.Abstractions/OrchardCore.FileStorage.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{F14A7206-3D23-AA4C-3C9F-B6FF89F0026A}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.FileStorage.AmazonS3/OrchardCore.FileStorage.AmazonS3.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{CC4D88BC-D3BB-09C4-6668-4E5B50AA4A92}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.FileStorage.AzureBlob/OrchardCore.FileStorage.AzureBlob.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{6CECD2CE-0ED5-CA86-2178-64E150460274}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.FileStorage.FileSystem/OrchardCore.FileStorage.FileSystem.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{ECC1BD69-A8DE-0DEA-5F06-DC6F1816D99F}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Flows.Core/OrchardCore.Flows.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{C904F855-8D16-F8E5-F0DE-63E8FD45FD82}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.HealthChecks.Abstractions/OrchardCore.HealthChecks.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{C274860A-1CC4-DF53-5C38-6E1D8DE032E0}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Indexing.Abstractions/OrchardCore.Indexing.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{4E609009-5AAB-FB06-78F2-147595F21DC8}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Indexing.Core/OrchardCore.Indexing.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{1EA65692-9C03-B357-DF81-3492740F295E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Infrastructure.Abstractions/OrchardCore.Infrastructure.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E18B19A7-222A-1B75-C64A-50E6157796C0}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Infrastructure/OrchardCore.Infrastructure.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{0C9EEC10-9049-BCB4-D8EE-E97EC0C186AA}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Liquid.Abstractions/OrchardCore.Liquid.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{5D6FEF3E-BE29-969C-B1E9-E338B6EE4A2E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Localization.Abstractions/OrchardCore.Localization.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{0484A3BA-3E07-C968-9B31-B524B1E0A497}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Localization.Core/OrchardCore.Localization.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{769B9E14-B111-1798-F3B4-7BE27B07C5E4}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Logging.NLog/OrchardCore.Logging.NLog.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{0719260A-9A32-F598-143C-4BACDF9D92DB}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Logging.Serilog/OrchardCore.Logging.Serilog.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{8C2C9479-3E47-1533-C97D-1B5C4DD1462E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Lucene.Core/OrchardCore.Lucene.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{22506F98-0FF9-19C6-87D3-105E3EB09529}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Markdown.Abstractions/OrchardCore.Markdown.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{D78C9368-3BEE-4BF4-02F6-0A8F5B59695E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Media.Abstractions/OrchardCore.Media.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{DA211E8B-B4A4-5FF8-B046-7DE6957AFF3E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Media.Core/OrchardCore.Media.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E7DE104D-6EC9-1D3B-B6BD-55FD82DA6EE4}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.MetaWeblog.Abstractions/OrchardCore.MetaWeblog.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{2B5CB018-372C-E430-EBDB-F91E8306D93D}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Navigation.Core/OrchardCore.Navigation.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{A36ED2A7-FA0D-0CE9-9C61-C49DB828AFB8}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Notifications.Abstractions/OrchardCore.Notifications.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{5B6A5A13-CEEC-41B1-E148-D92543AEB6B7}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Notifications.Core/OrchardCore.Notifications.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{27329F39-9622-13C7-0ED3-4159B9019BCB}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.OpenId.Core/OrchardCore.OpenId.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{86CC10E7-0801-A372-7D2C-1470C4EDD838}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Queries.Abstractions/OrchardCore.Queries.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{A29CAD70-03B7-A7FC-5605-25EF0020DAE2}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Queries.Core/OrchardCore.Queries.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{02F88605-304A-13A3-6A45-166111C04D8E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ReCaptcha.Core/OrchardCore.ReCaptcha.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{4A7B693F-CF27-9577-2BEA-2FD1EED32D5E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Recipes.Abstractions/OrchardCore.Recipes.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{5643670E-1584-17B0-FB3D-5FF1CF1FA711}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Recipes.Core/OrchardCore.Recipes.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{9C8AB6BA-902B-87EC-2810-1B5AECC028E3}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Redis.Abstractions/OrchardCore.Redis.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{92DEF19E-23A8-65F8-0CA8-9B825DB2FD06}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ResourceManagement.Abstractions/OrchardCore.ResourceManagement.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{82C30F9A-D077-779F-39CA-C0C0FA5F5CC1}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ResourceManagement.Core/OrchardCore.ResourceManagement.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{901F51D7-DDD0-C0CD-DBBC-F9E5969C9EF4}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.ResourceManagement/OrchardCore.ResourceManagement.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{C78FCFDF-7BD6-1D66-BCCE-948AA332D8F1}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Roles.Abstractions/OrchardCore.Roles.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{6A5863A0-BB1B-3527-BCFE-75F610C56C91}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Roles.Core/OrchardCore.Roles.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{2A691722-B363-A84E-BD02-07A51F012D16}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Rules.Abstractions/OrchardCore.Rules.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{546658EA-1715-1520-9FAD-61DD319ED0FF}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Rules.Core/OrchardCore.Rules.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{17FCFF52-0DCC-6C8D-B2B6-0A59C6134E5D}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Scripting.JavaScript/OrchardCore.Scripting.JavaScript.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{796FD535-7329-145C-D782-73E2E98D7883}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Search.Abstractions/OrchardCore.Search.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{28B17E40-8A25-44B8-7BA1-2234937C7286}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Search.Lucene.Abstractions/OrchardCore.Search.Lucene.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{F4DF4A7E-A559-2369-E1A2-E6895B9F2C30}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Seo.Abstractions/OrchardCore.Seo.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{8117E66F-B801-3006-E688-A039D71CFA04}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Settings.Core/OrchardCore.Settings.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{7A776E70-0CFF-B213-2AC9-FC1D2C62F655}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Setup.Abstractions/OrchardCore.Setup.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{68EFAB39-9C40-7190-73D5-C5D7F3794748}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Setup.Core/OrchardCore.Setup.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{2AE71BB1-E4BD-BB7C-CA44-9EBB07D29FC0}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Shells.Azure/OrchardCore.Shells.Azure.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{701BB482-C31A-C930-5E3E-AA8FA195D122}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Shortcodes.Abstractions/OrchardCore.Shortcodes.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{745C9C0E-AAF1-C0D2-8752-2FC717C0D11D}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Sitemaps.Abstractions/OrchardCore.Sitemaps.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{AF35D736-94B6-DC0B-76FB-551D27FD44F3}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Sms.Abstractions/OrchardCore.Sms.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{932DAA59-5EA4-F913-770C-775E636C2C6A}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Sms.Core/OrchardCore.Sms.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{D7A19E92-F38B-973D-FD5F-F3C515BFE372}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.SourceGenerators/OrchardCore.SourceGenerators.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{69D03D89-1D6B-8527-610C-848FFEB708A2}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Taxonomies.Core/OrchardCore.Taxonomies.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{C6A86BDA-7993-C083-BB24-5E183DC6F527}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.UrlRewriting.Abstractions/OrchardCore.UrlRewriting.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{CE2C51B0-E256-84FB-F9B5-B4BF0FFE568E}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.UrlRewriting.Core/OrchardCore.UrlRewriting.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{98086632-3F9B-B4AF-C509-4B8AB42F2DB1}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Users.Abstractions/OrchardCore.Users.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{940DCCA7-B900-1874-335D-C7C1E1DEF504}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Users.Core/OrchardCore.Users.Core.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{6CB6F8A8-9EE5-8B05-F552-D522811093CD}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Workflows.Abstractions/OrchardCore.Workflows.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{DEDE6535-720B-17C6-151D-4C7C10FAD4AB}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.XmlRpc.Abstractions/OrchardCore.XmlRpc.Abstractions.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{0D3C72E6-320B-B432-E042-416C413F112B}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore/OrchardCore.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{9FE1A6E5-02A1-EF30-1915-7BCAB003F388}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Application.Cms.Core.Targets/OrchardCore.Application.Cms.Core.Targets.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{89FCAB29-E380-ABC9-A8C4-0D5C19686926}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Application.Cms.Targets/OrchardCore.Application.Cms.Targets.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E15F6EF2-C59A-9F50-20F7-FACE86840BEB}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Application.Mvc.Targets/OrchardCore.Application.Mvc.Targets.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{51FE6A89-FBB7-ACD3-6567-4ECE59AD16CC}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Application.Targets/OrchardCore.Application.Targets.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{24A98950-5BD2-3F4A-4CDB-8EB2D0CD9148}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Module.Targets/OrchardCore.Module.Targets.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E86FB80D-7862-4F57-EB5C-407666A0BE3B}" AbsolutePath="/tmp/orchardcore-fixture/src/OrchardCore/OrchardCore.Theme.Targets/OrchardCore.Theme.Targets.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{12673A4C-5EDF-07CF-E6F5-70D6D3ED2A33}" AbsolutePath="/tmp/orchardcore-fixture/src/Templates/OrchardCore.ProjectTemplates/OrchardCore.ProjectTemplates.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{B5051AAB-EAF3-CFEE-D3BF-B33CB1B69822}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Abstractions.Tests/OrchardCore.Abstractions.Tests.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{FB04B494-C8E0-B251-A9F3-1D4BF3BFC7A1}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Benchmarks/OrchardCore.Benchmarks.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{4B5AFF27-704A-C7FB-3E0F-FF8E61ACA31D}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Functional/OrchardCore.Tests.Functional.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{9C53D6DD-5573-0EA1-748C-A00398F76BFB}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Integration/OrchardCore.Tests.Integration.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{332F7685-076B-AFF6-EC03-454C598D8BEB}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests/OrchardCore.Tests.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{ECFE29BC-1DFB-3742-3F1F-2F72887F58FE}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Features/Examples.Features.AssyAttrib/Examples.Features.AssyAttrib.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{03AACE55-1FDE-0E88-AF35-6EF68B743D1E}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Modules/BaseThemeSample/BaseThemeSample.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{19726191-19C1-41B6-2285-EDD9FAB3C0FB}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Modules/BaseThemeSample2/BaseThemeSample2.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{8EEC7EB4-0749-8EAE-A7DD-188207E8A5BA}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Modules/DerivedThemeSample/DerivedThemeSample.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E2E90F29-F534-D8B0-5693-2E8C5762B6DA}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Modules/DerivedThemeSample2/DerivedThemeSample2.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{2A893E53-B12B-B739-0E5C-C1912753313F}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Modules/Errors.OrchardCoreModules.TwoPlus/Errors.OrchardCoreModules.TwoPlus.csproj" BuildProjectInSolution="False">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{27E725D7-17E1-35CC-A6FE-61A0D206EF6A}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Modules/Examples.Modules.AssyAttrib.Alpha/Examples.Modules.AssyAttrib.Alpha.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{5733EDD7-37E9-A625-4A21-C453450D3763}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Modules/Examples.Modules.AssyAttrib.Bravo/Examples.Modules.AssyAttrib.Bravo.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{60BFE46E-40C6-9D2B-C80A-7838B7EC1D44}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Modules/Examples.Modules.AssyAttrib.Charlie/Examples.Modules.AssyAttrib.Charlie.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{3151F404-0EB8-1107-E18B-F6C25A0EE76B}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Modules/Examples.OrchardCoreModules.Alpha/Examples.OrchardCoreModules.Alpha.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{81F5584C-526F-A491-BE60-8C8D4CBCF4E2}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Modules/ModuleSample/ModuleSample.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{EC5089F1-EF9F-0DD3-DCA0-5BAB4D621300}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Pages/OrchardCore.Application.Pages/OrchardCore.Application.Pages.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{D2095500-5929-3E32-D4B6-506ED4CA239F}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Pages/OrchardCore.Modules.Pages/Module.Pages/Module.Pages.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{FEAE56DC-28FA-3BB9-7A75-80AE2CB56705}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Pages/OrchardCore.Themes.Pages/Theme.Pages/Theme.Pages.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{495871A8-51DB-3D47-6716-883CEE7101F2}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Themes/Errors.OrchardCoreThemes.ThemeAndModule/Errors.OrchardCoreThemes.ThemeAndModule.csproj" BuildProjectInSolution="False">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{F5B52595-99A1-A6BB-801D-394FE3DA442C}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Themes/Errors.OrchardCoreThemes.TwoPlus/Errors.OrchardCoreThemes.TwoPlus.csproj" BuildProjectInSolution="False">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{0B88149A-31D0-418B-82C9-0C9F95000546}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Themes/Examples.OrchardCoreThemes.Alpha/Examples.OrchardCoreThemes.Alpha.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{B207FB71-AED3-63C5-0AC0-3A8A464FD3D0}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Themes/Examples.Themes.AssyAttrib.Alpha/Examples.Themes.AssyAttrib.Alpha.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{E2E30FE1-4497-CB55-E89C-152ADCFA54C1}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Themes/Examples.Themes.AssyAttrib.Bravo/Examples.Themes.AssyAttrib.Bravo.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
  <ProjectConfiguration Project="{6C847F54-0082-AE6F-AA8F-F888688D9EF3}" AbsolutePath="/tmp/orchardcore-fixture/test/OrchardCore.Tests.Themes/Examples.Themes.AssyAttrib.Charlie/Examples.Themes.AssyAttrib.Charlie.csproj" BuildProjectInSolution="True">Debug|AnyCPU</ProjectConfiguration>
</SolutionConfiguration>
""";
}
