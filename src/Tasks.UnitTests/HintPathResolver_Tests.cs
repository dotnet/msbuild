using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests
{
    public class HintPathResolver_Tests : IDisposable
    {
        private readonly TestEnvironment _env;

        public HintPathResolver_Tests(ITestOutputHelper testOutput)
        {
            _env = TestEnvironment.Create(testOutput);
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        [Fact]
        public void CanResolveHintPath()
        {
            var tempFile = _env.CreateFile("FakeSystem.Net.Http.dll", "");
            bool result = ResolveHintPath(tempFile.Path);

            result.ShouldBe(true);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void CanResolveLongNonNormalizedHintPath()
        {
            var tempfolder = _env.DefaultTestDirectory.CreateDirectory("tempfolder_for_CanResolveLongHintPath");
            tempfolder.CreateFile("FakeSystem.Net.Http.dll");
            var longTempFilePath = tempfolder.Path + "\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\..\\tempfolder_for_CanResolveLongHintPath\\FakeSystem.Net.Http.dll";
            bool result = ResolveHintPath(longTempFilePath);

            result.ShouldBe(true);
        }

        [Fact]
        public void CanNotResolveHintPathWithNewLine()
        {
            var tempFile = _env.CreateFile("FakeSystem.Net.Http.dll", "");
            bool result = ResolveHintPath(Environment.NewLine + tempFile.Path + Environment.NewLine);

            result.ShouldBe(false);
        }

        [Fact]
        public void CanNotResolveHintPathWithSpace()
        {
            var tempFile = _env.CreateFile("FakeSystem.Net.Http.dll", "");
            bool result = ResolveHintPath("  " + tempFile.Path + "  ");

            result.ShouldBe(false);
        }
        private bool ResolveHintPath(string hintPath)
        {
            var hintPathResolver = new HintPathResolver(
                searchPathElement: "{HintPathFromItem}",
                getAssemblyName: (path) => throw new NotImplementedException(), // not called in this code path
                fileExists: p => FileUtilities.FileExistsNoThrow(p),
                getRuntimeVersion: (path) => throw new NotImplementedException(), // not called in this code path
                targetedRuntimeVesion: Version.Parse("4.0.30319"));

            var result = hintPathResolver.Resolve(new AssemblyNameExtension("FakeSystem.Net.Http"),
                sdkName: "",
                rawFileNameCandidate: "FakeSystem.Net.Http",
                isPrimaryProjectReference: true,
                wantSpecificVersion: false,
                executableExtensions: new string[] { ".winmd", ".dll", ".exe" },
                hintPath: hintPath,
                assemblyFolderKey: "",
                assembliesConsideredAndRejected: new List<ResolutionSearchLocation>(),
                foundPath: out var findPath,
                userRequestedSpecificFile: out var userResquestedSpecificFile);
            return result;
        }
    }
}
