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
                if (topLevelElement is XmlElement topLevelXmlElement && topLevelXmlElement.Name.Equals("configuration", System.StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var configurationElement in topLevelXmlElement.ChildNodes)
                    {
                        if (configurationElement is XmlElement configurationXmlElement && configurationXmlElement.Name.Equals("runtime", System.StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var runtimeElement in configurationXmlElement.ChildNodes)
                            {
                                if (runtimeElement is XmlElement runtimeXmlElement && runtimeXmlElement.Name.Equals("assemblyBinding", System.StringComparison.OrdinalIgnoreCase))
                                {
                                    foreach (var assemblyBindingElement in runtimeXmlElement.ChildNodes)
                                    {
                                        if (assemblyBindingElement is XmlElement assemblyBindingXmlElement && assemblyBindingXmlElement.Name.Equals("dependentAssembly", System.StringComparison.OrdinalIgnoreCase))
                                        {
                                            string name = string.Empty;
                                            string version = string.Empty;
                                            bool check = true;
                                            foreach (var dependentAssembly in assemblyBindingXmlElement.ChildNodes)
                                            {
                                                if (dependentAssembly is XmlElement dependentAssemblyXmlElement)
                                                {
                                                    if (dependentAssemblyXmlElement.Name.Equals("assemblyIdentity", System.StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        foreach (var assemblyIdentityAttribute in dependentAssemblyXmlElement.Attributes)
                                                        {
                                                            if (assemblyIdentityAttribute is XmlAttribute assemblyIdentityAttributeXmlElement && assemblyIdentityAttributeXmlElement.Name.Equals("name", System.StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                name = assemblyIdentityAttributeXmlElement.Value;
                                                            }
                                                        }
                                                    }
                                                    else if (dependentAssemblyXmlElement.Name.Equals("bindingRedirect", System.StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        foreach (var bindingRedirectAttribute in dependentAssemblyXmlElement.Attributes)
                                                        {
                                                            if (bindingRedirectAttribute is XmlAttribute bindingRedirectVersion && bindingRedirectVersion.Name.Equals("newVersion", System.StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                version = bindingRedirectVersion.Value;
                                                            }
                                                            else if (bindingRedirectAttribute is XmlAttribute notToCheck && notToCheck.Name.Equals("notToBeChecked", System.StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                check = !notToCheck.Value.Equals("true", System.StringComparison.OrdinalIgnoreCase);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            if (check && !string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(version))
                                            {
                                                string path = Path.Combine(AssemblyPath, name + ".dll");
                                                if (File.Exists(path) && !version.Equals(Assembly.LoadFile(path).GetName().Version.ToString()))
                                                {
                                                    Log.LogError($"Binding redirect for '{name} redirects to a different version ({version}) than MSBuild ships.");
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
