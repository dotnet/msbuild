// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.Publish.Tasks.Xdt;

namespace Microsoft.NET.Sdk.Publish.Tasks.Tests.Tasks
{
    public class GenerateEnvTransformTests
    {
        private XDocument _environmentTransformWithLocationTemplate => XDocument.Parse(
@"<?xml version=""1.0""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
   <location>
     <system.webServer>
       <aspNetCore>
         <environmentVariables xdt:Transform = ""InsertIfMissing"" />
          </aspNetCore>
        </system.webServer>
    </location>
</configuration>");

        private XDocument _environmentTransformWithoutLocationTemplate => XDocument.Parse(
@"<?xml version=""1.0""?>
<configuration xmlns:xdt=""http://schemas.microsoft.com/XML-Document-Transform"">
     <system.webServer>
       <aspNetCore>
         <environmentVariables xdt:Transform = ""InsertIfMissing"" />
          </aspNetCore>
        </system.webServer>
</configuration>");

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        public void GetEnvironmentVariables_HandlesNullAndEmpty(string value, object expected)
        {
            // Arrange
            GenerateEnvTransform env = new GenerateEnvTransform();

            // Act 
            var envVariables = env.GetEnvironmentVariables(value);

            // Assert
            Assert.Equal(expected, envVariables);
        }

        [Fact]
        public void GenerateEnvTransformDocument_HandlesNullAndEmpty()
        {
            // Arrange
            GenerateEnvTransform env = new GenerateEnvTransform();

            // Act 
            XDocument transformDoc = env.GenerateEnvTransformDocument(null, null);

            // Assert
            Assert.Null(transformDoc);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        public void Execute_DoesnotFail_IfEnvVarIsNullOrEmpty(string envVariable, bool expected)
        {
            // Arrange
            GenerateEnvTransform env = new GenerateEnvTransform()
            {
                WebConfigEnvironmentVariables = envVariable
            };

            // Act 
            bool isSuccess = env.Execute();

            // Assert
            Assert.Equal(expected, isSuccess);

        }

        [Theory]
        [InlineData("envname=envvalue", 1)]
        [InlineData("envname=envvalue;envname2=envvalue2", 2)]
        [InlineData("envname=", 1)]
        [InlineData("=envname", 1)]
        [InlineData("=envname=", 1)]
        [InlineData("=envname=envvalue", 1)]
        [InlineData("envnameWithoutEqual", 1)]
        [InlineData("envname=envvalue;envname2", 2)]
        [InlineData("envnamewithsemicolon=envvalue%3enVVal;", 1)]
        public void GetEnvironmentVariables_Returns_CorrectValues(string value, int expectedCount)
        {
            // Arrange
            GenerateEnvTransform env = new GenerateEnvTransform();

            // Act 
            var envVariables = env.GetEnvironmentVariables(value);

            // Assert
            Assert.Equal(expectedCount, envVariables.Count);
        }

        [Theory]
        [InlineData("envname=envvalue", 1)]
        [InlineData("envname=envvalue;envname2=envvalue2", 2)]
        [InlineData("envname=", 1)]
        [InlineData("=envname", 1)]
        [InlineData("=envname=", 1)]
        [InlineData("=envname=envvalue", 1)]
        [InlineData("envnameWithoutEqual", 1)]
        [InlineData("envname=envvalue;envname2", 2)]
        [InlineData("envname=envvalue;envname2=val2;envName3=val3", 3)]
        [InlineData("envnamewithsemicolon=envvalue%3enVVal;", 1)]
        public void GenerateEnvTransform_GeneretesTransforms_ForAllCases(string envVariables, int expected)
        {
            GenerateEnvTransform env = new GenerateEnvTransform();
            IList<XDocument> templateDocumentList = new List<XDocument>() { _environmentTransformWithLocationTemplate, _environmentTransformWithoutLocationTemplate };

            foreach (var template in templateDocumentList)
            {
                // Act
                XDocument envDoc = env.GenerateEnvTransformDocument(template, envVariables);

                // Assert
                Assert.Equal(expected, envDoc.Descendants("environmentVariable").Count());
            }

        }

        [Theory]
        [InlineData("envname=envvalue", 1)]
        [InlineData("envname=envvalue;envname2=envvalue2", 2)]
        [InlineData("envname=", 1)]
        [InlineData("=envname", 1)]
        [InlineData("=envname=", 1)]
        [InlineData("=envname=envvalue", 1)]
        [InlineData("envnameWithoutEqual", 1)]
        [InlineData("envname=envvalue;envname2", 2)]
        [InlineData("envname=envvalue;envname2=val2;envName3=val3", 3)]
        [InlineData("envnamewithsemicolon=envvalue%3enVVal;", 1)]
        public void Execute_Updates_WebConfig_Correctly(string envVariables, int expected)
        {
            string envTemplatePath = Path.GetTempFileName();
            string webConfigPath = Path.GetTempFileName();
            string tempDir = Path.GetDirectoryName(envTemplatePath);
            try
            {
                // Arrange
                List<XDocument> locationWebConfigTemplateList = new List<XDocument>() {WebConfigTransformTemplates.WebConfigTemplate};
                foreach (var locationWebConfigTemplate in locationWebConfigTemplateList)
                {

                    _environmentTransformWithLocationTemplate.Save(envTemplatePath, SaveOptions.None);
                    XDocument webConfigTemplate = locationWebConfigTemplate;
                    webConfigTemplate.Save(webConfigPath);

                    GenerateEnvTransform env = new GenerateEnvTransform()
                    {
                        WebConfigEnvironmentVariables = envVariables,
                        EnvTransformTemplatePaths = new List<string>() { envTemplatePath }.ToArray(),
                        PublishTempDirectory = tempDir
                    };


                    // Act
                    bool isSuccess = env.Execute();
                    Assert.True(isSuccess);
                    foreach (var generatedPath in env.GeneratedTransformFullPaths)
                    {
                        Assert.True(File.Exists(generatedPath));

                        TransformXml transformTask = new TransformXml()
                        {
                            Source = webConfigPath,
                            Destination = webConfigPath,
                            Transform = generatedPath,
                            SourceRootPath = Path.GetTempPath(),
                            TransformRootPath = Path.GetTempPath(),
                            StackTrace = true
                        };

                        bool success = transformTask.RunXmlTransform(isLoggingEnabled: false);

                        // Assert
                        Assert.Equal(expected, XDocument.Parse(File.ReadAllText(webConfigPath)).Root.Descendants("environmentVariable").Count());
                    }
                }
            }
            finally
            {
                File.Delete(envTemplatePath);
                File.Delete(webConfigPath);
            }
        }

        [Theory]
        [InlineData("envname=envvalue", 1)]
        [InlineData("envname=envvalue;envname2=envvalue2", 2)]
        [InlineData("envname=", 1)]
        [InlineData("=envname", 1)]
        [InlineData("=envname=", 1)]
        [InlineData("=envname=envvalue", 1)]
        [InlineData("envnameWithoutEqual", 1)]
        [InlineData("envname=envvalue;envname2", 2)]
        [InlineData("envname=envvalue;envname2=val2;envName3=val3", 3)]
        [InlineData("envnamewithsemicolon=envvalue%3enVVal;", 1)]
        public void EnvTransform_Updates_WebConfig_Correctly_EvenWithEnvVariable(string envVariables, int expected)
        {
            string envTemplatePath = Path.GetTempFileName();
            string webConfigPath = Path.GetTempFileName();
            string tempDir = Path.GetDirectoryName(envTemplatePath);
            try
            {
                // Arrange
                _environmentTransformWithLocationTemplate.Save(envTemplatePath, SaveOptions.None);
                XDocument webConfigTemplate = WebConfigTransformTemplates.WebConfigTemplateWithEnvironmentVariable;
                webConfigTemplate.Save(webConfigPath);

                GenerateEnvTransform env = new GenerateEnvTransform()
                {
                    WebConfigEnvironmentVariables = envVariables,
                    EnvTransformTemplatePaths = new List<string>() { envTemplatePath }.ToArray(),
                    PublishTempDirectory = tempDir
                };

                // Act
                bool isSuccess = env.Execute();
                Assert.True(isSuccess);
                foreach (var generatedPath in env.GeneratedTransformFullPaths)
                {
                    Assert.True(File.Exists(generatedPath));

                    TransformXml transformTask = new TransformXml()
                    {
                        Source = webConfigPath,
                        Destination = webConfigPath,
                        Transform = generatedPath,
                        SourceRootPath = Path.GetTempPath(),
                        TransformRootPath = Path.GetTempPath(),
                        StackTrace = true
                    };

                    bool success = transformTask.RunXmlTransform(isLoggingEnabled: false);

                    // Assert
                    // Expected should be always one more since an env variable is already present in the web.config.
                    Assert.Equal(expected + 1, XDocument.Parse(File.ReadAllText(webConfigPath)).Root.Descendants("environmentVariable").Count());
                }
            }
            finally
            {
                File.Delete(envTemplatePath);
                File.Delete(webConfigPath);
            }
        }

        [Theory]
        [InlineData("envname=envvalue", 1)]
        [InlineData("envname=envvalue;envname2=envvalue2", 2)]
        [InlineData("envname=", 1)]
        [InlineData("=envname", 1)]
        [InlineData("=envname=", 1)]
        [InlineData("=envname=envvalue", 1)]
        [InlineData("envnameWithoutEqual", 1)]
        [InlineData("envname=envvalue;envname2", 2)]
        [InlineData("envname=envvalue;envname2=val2;envName3=val3", 3)]
        [InlineData("envnamewithsemicolon=envvalue%3enVVal;", 1)]
        public void Execute_Updates_WebConfig_Correctly_WithNoLocation(string envVariables, int expected)
        {
            string envTemplatePath = Path.GetTempFileName();
            string webConfigPath = Path.GetTempFileName();
            string tempDir = Path.GetDirectoryName(envTemplatePath);
            try
            {
                List<XDocument> locationWebConfigTemplateList = new List<XDocument>() {WebConfigTransformTemplates.WebConfigTemplateWithoutLocation,
                                                                               WebConfigTransformTemplates.WebConfigTemplateWithNonRelevantLocationFirst,
                                                                               WebConfigTransformTemplates.WebConfigTemplateWithNonRelevantLocationLast,
                                                                               WebConfigTransformTemplates.WebConfigTemplateWithRelevantLocationFirst,
                                                                               WebConfigTransformTemplates.WebConfigTemplateWithRelevantLocationLast};
                foreach (var locationWebConfigTemplate in locationWebConfigTemplateList)
                {
                    // Arrange
                    _environmentTransformWithoutLocationTemplate.Save(envTemplatePath, SaveOptions.None);
                    XDocument webConfigTemplate = locationWebConfigTemplate;
                    webConfigTemplate.Save(webConfigPath);

                    GenerateEnvTransform env = new GenerateEnvTransform()
                    {
                        WebConfigEnvironmentVariables = envVariables,
                        EnvTransformTemplatePaths = new List<string>() { envTemplatePath }.ToArray(),
                        PublishTempDirectory = tempDir
                    };


                    // Act
                    bool isSuccess = env.Execute();
                    Assert.True(isSuccess);
                    foreach (var generatedPath in env.GeneratedTransformFullPaths)
                    {
                        Assert.True(File.Exists(generatedPath));

                        TransformXml transformTask = new TransformXml()
                        {
                            Source = webConfigPath,
                            Destination = webConfigPath,
                            Transform = generatedPath,
                            SourceRootPath = Path.GetTempPath(),
                            TransformRootPath = Path.GetTempPath(),
                            StackTrace = true
                        };

                        bool success = transformTask.RunXmlTransform(isLoggingEnabled: false);

                        // Assert
                        Assert.Equal(expected, XDocument.Parse(File.ReadAllText(webConfigPath)).Root.Descendants("environmentVariable").Count());
                    }
                }
            }
            finally
            {
                File.Delete(envTemplatePath);
                File.Delete(webConfigPath);
            }
        }
    }
}
