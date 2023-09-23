// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyModel;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPreserveCompilationContext : SdkTest
    {
        public GivenThatWeWantToPreserveCompilationContext(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("net46", "netstandard1.3", false)]
        [InlineData("netcoreapp1.1", "netstandard1.3", false)]
        [InlineData("netcoreapp2.0", "netstandard2.0", false)]
        [InlineData("netcoreapp2.0", "netstandard2.0", true)]
        [InlineData("netcoreapp3.0", "netstandard2.0", false)]
        [InlineData("netcoreapp3.0", "netstandard2.0", true)]
        public void It_publishes_the_project_with_a_refs_folder_and_correct_deps_file(string appTargetFramework, string libraryTargetFramework, bool withoutCopyingRefs)
        {
            if (appTargetFramework == "net46" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testLibraryProject = new TestProject()
            {
                Name = "TestLibrary",
                TargetFrameworks = libraryTargetFramework
            };

            var testProject = new TestProject()
            {
                Name = "TestApp",
                IsExe = true,
                TargetFrameworks = appTargetFramework,
                RuntimeIdentifier = $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86"
            };

            testProject.AdditionalProperties["PreserveCompilationContext"] = "true";

            if (withoutCopyingRefs)
            {
                testProject.AdditionalProperties["PreserveCompilationReferences"] = "false";
            }

            testProject.ReferencedProjects.Add(testLibraryProject);
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("System.Data.SqlClient", "4.4.3"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: appTargetFramework + withoutCopyingRefs);

            var getValuesCommand = new GetValuesCommand(testAsset, "LangVersion");
            getValuesCommand.Execute().Should().Pass();

            var langVersion = getValuesCommand.GetValues().FirstOrDefault() ?? string.Empty;

            var publishCommand = new PublishCommand(testAsset);

            publishCommand
                .Execute()
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(appTargetFramework, runtimeIdentifier: $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86");

            publishDirectory.Should().HaveFiles(new[] {
                appTargetFramework == "net46" ? "TestApp.exe" : "TestApp.dll",
                "TestLibrary.dll",
                "Newtonsoft.Json.dll"});

            var refsDirectory = new DirectoryInfo(Path.Combine(publishDirectory.FullName, "refs"));

            if (withoutCopyingRefs)
            {
                refsDirectory.Should().NotExist();
            }
            else
            {
                // Should have compilation time assemblies
                refsDirectory.Should().HaveFile("System.IO.dll");
                // Libraries in which lib==ref should be deduped
                refsDirectory.Should().NotHaveFile("TestLibrary.dll");
                refsDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");
            }

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, "TestApp.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                string[] expectedDefines;
                if (appTargetFramework == "net46")
                {
                    expectedDefines = new[] { "DEBUG", "TRACE", "NETFRAMEWORK", "NET46" };
                }
                else
                {
                    expectedDefines = new[] { "DEBUG", "TRACE", "NETCOREAPP", appTargetFramework.ToUpperInvariant().Replace('.', '_') };
                }

                dependencyContext.CompilationOptions.Defines.Should().Contain(expectedDefines);
                dependencyContext.CompilationOptions.LanguageVersion.Should().Be(langVersion);
                dependencyContext.CompilationOptions.Platform.Should().Be("x86");
                dependencyContext.CompilationOptions.Optimize.Should().Be(false);
                dependencyContext.CompilationOptions.KeyFile.Should().Be("");
                dependencyContext.CompilationOptions.EmitEntryPoint.Should().Be(true);
                dependencyContext.CompilationOptions.DebugType.Should().Be("portable");

                var compileLibraryAssemblyNames = dependencyContext.CompileLibraries.SelectMany(cl => cl.Assemblies)
                    .Select(a => a.Split('/').Last())
                    .Distinct().ToList();
                var expectedCompileLibraryNames = CompileLibraryNames[appTargetFramework].Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                var extraCompileLibraryNames = compileLibraryAssemblyNames.Except(expectedCompileLibraryNames).ToList();
                var missingCompileLibraryNames = expectedCompileLibraryNames.Except(compileLibraryAssemblyNames).ToList();

                if (extraCompileLibraryNames.Any())
                {
                    Log.WriteLine("Unexpected compile libraries: " + string.Join(' ', extraCompileLibraryNames));
                }
                if (missingCompileLibraryNames.Any())
                {
                    Log.WriteLine("Missing compile libraries: " + string.Join(' ', missingCompileLibraryNames));
                }

                compileLibraryAssemblyNames.Should().BeEquivalentTo(expectedCompileLibraryNames);

                // Ensure P2P references are specified correctly
                var testLibrary = dependencyContext
                    .CompileLibraries
                    .FirstOrDefault(l => string.Equals(l.Name, "testlibrary", StringComparison.OrdinalIgnoreCase));

                testLibrary.Assemblies.Count.Should().Be(1);
                testLibrary.Assemblies[0].Should().Be("TestLibrary.dll");

                // Ensure framework references are specified correctly
                if (appTargetFramework == "net46")
                {
                    var mscorlibLibrary = dependencyContext
                        .CompileLibraries
                        .FirstOrDefault(l => string.Equals(l.Name, "mscorlib", StringComparison.OrdinalIgnoreCase));
                    mscorlibLibrary.Assemblies.Count.Should().Be(1);
                    mscorlibLibrary.Assemblies[0].Should().Be(".NETFramework/v4.6/mscorlib.dll");

                    var systemCoreLibrary = dependencyContext
                        .CompileLibraries
                        .FirstOrDefault(l => string.Equals(l.Name, "system.core", StringComparison.OrdinalIgnoreCase));
                    systemCoreLibrary.Assemblies.Count.Should().Be(1);
                    systemCoreLibrary.Assemblies[0].Should().Be(".NETFramework/v4.6/System.Core.dll");

                    var systemCollectionsLibrary = dependencyContext
                        .CompileLibraries
                        .FirstOrDefault(l => string.Equals(l.Name, "system.collections.reference", StringComparison.OrdinalIgnoreCase));
                    systemCollectionsLibrary.Assemblies.Count.Should().Be(1);
                    systemCollectionsLibrary.Assemblies[0].Should().Be(".NETFramework/v4.6/Facades/System.Collections.dll");
                }
            }
        }

        [Fact]
        public void It_excludes_runtime_store_packages_from_the_refs_folder()
        {
            var targetFramework = "netcoreapp2.0";

            var testAsset = _testAssetsManager
                .CopyTestAsset("CompilationContext", "PreserveCompilationContextRefs")
                .WithSource()
                .WithProjectChanges((path, project) =>
                {
                    if (Path.GetFileName(path).Equals("TestApp.csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        var ns = project.Root.Name.Namespace;

                        var targetFrameworkElement = project.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFrameworks").Single();
                        targetFrameworkElement.SetValue(targetFramework);
                    }
                })
                .Restore(Log, "TestApp");

            var manifestFile = Path.Combine(testAsset.TestRoot, "manifest.xml");

            File.WriteAllLines(manifestFile, new[]
            {
                "<StoreArtifacts>",
                $@"  <Package Id=""Newtonsoft.Json"" Version=""{ToolsetInfo.GetNewtonsoftJsonPackageVersion()}"" />",
                @"  <Package Id=""System.Data.SqlClient"" Version=""4.3.0"" />",
                "</StoreArtifacts>",
            });

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            var publishCommand = new PublishCommand(Log, appProjectDirectory);
            publishCommand
                .ExecuteWithoutRestore($"/p:TargetFramework={targetFramework}", $"/p:TargetManifestFiles={manifestFile}")
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86");

            publishDirectory.Should().HaveFiles(new[] {
                "TestApp.dll",
                "TestLibrary.dll"});

            // excluded through TargetManifestFiles
            publishDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");
            publishDirectory.Should().NotHaveFile("System.Data.SqlClient.dll");

            var refsDirectory = new DirectoryInfo(Path.Combine(publishDirectory.FullName, "refs"));
            // Should have compilation time assemblies
            refsDirectory.Should().HaveFile("System.IO.dll");
            // System.Data.SqlClient has separate compile and runtime assemblies, so the compile assembly
            // should be copied to refs even though the runtime assembly is listed in TargetManifestFiles
            refsDirectory.Should().HaveFile("System.Data.SqlClient.dll");

            // Libraries in which lib==ref should be deduped
            refsDirectory.Should().NotHaveFile("TestLibrary.dll");
            refsDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");
        }

        Dictionary<string, string> CompileLibraryNames = new()
        {
            { "net46",
@"TestApp.exe
mscorlib.dll
System.ComponentModel.Composition.dll
System.Core.dll
System.Data.Common.dll
System.Data.dll
System.Data.SqlClient.dll
System.dll
System.Drawing.dll
System.IO.Compression.FileSystem.dll
System.Numerics.dll
System.Runtime.Serialization.dll
System.Xml.dll
System.Xml.Linq.dll
System.Collections.Concurrent.dll
System.Collections.dll
System.ComponentModel.Annotations.dll
System.ComponentModel.dll
System.ComponentModel.EventBasedAsync.dll
System.Diagnostics.Contracts.dll
System.Diagnostics.Debug.dll
System.Diagnostics.Tools.dll
System.Diagnostics.Tracing.dll
System.Dynamic.Runtime.dll
System.Globalization.dll
System.IO.dll
System.Linq.dll
System.Linq.Expressions.dll
System.Linq.Parallel.dll
System.Linq.Queryable.dll
System.Net.NetworkInformation.dll
System.Net.Primitives.dll
System.Net.Requests.dll
System.Net.WebHeaderCollection.dll
System.ObjectModel.dll
System.Reflection.dll
System.Reflection.Emit.dll
System.Reflection.Emit.ILGeneration.dll
System.Reflection.Emit.Lightweight.dll
System.Reflection.Extensions.dll
System.Reflection.Primitives.dll
System.Resources.ResourceManager.dll
System.Runtime.dll
System.Runtime.Extensions.dll
System.Runtime.Handles.dll
System.Runtime.InteropServices.dll
System.Runtime.InteropServices.WindowsRuntime.dll
System.Runtime.Numerics.dll
System.Runtime.Serialization.Json.dll
System.Runtime.Serialization.Primitives.dll
System.Runtime.Serialization.Xml.dll
System.Security.Principal.dll
System.ServiceModel.Duplex.dll
System.ServiceModel.Http.dll
System.ServiceModel.NetTcp.dll
System.ServiceModel.Primitives.dll
System.ServiceModel.Security.dll
System.Text.Encoding.dll
System.Text.Encoding.Extensions.dll
System.Text.RegularExpressions.dll
System.Threading.dll
System.Threading.Tasks.dll
System.Threading.Tasks.Parallel.dll
System.Threading.Timer.dll
System.Xml.XDocument.dll
System.Xml.XmlSerializer.dll
Microsoft.Win32.Primitives.dll
Newtonsoft.Json.dll
System.AppContext.dll
System.Console.dll
System.Globalization.Calendars.dll
System.IO.Compression.dll
System.IO.Compression.ZipFile.dll
System.IO.FileSystem.dll
System.IO.FileSystem.Primitives.dll
System.Net.Http.dll
System.Net.Sockets.dll
System.Runtime.InteropServices.RuntimeInformation.dll
System.Security.Cryptography.Algorithms.dll
System.Security.Cryptography.Encoding.dll
System.Security.Cryptography.Primitives.dll
System.Security.Cryptography.X509Certificates.dll
System.Xml.ReaderWriter.dll
TestLibrary.dll"
            },
            {
                "netcoreapp1.1",
@"TestApp.dll
Microsoft.CSharp.dll
Microsoft.VisualBasic.dll
Microsoft.Win32.Primitives.dll
Newtonsoft.Json.dll
System.AppContext.dll
System.Buffers.dll
System.Collections.dll
System.Collections.Concurrent.dll
System.Collections.Immutable.dll
System.ComponentModel.dll
System.ComponentModel.Annotations.dll
System.Console.dll
System.Collections.NonGeneric.dll
System.ComponentModel.Primitives.dll
System.ComponentModel.TypeConverter.dll
System.Data.Common.dll
System.Data.SqlClient.dll
System.Diagnostics.Debug.dll
System.Diagnostics.DiagnosticSource.dll
System.Diagnostics.Process.dll
System.Diagnostics.Tools.dll
System.Diagnostics.Tracing.dll
System.Dynamic.Runtime.dll
System.Globalization.dll
System.Globalization.Calendars.dll
System.Globalization.Extensions.dll
System.IO.dll
System.IO.Compression.dll
System.IO.Compression.ZipFile.dll
System.IO.FileSystem.dll
System.IO.FileSystem.Primitives.dll
System.IO.FileSystem.Watcher.dll
System.IO.MemoryMappedFiles.dll
System.IO.UnmanagedMemoryStream.dll
System.Linq.dll
System.Linq.Expressions.dll
System.Linq.Parallel.dll
System.Linq.Queryable.dll
System.Net.Http.dll
System.Net.NameResolution.dll
System.Net.Primitives.dll
System.Net.Requests.dll
System.Net.Security.dll
System.Net.Sockets.dll
System.Net.WebHeaderCollection.dll
System.Numerics.Vectors.dll
System.ObjectModel.dll
System.Reflection.dll
System.Reflection.DispatchProxy.dll
System.Reflection.Extensions.dll
System.Reflection.Metadata.dll
System.Reflection.Primitives.dll
System.Reflection.TypeExtensions.dll
System.Resources.Reader.dll
System.Resources.ResourceManager.dll
System.Runtime.dll
System.Runtime.Extensions.dll
System.Runtime.Handles.dll
System.Runtime.InteropServices.dll
System.Runtime.InteropServices.RuntimeInformation.dll
System.Runtime.Numerics.dll
System.Runtime.Serialization.Primitives.dll
System.Runtime.Serialization.Formatters.dll
System.Security.Cryptography.Algorithms.dll
System.Security.Cryptography.Encoding.dll
System.Security.Cryptography.OpenSsl.dll
System.Security.Cryptography.Primitives.dll
System.Security.Cryptography.X509Certificates.dll
System.Security.Principal.dll
System.Text.Encoding.dll
System.Text.Encoding.Extensions.dll
System.Text.RegularExpressions.dll
System.Threading.dll
System.Threading.Tasks.dll
System.Threading.Tasks.Dataflow.dll
System.Threading.Tasks.Extensions.dll
System.Threading.Tasks.Parallel.dll
System.Threading.Thread.dll
System.Threading.ThreadPool.dll
System.Threading.Timer.dll
System.Xml.ReaderWriter.dll
System.Xml.XDocument.dll
System.Xml.XmlDocument.dll
TestLibrary.dll"
            },
            {
                "netcoreapp2.0",
@"TestApp.dll
Microsoft.CSharp.dll
Microsoft.VisualBasic.dll
Microsoft.Win32.Primitives.dll
mscorlib.dll
netstandard.dll
Newtonsoft.Json.dll
System.AppContext.dll
System.Buffers.dll
System.Collections.Concurrent.dll
System.Collections.dll
System.Collections.Immutable.dll
System.Collections.NonGeneric.dll
System.Collections.Specialized.dll
System.ComponentModel.Annotations.dll
System.ComponentModel.Composition.dll
System.ComponentModel.DataAnnotations.dll
System.ComponentModel.dll
System.ComponentModel.EventBasedAsync.dll
System.ComponentModel.Primitives.dll
System.ComponentModel.TypeConverter.dll
System.Configuration.dll
System.Console.dll
System.Core.dll
System.Data.Common.dll
System.Data.dll
System.Data.SqlClient.dll
System.Diagnostics.Contracts.dll
System.Diagnostics.Debug.dll
System.Diagnostics.DiagnosticSource.dll
System.Diagnostics.FileVersionInfo.dll
System.Diagnostics.Process.dll
System.Diagnostics.StackTrace.dll
System.Diagnostics.TextWriterTraceListener.dll
System.Diagnostics.Tools.dll
System.Diagnostics.TraceSource.dll
System.Diagnostics.Tracing.dll
System.dll
System.Drawing.dll
System.Drawing.Primitives.dll
System.Dynamic.Runtime.dll
System.Globalization.Calendars.dll
System.Globalization.dll
System.Globalization.Extensions.dll
System.IO.Compression.dll
System.IO.Compression.FileSystem.dll
System.IO.Compression.ZipFile.dll
System.IO.dll
System.IO.FileSystem.dll
System.IO.FileSystem.DriveInfo.dll
System.IO.FileSystem.Primitives.dll
System.IO.FileSystem.Watcher.dll
System.IO.IsolatedStorage.dll
System.IO.MemoryMappedFiles.dll
System.IO.Pipes.dll
System.IO.UnmanagedMemoryStream.dll
System.Linq.dll
System.Linq.Expressions.dll
System.Linq.Parallel.dll
System.Linq.Queryable.dll
System.Net.dll
System.Net.Http.dll
System.Net.HttpListener.dll
System.Net.Mail.dll
System.Net.NameResolution.dll
System.Net.NetworkInformation.dll
System.Net.Ping.dll
System.Net.Primitives.dll
System.Net.Requests.dll
System.Net.Security.dll
System.Net.ServicePoint.dll
System.Net.Sockets.dll
System.Net.WebClient.dll
System.Net.WebHeaderCollection.dll
System.Net.WebProxy.dll
System.Net.WebSockets.Client.dll
System.Net.WebSockets.dll
System.Numerics.dll
System.Numerics.Vectors.dll
System.ObjectModel.dll
System.Reflection.DispatchProxy.dll
System.Reflection.dll
System.Reflection.Emit.dll
System.Reflection.Emit.ILGeneration.dll
System.Reflection.Emit.Lightweight.dll
System.Reflection.Extensions.dll
System.Reflection.Metadata.dll
System.Reflection.Primitives.dll
System.Reflection.TypeExtensions.dll
System.Resources.Reader.dll
System.Resources.ResourceManager.dll
System.Resources.Writer.dll
System.Runtime.CompilerServices.VisualC.dll
System.Runtime.dll
System.Runtime.Extensions.dll
System.Runtime.Handles.dll
System.Runtime.InteropServices.dll
System.Runtime.InteropServices.RuntimeInformation.dll
System.Runtime.InteropServices.WindowsRuntime.dll
System.Runtime.Loader.dll
System.Runtime.Numerics.dll
System.Runtime.Serialization.dll
System.Runtime.Serialization.Formatters.dll
System.Runtime.Serialization.Json.dll
System.Runtime.Serialization.Primitives.dll
System.Runtime.Serialization.Xml.dll
System.Security.Claims.dll
System.Security.Cryptography.Algorithms.dll
System.Security.Cryptography.Csp.dll
System.Security.Cryptography.Encoding.dll
System.Security.Cryptography.Primitives.dll
System.Security.Cryptography.X509Certificates.dll
System.Security.dll
System.Security.Principal.dll
System.Security.SecureString.dll
System.ServiceModel.Web.dll
System.ServiceProcess.dll
System.Text.Encoding.dll
System.Text.Encoding.Extensions.dll
System.Text.RegularExpressions.dll
System.Threading.dll
System.Threading.Overlapped.dll
System.Threading.Tasks.Dataflow.dll
System.Threading.Tasks.dll
System.Threading.Tasks.Extensions.dll
System.Threading.Tasks.Parallel.dll
System.Threading.Thread.dll
System.Threading.ThreadPool.dll
System.Threading.Timer.dll
System.Transactions.dll
System.Transactions.Local.dll
System.ValueTuple.dll
System.Web.dll
System.Web.HttpUtility.dll
System.Windows.dll
System.Xml.dll
System.Xml.Linq.dll
System.Xml.ReaderWriter.dll
System.Xml.Serialization.dll
System.Xml.XDocument.dll
System.Xml.XmlDocument.dll
System.Xml.XmlSerializer.dll
System.Xml.XPath.dll
System.Xml.XPath.XDocument.dll
WindowsBase.dll
TestLibrary.dll"
            },
            {
                "netcoreapp3.0",
@"TestApp.dll
Microsoft.CSharp.dll
Microsoft.VisualBasic.dll
Microsoft.VisualBasic.Core.dll
Microsoft.Win32.Primitives.dll
mscorlib.dll
netstandard.dll
Newtonsoft.Json.dll
System.AppContext.dll
System.Buffers.dll
System.Collections.Concurrent.dll
System.Collections.dll
System.Collections.Immutable.dll
System.Collections.NonGeneric.dll
System.Collections.Specialized.dll
System.ComponentModel.Annotations.dll
System.ComponentModel.DataAnnotations.dll
System.ComponentModel.dll
System.ComponentModel.EventBasedAsync.dll
System.ComponentModel.Primitives.dll
System.ComponentModel.TypeConverter.dll
System.Configuration.dll
System.Console.dll
System.Core.dll
System.Data.Common.dll
System.Data.dll
System.Data.DataSetExtensions.dll
System.Data.SqlClient.dll
System.Diagnostics.Contracts.dll
System.Diagnostics.Debug.dll
System.Diagnostics.DiagnosticSource.dll
System.Diagnostics.FileVersionInfo.dll
System.Diagnostics.Process.dll
System.Diagnostics.StackTrace.dll
System.Diagnostics.TextWriterTraceListener.dll
System.Diagnostics.Tools.dll
System.Diagnostics.TraceSource.dll
System.Diagnostics.Tracing.dll
System.dll
System.Drawing.dll
System.Drawing.Primitives.dll
System.Dynamic.Runtime.dll
System.Globalization.Calendars.dll
System.Globalization.dll
System.Globalization.Extensions.dll
System.IO.Compression.dll
System.IO.Compression.FileSystem.dll
System.IO.Compression.ZipFile.dll
System.IO.dll
System.IO.FileSystem.dll
System.IO.FileSystem.DriveInfo.dll
System.IO.FileSystem.Primitives.dll
System.IO.FileSystem.Watcher.dll
System.IO.IsolatedStorage.dll
System.IO.MemoryMappedFiles.dll
System.IO.Pipes.dll
System.IO.UnmanagedMemoryStream.dll
System.Linq.dll
System.Linq.Expressions.dll
System.Linq.Parallel.dll
System.Linq.Queryable.dll
System.Net.dll
System.Net.Http.dll
System.Net.HttpListener.dll
System.Net.Mail.dll
System.Net.NameResolution.dll
System.Net.NetworkInformation.dll
System.Net.Ping.dll
System.Net.Primitives.dll
System.Net.Requests.dll
System.Net.Security.dll
System.Net.ServicePoint.dll
System.Net.Sockets.dll
System.Net.WebClient.dll
System.Net.WebHeaderCollection.dll
System.Net.WebProxy.dll
System.Net.WebSockets.Client.dll
System.Net.WebSockets.dll
System.Numerics.dll
System.Numerics.Vectors.dll
System.ObjectModel.dll
System.Reflection.DispatchProxy.dll
System.Reflection.dll
System.Reflection.Emit.dll
System.Reflection.Emit.ILGeneration.dll
System.Reflection.Emit.Lightweight.dll
System.Reflection.Extensions.dll
System.Reflection.Metadata.dll
System.Reflection.Primitives.dll
System.Reflection.TypeExtensions.dll
System.Resources.Reader.dll
System.Resources.ResourceManager.dll
System.Resources.Writer.dll
System.Runtime.CompilerServices.VisualC.dll
System.Runtime.dll
System.Runtime.CompilerServices.Unsafe.dll
System.Runtime.Extensions.dll
System.Runtime.Handles.dll
System.Runtime.InteropServices.dll
System.Runtime.InteropServices.RuntimeInformation.dll
System.Runtime.InteropServices.WindowsRuntime.dll
System.Runtime.Loader.dll
System.Runtime.Numerics.dll
System.Runtime.Serialization.dll
System.Runtime.Serialization.Formatters.dll
System.Runtime.Serialization.Json.dll
System.Runtime.Serialization.Primitives.dll
System.Runtime.Serialization.Xml.dll
System.Security.Claims.dll
System.Security.Cryptography.Algorithms.dll
System.Security.Cryptography.Csp.dll
System.Security.Cryptography.Encoding.dll
System.Security.Cryptography.Primitives.dll
System.Security.Cryptography.X509Certificates.dll
System.Security.dll
System.Security.Principal.dll
System.Security.SecureString.dll
System.ServiceModel.Web.dll
System.ServiceProcess.dll
System.Text.Encoding.dll
System.Text.Encoding.CodePages.dll
System.Text.Encoding.Extensions.dll
System.Text.Encodings.Web.dll
System.Text.RegularExpressions.dll
System.Threading.dll
System.Threading.Channels.dll
System.Threading.Overlapped.dll
System.Threading.Tasks.Dataflow.dll
System.Threading.Tasks.dll
System.Threading.Tasks.Extensions.dll
System.Threading.Tasks.Parallel.dll
System.Threading.Thread.dll
System.Threading.ThreadPool.dll
System.Threading.Timer.dll
System.Transactions.dll
System.Transactions.Local.dll
System.ValueTuple.dll
System.Web.dll
System.Web.HttpUtility.dll
System.Windows.dll
System.Xml.dll
System.Xml.Linq.dll
System.Xml.ReaderWriter.dll
System.Xml.Serialization.dll
System.Xml.XDocument.dll
System.Xml.XmlDocument.dll
System.Xml.XmlSerializer.dll
System.Xml.XPath.dll
System.Xml.XPath.XDocument.dll
WindowsBase.dll
TestLibrary.dll
System.IO.Compression.Brotli.dll
System.Memory.dll
System.Runtime.Intrinsics.dll
System.Text.Json.dll"
            }
        };
    }
}
