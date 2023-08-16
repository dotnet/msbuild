// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    public class StaticWebAssetsGeneratePackagePropsFileTest
    {
        [Fact]
        public void WritesPropsFile_WithProvidedImportPath()
        {
            // Arrange
            var file = Path.GetTempFileName();
            var expectedDocument = @"<Project>
  <Import Project=""Microsoft.AspNetCore.StaticWebAssets.props"" />
</Project>";

            try
            {
                var buildEngine = new Mock<IBuildEngine>();

                var task = new StaticWebAssetsGeneratePackagePropsFile
                {
                    BuildEngine = buildEngine.Object,
                    PropsFileImport = "Microsoft.AspNetCore.StaticWebAssets.props",
                    BuildTargetPath = file
                };

                // Act
                var result = task.Execute();

                // Assert
                result.Should().Be(true);
                var document = File.ReadAllText(file);
                document.Should().Contain(expectedDocument);
            }
            finally
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }
}
