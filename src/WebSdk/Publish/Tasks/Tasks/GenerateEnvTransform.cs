// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks
{
    public class GenerateEnvTransform : Task
    {
        [Required]
        public string WebConfigEnvironmentVariables { get; set; }

        [Required]
        public string[] EnvTransformTemplatePaths { get; set; }

        [Required]
        public string PublishTempDirectory { get; set; }

        [Output]
        public string[] GeneratedTransformFullPaths { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(WebConfigEnvironmentVariables))
            {
                // Nothing to do here.
                return true;
            }

            bool isSuccess = true;

            List<string> generatedFiles = new List<string>();
            foreach (var envTransformTemplatePath in EnvTransformTemplatePaths)
            {
                if (File.Exists(envTransformTemplatePath))
                {
                    string templateContent = File.ReadAllText(envTransformTemplatePath);
                    XDocument templateContentDocument = XDocument.Parse(templateContent);

                    XDocument envTransformDoc = GenerateEnvTransformDocument(templateContentDocument, WebConfigEnvironmentVariables);
                    if (envTransformDoc != null)
                    {
                        string generatedTransformFileName = Path.Combine(PublishTempDirectory, Path.GetFileName(envTransformTemplatePath));
                        envTransformDoc.Save(generatedTransformFileName, SaveOptions.None);
                        generatedFiles.Add(generatedTransformFileName);
                    }
                }
            }

            GeneratedTransformFullPaths = generatedFiles.ToArray();
            return isSuccess;
        }

        public XDocument GenerateEnvTransformDocument(XDocument templateContentDocument, string webConfigEnvironmentVariables)
        {
            if (string.IsNullOrEmpty(webConfigEnvironmentVariables))
            {
                return null;
            }

            if (templateContentDocument == null)
            {
                return null;
            }

            var envVariables = GetEnvironmentVariables(webConfigEnvironmentVariables);
            if (envVariables == null || envVariables.Count == 0)
            {
                return null;
            }

            XDocument updatedContent = templateContentDocument;
            XNamespace xdt = "http://schemas.microsoft.com/XML-Document-Transform";
            foreach (var envVariable in envVariables)
            {
                var envVariableTransform =
                        new XElement("environmentVariable", new XAttribute("name", envVariable.Key),
                        new XAttribute("value", envVariable.Value),
                        new XAttribute(xdt + "Locator", "Match(name)"),
                        new XAttribute(xdt + "Transform", "InsertIfMissing"));

                updatedContent.Descendants("environmentVariables").Single().Add(envVariableTransform);
            }

            return updatedContent;
        }

        public List<KeyValuePair<string, string>> GetEnvironmentVariables(string webConfigEnvironmentVariables)
        {
            if (string.IsNullOrEmpty(webConfigEnvironmentVariables))
            {
                return null;
            }

            var keyValuePairs = new List<KeyValuePair<string, string>>();
            IEnumerable<string> envVars = webConfigEnvironmentVariables.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var envVar in envVars)
            {
                var keyValueArray = envVar.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                string name = keyValueArray.First();
                string value = string.Join("", keyValueArray.Skip(1));
                keyValuePairs.Add(new KeyValuePair<string, string>(keyValueArray[0], value));
            }

            return keyValuePairs;
        }
    }
}
