// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Xml.Linq;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPreserveCompilationContext : SdkTest
    {
        [Fact]
        public void It_publishes_the_project_with_a_refs_folder_and_correct_deps_file()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CompilationContext", "PreserveCompilationContext")
                .WithSource();

            testAsset.Restore("TestApp");
            testAsset.Restore("TestLibrary");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            foreach (var targetFramework in new[] { "net46", "netcoreapp1.0" })
            {
                var publishCommand = new PublishCommand(Stage0MSBuild, appProjectDirectory);

                if (targetFramework == "net46" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    continue;
                }

                publishCommand
                    .Execute($"/p:TargetFramework={targetFramework}")
                    .Should()
                    .Pass();

                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: "win7-x86");

                publishDirectory.Should().HaveFiles(new[] {
                    targetFramework == "net46" ? "TestApp.exe" : "TestApp.dll",
                    "TestLibrary.dll",
                    "Newtonsoft.Json.dll"});

                var refsDirectory = new DirectoryInfo(Path.Combine(publishDirectory.FullName, "refs"));
                // Should have compilation time assemblies
                refsDirectory.Should().HaveFile("System.IO.dll");
                // Libraries in which lib==ref should be deduped
                refsDirectory.Should().NotHaveFile("TestLibrary.dll");
                refsDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");

                using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, "TestApp.deps.json")))
                {
                    var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                    string[] expectedDefines;
                    if (targetFramework == "net46")
                    {
                        expectedDefines = new[] { "DEBUG", "TRACE", "NET46" };
                    }
                    else
                    {
                        expectedDefines = new[] { "DEBUG", "TRACE", "NETCOREAPP1_0" };
                    }

                    dependencyContext.CompilationOptions.Defines.Should().BeEquivalentTo(expectedDefines);
                    dependencyContext.CompilationOptions.LanguageVersion.Should().Be("");
                    dependencyContext.CompilationOptions.Platform.Should().Be("x86");
                    dependencyContext.CompilationOptions.Optimize.Should().Be(false);
                    dependencyContext.CompilationOptions.KeyFile.Should().Be("");
                    dependencyContext.CompilationOptions.EmitEntryPoint.Should().Be(true);
                    dependencyContext.CompilationOptions.DebugType.Should().Be("portable");

                    var compileLibraryNames = dependencyContext.CompileLibraries.Select(cl => cl.Name).Distinct().ToList();
                    var expectedCompileLibraryNames = targetFramework == "net46" ? Net46CompileLibraryNames.Distinct() : NetCoreAppCompileLibraryNames.Distinct();

                    compileLibraryNames.Should().BeEquivalentTo(expectedCompileLibraryNames);

                    // Ensure P2P references are specified correctly
                    var testLibrary = dependencyContext
                        .CompileLibraries
                        .FirstOrDefault(l => string.Equals(l.Name, "testlibrary", StringComparison.OrdinalIgnoreCase));

                    testLibrary.Assemblies.Count.Should().Be(1);
                    testLibrary.Assemblies[0].Should().Be("TestLibrary.dll");

                    // Ensure framework references are specified correctly
                    if (targetFramework == "net46")
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
                            .FirstOrDefault(l => string.Equals(l.Name, "system.collections", StringComparison.OrdinalIgnoreCase));
                        systemCollectionsLibrary.Assemblies.Count.Should().Be(1);
                        systemCollectionsLibrary.Assemblies[0].Should().Be(".NETFramework/v4.6/Facades/System.Collections.dll");
                    }
                }
            }
        }

        string[] Net46CompileLibraryNames = @"testapp
mscorlib
system.componentmodel.composition
system.core
system.data
system
system.drawing
system.io.compression.filesystem
system.numerics
system.runtime.serialization
system.xml
system.xml.linq
system.collections.concurrent
system.collections
system.componentmodel.annotations
system.componentmodel
system.componentmodel.eventbasedasync
system.diagnostics.contracts
system.diagnostics.debug
system.diagnostics.tools
system.diagnostics.tracing
system.dynamic.runtime
system.globalization
system.io
system.linq
system.linq.expressions
system.linq.parallel
system.linq.queryable
system.net.networkinformation
system.net.primitives
system.net.requests
system.net.webheadercollection
system.objectmodel
system.reflection
system.reflection.emit
system.reflection.emit.ilgeneration
system.reflection.emit.lightweight
system.reflection.extensions
system.reflection.primitives
system.resources.resourcemanager
system.runtime
system.runtime.extensions
system.runtime.handles
system.runtime.interopservices
system.runtime.interopservices.windowsruntime
system.runtime.numerics
system.runtime.serialization.json
system.runtime.serialization.primitives
system.runtime.serialization.xml
system.security.principal
system.servicemodel.duplex
system.servicemodel.http
system.servicemodel.nettcp
system.servicemodel.primitives
system.servicemodel.security
system.text.encoding
system.text.encoding.extensions
system.text.regularexpressions
system.threading
system.threading.tasks
system.threading.tasks.parallel
system.threading.timer
system.xml.xdocument
system.xml.xmlserializer
microsoft.netcore.platforms
microsoft.win32.primitives
netstandard.library
newtonsoft.json
system.appcontext
system.collections
system.collections.concurrent
system.console
system.diagnostics.debug
system.diagnostics.diagnosticsource
system.diagnostics.tools
system.diagnostics.tracing
system.globalization
system.globalization.calendars
system.io
system.io.compression
system.io.compression.zipfile
system.io.filesystem
system.io.filesystem.primitives
system.linq
system.linq.expressions
system.net.http
system.net.primitives
system.net.sockets
system.objectmodel
system.reflection
system.reflection.extensions
system.reflection.primitives
system.resources.resourcemanager
system.runtime
system.runtime.extensions
system.runtime.handles
system.runtime.interopservices
system.runtime.interopservices.runtimeinformation
system.runtime.numerics
system.security.cryptography.algorithms
system.security.cryptography.encoding
system.security.cryptography.primitives
system.security.cryptography.x509certificates
system.text.encoding
system.text.encoding.extensions
system.text.regularexpressions
system.threading
system.threading.tasks
system.threading.timer
system.xml.readerwriter
system.xml.xdocument
testlibrary".Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        string [] NetCoreAppCompileLibraryNames = @"testapp
libuv
microsoft.codeanalysis.analyzers
microsoft.codeanalysis.common
microsoft.codeanalysis.csharp
microsoft.codeanalysis.visualbasic
microsoft.csharp
microsoft.netcore.app
microsoft.netcore.dotnethost
microsoft.netcore.dotnethostpolicy
microsoft.netcore.dotnethostresolver
microsoft.netcore.jit
microsoft.netcore.platforms
microsoft.netcore.runtime.coreclr
microsoft.netcore.targets
microsoft.netcore.windows.apisets
microsoft.visualbasic
microsoft.win32.primitives
microsoft.win32.registry
netstandard.library
newtonsoft.json
runtime.any.system.collections
runtime.any.system.diagnostics.tools
runtime.any.system.diagnostics.tracing
runtime.any.system.globalization
runtime.any.system.globalization.calendars
runtime.any.system.io
runtime.any.system.reflection
runtime.any.system.reflection.extensions
runtime.any.system.reflection.primitives
runtime.any.system.resources.resourcemanager
runtime.any.system.runtime
runtime.any.system.runtime.handles
runtime.any.system.runtime.interopservices
runtime.any.system.text.encoding
runtime.any.system.text.encoding.extensions
runtime.any.system.threading.tasks
runtime.any.system.threading.timer
runtime.native.system
runtime.native.system.io.compression
runtime.native.system.net.http
runtime.native.system.net.security
runtime.native.system.security.cryptography
runtime.win.microsoft.win32.primitives
runtime.win.system.console
runtime.win.system.diagnostics.debug
runtime.win.system.io.filesystem
runtime.win.system.net.primitives
runtime.win.system.net.sockets
runtime.win.system.runtime.extensions
runtime.win7-x86.microsoft.netcore.dotnethost
runtime.win7-x86.microsoft.netcore.dotnethostpolicy
runtime.win7-x86.microsoft.netcore.dotnethostresolver
runtime.win7-x86.microsoft.netcore.jit
runtime.win7-x86.microsoft.netcore.runtime.coreclr
runtime.win7-x86.microsoft.netcore.windows.apisets
runtime.win7-x86.runtime.native.system.io.compression
runtime.win7.system.private.uri
system.appcontext
system.buffers
system.collections
system.collections.concurrent
system.collections.immutable
system.componentmodel
system.componentmodel.annotations
system.console
system.diagnostics.debug
system.diagnostics.diagnosticsource
system.diagnostics.fileversioninfo
system.diagnostics.process
system.diagnostics.stacktrace
system.diagnostics.tools
system.diagnostics.tracing
system.dynamic.runtime
system.globalization
system.globalization.calendars
system.globalization.extensions
system.io
system.io.compression
system.io.compression.zipfile
system.io.filesystem
system.io.filesystem.primitives
system.io.filesystem.watcher
system.io.memorymappedfiles
system.io.unmanagedmemorystream
system.linq
system.linq.expressions
system.linq.parallel
system.linq.queryable
system.net.http
system.net.nameresolution
system.net.primitives
system.net.requests
system.net.security
system.net.sockets
system.net.webheadercollection
system.numerics.vectors
system.objectmodel
system.private.uri
system.reflection
system.reflection.dispatchproxy
system.reflection.emit
system.reflection.emit.ilgeneration
system.reflection.emit.lightweight
system.reflection.extensions
system.reflection.metadata
system.reflection.primitives
system.reflection.typeextensions
system.resources.reader
system.resources.resourcemanager
system.runtime
system.runtime.extensions
system.runtime.handles
system.runtime.interopservices
system.runtime.interopservices.runtimeinformation
system.runtime.loader
system.runtime.numerics
system.runtime.serialization.primitives
system.security.claims
system.security.cryptography.algorithms
system.security.cryptography.cng
system.security.cryptography.csp
system.security.cryptography.encoding
system.security.cryptography.openssl
system.security.cryptography.primitives
system.security.cryptography.x509certificates
system.security.principal
system.security.principal.windows
system.text.encoding
system.text.encoding.codepages
system.text.encoding.extensions
system.text.regularexpressions
system.threading
system.threading.overlapped
system.threading.tasks
system.threading.tasks.dataflow
system.threading.tasks.extensions
system.threading.tasks.parallel
system.threading.thread
system.threading.threadpool
system.threading.timer
system.xml.readerwriter
system.xml.xdocument
system.xml.xmldocument
system.xml.xpath
system.xml.xpath.xdocument
testlibrary".Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }
}