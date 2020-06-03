using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.AssemblyFoldersFromConfig;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests.AssemblyDependency
{
    public class AssemblyFoldersFromConfig_Tests : ResolveAssemblyReferenceTestFixture
    {
        public AssemblyFoldersFromConfig_Tests(ITestOutputHelper output) : base(output)
        {
            s_existentFiles.AddRange(new[]
            {
                Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder1", "assemblyfromconfig1.dll"),
                Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder2", "assemblyfromconfig2.dll"),
                Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder3_x86", "assemblyfromconfig3_x86.dll"),

                Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder_x86", "assemblyfromconfig_common.dll"),
                Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder_x64", "assemblyfromconfig_common.dll"),
                Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder5010x64", "v5assembly.dll"),
                Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder501000x86", "v5assembly.dll")
            });
        }
        
        [Fact]
        public void AssemblyFoldersFromConfigTest()
        {
            var assemblyConfig = Path.GetTempFileName();
            File.WriteAllText(assemblyConfig, TestFile);

            var moniker = "{AssemblyFoldersFromConfig:" + assemblyConfig + ",v4.5}";

            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference
                {
                    BuildEngine = new MockEngine(_output),
                    Assemblies = new ITaskItem[] {new TaskItem("assemblyfromconfig2")},
                    SearchPaths = new[] {moniker}
                };
                
                Execute(t);

                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder2", "assemblyfromconfig2.dll"), t.ResolvedFiles[0].ItemSpec);
                t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(moniker, StringCompareShould.IgnoreCase);
            }
            finally
            {
                FileUtilities.DeleteNoThrow(assemblyConfig);
            }
        }

        [Fact]
        public void AssemblyFoldersFromConfigPlatformSpecificAssemblyFirstTest()
        {
            var assemblyConfig = Path.GetTempFileName();
            File.WriteAllText(assemblyConfig, TestFile);

            var moniker = "{AssemblyFoldersFromConfig:" + assemblyConfig + ",v4.5}";

            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference
                {
                    BuildEngine = new MockEngine(_output),
                    Assemblies = new ITaskItem[] {new TaskItem("assemblyfromconfig_common.dll")},
                    SearchPaths = new[] {moniker},
                    TargetProcessorArchitecture = "x86"
                };

                Execute(t);

                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder_x86", "assemblyfromconfig_common.dll"), t.ResolvedFiles[0].ItemSpec);
                t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(moniker, StringCompareShould.IgnoreCase);
            }
            finally
            {
                FileUtilities.DeleteNoThrow(assemblyConfig);
            }
        }

        [Fact]
        public void AssemblyFoldersFromConfigNormalizeNetFrameworkVersion()
        {
            var assemblyConfig = Path.GetTempFileName();
            File.WriteAllText(assemblyConfig, TestFile);

            var moniker = "{AssemblyFoldersFromConfig:" + assemblyConfig + ",v5.0}";

            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference
                {
                    BuildEngine = new MockEngine(_output),
                    Assemblies = new ITaskItem[] { new TaskItem("v5assembly.dll") },
                    SearchPaths = new[] { moniker },
                    TargetProcessorArchitecture = "x86"
                };

                Execute(t);

                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder501000x86", "v5assembly.dll"), t.ResolvedFiles[0].ItemSpec);
                t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(moniker, StringCompareShould.IgnoreCase);

                // Try again changing only the processor architecture
                t = new ResolveAssemblyReference
                {
                    BuildEngine = new MockEngine(_output),
                    Assemblies = new ITaskItem[] { new TaskItem("v5assembly.dll") },
                    SearchPaths = new[] { moniker },
                    TargetProcessorArchitecture = "AMD64"
                };

                Execute(t);

                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder5010x64", "v5assembly.dll"), t.ResolvedFiles[0].ItemSpec);
                t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(moniker, StringCompareShould.IgnoreCase);
            }
            finally
            {
                FileUtilities.DeleteNoThrow(assemblyConfig);
            }
        }

        [Fact]
        public void AssemblyFoldersFromConfigFileNotFoundTest()
        {
            var assemblyConfig = Path.GetTempFileName();
            File.Delete(assemblyConfig);
            var moniker = "{AssemblyFoldersFromConfig:" + assemblyConfig + ",v4.5}";

            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference
                {
                    BuildEngine = new MockEngine(_output),
                    Assemblies = new ITaskItem[] {new TaskItem("assemblyfromconfig_common.dll")},
                    SearchPaths = new[] {moniker},
                    TargetProcessorArchitecture = "x86"
                };


                Assert.Throws<InternalErrorException>(() => Execute(t));
            }
            finally
            {
                FileUtilities.DeleteNoThrow(assemblyConfig);
            }
        }

        [Fact]
        public void AssemblyFoldersFromConfigFileMalformed()
        {
            var assemblyConfig = Path.GetTempFileName();
            File.WriteAllText(assemblyConfig, "<<<>><>!" + TestFile);

            var moniker = "{AssemblyFoldersFromConfig:" + assemblyConfig + ",v4.5}";

            try
            {
                MockEngine engine = new MockEngine(_output);
                ResolveAssemblyReference t = new ResolveAssemblyReference
                {
                    BuildEngine = engine,
                    Assemblies = new ITaskItem[] { new TaskItem("assemblyfromconfig2") },
                    SearchPaths = new[] { moniker }
                };

                var success = Execute(t);

                Assert.False(success);
                Assert.Empty(t.ResolvedFiles);
                engine.AssertLogContains(") specified in Microsoft.Common.CurrentVersion.targets was invalid. The error was: ");
            }
            finally
            {
                FileUtilities.DeleteNoThrow(assemblyConfig);
            }
        }

        private readonly string TestFile = @"
<AssemblyFoldersConfig>
  <AssemblyFolders>
    <AssemblyFolder>
      <Name>Test Assemblies</Name>
      <FrameworkVersion>v5.0</FrameworkVersion>
      <Path>" + Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder1") + @"</Path>
    </AssemblyFolder>
    <AssemblyFolder>
      <Name>Test Assemblies2</Name>
      <FrameworkVersion>v4.5.25000</FrameworkVersion>
      <Path>" + Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder2") + @"</Path>
    </AssemblyFolder>
    <AssemblyFolder>
      <FrameworkVersion>v4.0</FrameworkVersion>
      <Path>" + Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder3") + @"</Path>
    </AssemblyFolder>
    <AssemblyFolder>
      <Name>Platform Specific</Name>
      <FrameworkVersion>v4.5.25000</FrameworkVersion>
      <Path>" + Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder_x64") + @"</Path>
      <Platform>x64</Platform>
    </AssemblyFolder>
    <AssemblyFolder>
      <FrameworkVersion>v4.5</FrameworkVersion>
      <Path>" + Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder_x86") + @"</Path>
      <Platform>x86</Platform>
    </AssemblyFolder>

    <AssemblyFolder>
      <FrameworkVersion>v5.0.1.0</FrameworkVersion>
      <Path>" + Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder5010x64") + @"</Path>
      <Platform>x64</Platform>
    </AssemblyFolder>
    <AssemblyFolder>
      <FrameworkVersion>v5.0.100.0</FrameworkVersion>
      <Path>" + Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder501000x86") + @"</Path>
      <Platform>x86</Platform>
    </AssemblyFolder>
  </AssemblyFolders>
</AssemblyFoldersConfig>
";
    }
}
