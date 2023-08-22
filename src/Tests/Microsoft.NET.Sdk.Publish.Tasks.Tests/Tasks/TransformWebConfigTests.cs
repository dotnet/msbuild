// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests
{
    public class TransformWebConfigTests
    {

        [Theory]
        [InlineData("Web.config")]
        [InlineData("web.config")]
        [InlineData("web.Config")]
        [InlineData("wEb.CoNfIg")]
        [InlineData("WEB.CONFIG")]
        public void TransformWebConfig_FindWebConfig(string webConfigToSearchFor)
        {

            string projectFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                //Arrange
                CreateDummyFile(projectFolder, webConfigToSearchFor);
                var transformWebConfigTask = new TransformWebConfig();

                var projectFile = Path.Combine(projectFolder, "Test.csproj");

                //Act
                var webConfig = transformWebConfigTask.GetWebConfigFileOrDefault(projectFile, "web.config");

                //Assert
                Assert.Equal(Path.Combine(projectFolder, webConfigToSearchFor), webConfig);
            }
            finally
            {
                if (File.Exists(Path.Combine(projectFolder, webConfigToSearchFor)))
                {
                    File.Delete(Path.Combine(projectFolder, webConfigToSearchFor));
                }
            }
        }

        [Fact]
        public void TransformWebConfig_ReturnDefaultWebConfig()
        {
            string projectFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string fileName = "unrelated.txt";
            try
            {
                //Arrange
                CreateDummyFile(projectFolder, fileName);
                var transformWebConfigTask = new TransformWebConfig();

                var projectFile = Path.Combine(projectFolder, "Test.csproj");

                //Act
                var webConfig = transformWebConfigTask.GetWebConfigFileOrDefault(projectFile, "web.config");

                //Assert
                Assert.Equal(Path.Combine(projectFolder, "web.config"), webConfig);
            }
            finally
            {
                if (File.Exists(Path.Combine(projectFolder, fileName)))
                {
                    File.Delete(Path.Combine(projectFolder, fileName));
                }
            }
        }

        private void CreateDummyFile(string path, string name)
        {
            Directory.CreateDirectory(path);
            using var fs = File.Create(Path.Combine(path, name));
            byte[] info = new UTF8Encoding(true).GetBytes("transformwebconfig_test");
            fs.Write(info, 0, info.Length);
        }
    }
}
