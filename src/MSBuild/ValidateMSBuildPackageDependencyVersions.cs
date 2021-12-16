using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
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

        public override bool Execute()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(AppConfig);
            foreach (var topLevelElement in doc.ChildNodes)
            {
                if (topLevelElement is XmlElement topLevelXmlElement && topLevelXmlElement.Name.Equals("configuration", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var configurationElement in topLevelXmlElement.ChildNodes)
                    {
                        if (configurationElement is XmlElement configurationXmlElement && configurationXmlElement.Name.Equals("runtime", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var runtimeElement in configurationXmlElement.ChildNodes)
                            {
                                if (runtimeElement is XmlElement runtimeXmlElement && runtimeXmlElement.Name.Equals("assemblyBinding", StringComparison.OrdinalIgnoreCase))
                                {
                                    foreach (var assemblyBindingElement in runtimeXmlElement.ChildNodes)
                                    {
                                        if (assemblyBindingElement is XmlElement assemblyBindingXmlElement && assemblyBindingXmlElement.Name.Equals("dependentAssembly", StringComparison.OrdinalIgnoreCase))
                                        {
                                            string name = string.Empty;
                                            string version = string.Empty;
                                            foreach (var dependentAssembly in assemblyBindingXmlElement.ChildNodes)
                                            {
                                                if (dependentAssembly is XmlElement dependentAssemblyXmlElement)
                                                {
                                                    if (dependentAssemblyXmlElement.Name.Equals("assemblyIdentity", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        foreach (var assemblyIdentityAttribute in dependentAssemblyXmlElement.Attributes)
                                                        {
                                                            if (assemblyIdentityAttribute is XmlAttribute assemblyIdentityAttributeXmlElement && assemblyIdentityAttributeXmlElement.Name.Equals("name", StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                name = assemblyIdentityAttributeXmlElement.Value;
                                                            }
                                                        }
                                                    }
                                                    else if (dependentAssemblyXmlElement.Name.Equals("bindingRedirect", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        foreach (var bindingRedirectAttribute in dependentAssemblyXmlElement.Attributes)
                                                        {
                                                            if (bindingRedirectAttribute is XmlAttribute bindingRedirectAttributeXmlElement && bindingRedirectAttributeXmlElement.Name.Equals("newVersion", StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                version = bindingRedirectAttributeXmlElement.Value;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(version))
                                            {
                                                string path = Path.Combine(AssemblyPath, name + ".dll");
                                                string assemblyVersion = File.Exists(path) ? Assembly.LoadFile(path).GetName().Version.ToString() : version;
                                                if (!version.Equals(assemblyVersion))
                                                {
                                                    if (!(String.Equals(name, "System.ValueTuple", StringComparison.OrdinalIgnoreCase) && String.Equals(version, "4.0.0.0") && String.Equals(assemblyVersion, "4.0.3.0")))
                                                    {
                                                        Log.LogError($"Binding redirect for '{name}' redirects to a different version ({version}) than MSBuild ships ({assemblyVersion}).");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return !Log.HasLoggedErrors;
        }
    }
}
