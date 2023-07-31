// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public sealed class WriteAppConfigWithSupportedRuntime : TaskBase
    {
        /// <summary>
        /// Path to the app.config source file.
        /// </summary>
        public ITaskItem AppConfigFile { get; set; }

        [Required]
        public string TargetFrameworkIdentifier { get; set; }

        [Required]
        public string TargetFrameworkVersion { get; set; }

        public string TargetFrameworkProfile { get; set; }

        /// <summary>
        /// Path to an intermediate file where we can write the input app.config plus the generated startup supportedRuntime
        /// </summary>
        [Required]
        public ITaskItem OutputAppConfigFile { get; set; }

        protected override void ExecuteCore()
        {
            XDocument doc = LoadAppConfig(AppConfigFile);

            AddSupportedRuntimeToAppconfig(doc, TargetFrameworkIdentifier, TargetFrameworkVersion, TargetFrameworkProfile);

            var fileStream = new FileStream(
                OutputAppConfigFile.ItemSpec,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);

            using (var stream = new StreamWriter(fileStream))
            {
                doc.Save(stream);
            }
        }

        public static void AddSupportedRuntimeToAppconfig(
            XDocument doc,
            string targetFrameworkIdentifier,
            string targetFrameworkVersion,
            string targetFrameworkProfile = null)
        {
            XElement startupNode = doc.Root
                                      .Nodes()
                                      .OfType<XElement>()
                                      .FirstOrDefault(e => e.Name.LocalName == "startup");

            string runtimeVersion = string.Empty;
            if (!HasExistingSupportedRuntime(startupNode))
            {
                if (TryGetSupportRuntimeNode(
                    targetFrameworkIdentifier,
                    targetFrameworkVersion,
                    targetFrameworkProfile,
                    runtimeVersion,
                    out XElement supportedRuntime))
                {
                    if (startupNode == null)
                    {
                        startupNode = new XElement("startup");
                        doc.Root.Add(startupNode);
                    }

                    startupNode.Add(supportedRuntime);
                }
            }
        }

        private static bool HasExistingSupportedRuntime(XElement startupNode)
        {
            return startupNode != null
                  && startupNode.Nodes().OfType<XElement>().Any(e => e.Name.LocalName == "supportedRuntime");
        }

        //https://github.com/dotnet/docs/blob/main/docs/framework/configure-apps/file-schema/startup/supportedruntime-element.md
        private static bool TryGetSupportRuntimeNode(
            string targetFrameworkIdentifier,
            string targetFrameworkVersion,
            string targetFrameworkProfile,
            string runtimeVersion,
            out XElement supportedRuntime)
        {
            supportedRuntime = null;

            if (targetFrameworkIdentifier == ".NETFramework"
                && Version.TryParse(targetFrameworkVersion.TrimStart('v', 'V'), out Version parsedVersion))
            {
                if (parsedVersion.Major < 4)
                {
                    string supportedRuntimeVersion = null;

                    if (parsedVersion.Major == 1 && parsedVersion.Minor >= 0 && parsedVersion.Minor > 1)
                    {
                        supportedRuntimeVersion = "v1.0.3705";
                    }
                    else if (parsedVersion.Major == 1 && parsedVersion.Minor >= 1)
                    {
                        supportedRuntimeVersion = "v1.1.4322";
                    }
                    else if (parsedVersion.Major >= 2 && parsedVersion.Major < 4)
                    {
                        supportedRuntimeVersion = "v2.0.50727";
                    }

                    if (supportedRuntimeVersion == null)
                    {
                        return false;
                    }

                    supportedRuntime =
                           new XElement(
                               "supportedRuntime",
                               new XAttribute("version", supportedRuntimeVersion));

                    return true;
                }
                else if (parsedVersion.Major == 4)
                {
                    string profileInSku = targetFrameworkProfile != null ? $",Profile={targetFrameworkProfile}" : string.Empty;
                    supportedRuntime =
                        new XElement(
                            "supportedRuntime",
                            new XAttribute("version", "v4.0"),
                                new XAttribute("sku", $"{targetFrameworkIdentifier},Version={targetFrameworkVersion}{profileInSku}"));

                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Load or create App.Config
        /// </summary>
        private XDocument LoadAppConfig(ITaskItem appConfigItem)
        {
            XDocument document;
            if (appConfigItem == null)
            {
                document =
                    new XDocument(
                        new XDeclaration("1.0", "utf-8", "true"),
                            new XElement("configuration"));
            }
            else
            {
                document = XDocument.Load(appConfigItem.ItemSpec);
                if (document.Root == null || document.Root.Name != "configuration")
                {
                    throw new BuildErrorException(Strings.AppConfigRequiresRootConfiguration);
                }
            }

            return document;
        }
    }
}
