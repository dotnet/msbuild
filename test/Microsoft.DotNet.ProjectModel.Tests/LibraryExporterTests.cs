using System;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class LibraryExporterTests : TestBase
    {
        [Fact]
        public void GetLibraryExportsWithoutLockFile()
        {

            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary");

            foreach (var lockfile in Directory.GetFiles(testInstance.Path, "project.lock.json"))
            {
                File.Delete(lockfile);
            }

            var builder = new ProjectContextBuilder().WithProjectDirectory(Path.Combine(testInstance.Path, "TestApp"));

            foreach (var context in builder.BuildAllTargets())
            {
                var exporter = context.CreateExporter("Debug");
                var exports = exporter.GetAllExports();
                Assert.NotEmpty(exports);
            }
        }
    }
}
