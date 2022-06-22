// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
namespace MSBuild
{
    public class ValidateMSBuildPackageDependencyVersions : Task
    {
        [Required]
        public string AppConfig { get; set; }
        [Required]
        public string AssemblyPath { get; set; }

        // Microsoft.Build.Conversion.Core and Microsoft.Build.Engine are deprecated, but they're still used in VS for now. This project doesn't directly reference them, so they don't appear in its output directory.
        // Microsoft.NET.StringTools uses API not available in net35, but since we need it to work for TaskHosts as well, there are simpler versions implemented for that. Ensure it's the right version.
        // Microsoft.Activities.Build and XamlBuildTask are loaded within an AppDomain in the XamlBuildTask after having been loaded from the GAC elsewhere. See https://github.com/dotnet/msbuild/pull/856
        private string[] assembliesToIgnore = { "Microsoft.Build.Conversion.Core", "Microsoft.NET.StringTools.net35", "Microsoft.Build.Engine", "Microsoft.Activities.Build", "XamlBuildTask" };

        public override bool Execute()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(AppConfig);
            XmlNamespaceManager namespaceManager = new(doc.NameTable);
            namespaceManager.AddNamespace("asm", "urn:schemas-microsoft-com:asm.v1");
            bool foundSystemValueTuple = false;
            foreach (XmlElement dependentAssemblyElement in doc.DocumentElement.SelectNodes("/configuration/runtime/asm:assemblyBinding/asm:dependentAssembly[asm:assemblyIdentity][asm:bindingRedirect]", namespaceManager))
            {
                string name = (dependentAssemblyElement.SelectSingleNode("asm:assemblyIdentity", namespaceManager) as XmlElement).GetAttribute("name");
                string version = (dependentAssemblyElement.SelectSingleNode("asm:bindingRedirect", namespaceManager) as XmlElement).GetAttribute("newVersion");
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(version) && !assembliesToIgnore.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    string path = Path.Combine(AssemblyPath, name + ".dll");
                    string assemblyVersion = AssemblyName.GetAssemblyName(path).Version.ToString();
                    if (!version.Equals(assemblyVersion))
                    {
                        // Ensure that the binding redirect is to the GAC version, but
                        // we still ship the version we explicitly reference to let
                        // API consumers bind to it at runtime.
                        // See https://github.com/dotnet/msbuild/issues/6976.
                        if (String.Equals(name, "System.ValueTuple", StringComparison.OrdinalIgnoreCase) && String.Equals(version, "4.0.0.0") && String.Equals(assemblyVersion, "4.0.3.0"))
                        {
                            foundSystemValueTuple = true;
                        }
                        else
                        {
                            Log.LogError($"Binding redirect for '{name}' redirects to a different version ({version}) than MSBuild ships ({assemblyVersion}).");
                        }
                    }
                }
            }
            if (!foundSystemValueTuple)
            {
                Log.LogError("Binding redirect for 'System.ValueTuple' missing.");
            }
            return !Log.HasLoggedErrors;
        }
    }
}
