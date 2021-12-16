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

        private string[] assembliesToIgnore = { "Microsoft.Build.Conversion.Core", "Microsoft.NET.StringTools.net35", "Microsoft.Build.Engine", "Microsoft.Activities.Build", "XamlBuildTask" };

        public override bool Execute()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(AppConfig);
            var runtime = doc.SelectSingleNode("configuration").SelectSingleNode("runtime");
            bool foundSystemValueTuple = false;
            foreach (var node in runtime.ChildNodes)
            {
                if (node is XmlElement assemblyBinding && assemblyBinding.Name.Equals("assemblyBinding"))
                {
                    foreach (var assemblyBindingElement in assemblyBinding.ChildNodes)
                    {
                        string name = string.Empty;
                        string version = string.Empty;
                        if (assemblyBindingElement is not XmlElement dependentAssemblyElement)
                        {
                            continue;
                        }
                        foreach (var dependentAssembly in dependentAssemblyElement.ChildNodes)
                        {
                            if (dependentAssembly is XmlElement dependentAssemblyXmlElement)
                            {
                                if (dependentAssemblyXmlElement.Name.Equals("assemblyIdentity", StringComparison.OrdinalIgnoreCase))
                                {
                                    name = dependentAssemblyXmlElement.GetAttribute("name");
                                }
                                else if (dependentAssemblyXmlElement.Name.Equals("bindingRedirect", StringComparison.OrdinalIgnoreCase))
                                {
                                    version = dependentAssemblyXmlElement.GetAttribute("newVersion");
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(version) && !assembliesToIgnore.Contains(name, StringComparer.OrdinalIgnoreCase))
                        {
                            string path = Path.Combine(AssemblyPath, name + ".dll");
                            string assemblyVersion = AssemblyName.GetAssemblyName(path).Version.ToString();
                            if (!version.Equals(assemblyVersion))
                            {
                                // It is unusual to want to redirect down, but in this case it's ok: 4.0.3.0 forwards to 4.0.0.0 in the GAC, so this just removes the need to redistribute a file
                                // and makes that resolution faster. Still verify that the versions are exactly as in this comment, as that may change.
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
