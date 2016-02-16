using System;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class LibraryExporterTests : TestBase
    {
        private readonly string _testProjectsRoot;

        public LibraryExporterTests()
        {
            _testProjectsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects");
        }

        [Fact]
        public void GetLibraryExportsWithoutLockFile()
        {
            var root = Temp.CreateDirectory().CopyDirectory(Path.Combine(_testProjectsRoot, "TestAppWithLibrary"));

            foreach (var lockfile in Directory.GetFiles(root.Path, "project.lock.json"))
            {
                File.Delete(lockfile);
            }

            var builder = new ProjectContextBuilder().WithProjectDirectory(Path.Combine(root.Path, "TestApp"));

            foreach (var context in builder.BuildAllTargets())
            {
                var exporter = context.CreateExporter("Debug");
                var exports = exporter.GetAllExports();
                Assert.NotEmpty(exports);
            }
        }
    }
}
