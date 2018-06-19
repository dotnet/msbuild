// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Reflection;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Take suggested redirects (from the ResolveAssemblyReference and GenerateOutOfBandAssemblyTables tasks)
    /// and add them to an intermediate copy of the App.config file.
    /// </summary>
    public class GenerateBindingRedirects : TaskExtension
    {
        // <param name="SuggestedRedirects">RAR suggested binding redirects.</param>
        // <param name="AppConfigFile">The source App.Config file.</param>
        // <param name="TargetName">The name of the target app config file: XXX.exe.config.</param>
        // <param name="OutputAppConfigFile">The output App.Config file.</param>
        // <returns>True if there was success.</returns>

        /// <summary>
        /// Sugested redirects as output from the ResolveAssemblyReference task.
        /// </summary>
        public ITaskItem[] SuggestedRedirects { get; set; }

        /// <summary>
        /// Path to the app.config source file.
        /// </summary>
        public ITaskItem AppConfigFile { get; set; }

        /// <summary>
        /// Name of the output application config file: $(TargetFileName).config
        /// </summary>
        public string TargetName { get; set; }

        /// <summary>
        /// Path to an intermediate file where we can write the input app.config plus the generated binding redirects.
        /// </summary>
        [Output]
        public ITaskItem OutputAppConfigFile { get; set; }

        /// <summary>
        /// Execute the task.
        /// </summary>
        public override bool Execute()
        {
            if (SuggestedRedirects == null || SuggestedRedirects.Length == 0)
            {
                Log.LogMessageFromResources("GenerateBindingRedirects.NoSuggestedRedirects");
                OutputAppConfigFile = null;
                return true;
            }

            var redirects = ParseSuggestedRedirects();

            var doc = LoadAppConfig(AppConfigFile);

            if (doc == null)
            {
                return false;
            }

            XElement runtimeNode = doc.Root
                                      .Nodes()
                                      .OfType<XElement>()
                                      .FirstOrDefault(e => e.Name.LocalName == "runtime");

            if (runtimeNode == null)
            {
                runtimeNode = new XElement("runtime");
                doc.Root.Add(runtimeNode);
            }
            else
            {
                UpdateExistingBindingRedirects(runtimeNode, redirects);
            }

            var ns = XNamespace.Get("urn:schemas-microsoft-com:asm.v1");

            var redirectNodes = from redirect in redirects
                                select new XElement(
                                    ns + "assemblyBinding",
                                    new XElement(
                                        ns + "dependentAssembly",
                                        new XElement(
                                            ns + "assemblyIdentity",
                                            new XAttribute("name", redirect.Key.Name),
                                            new XAttribute("publicKeyToken", ResolveAssemblyReference.ByteArrayToString(redirect.Key.GetPublicKeyToken())),
                                            new XAttribute("culture", String.IsNullOrEmpty(redirect.Key.CultureName) ? "neutral" : redirect.Key.CultureName)),
                                        new XElement(
                                            ns + "bindingRedirect",
                                            new XAttribute("oldVersion", "0.0.0.0-" + redirect.Value),
                                            new XAttribute("newVersion", redirect.Value))));

            runtimeNode.Add(redirectNodes);

            var writeOutput = true;

            if(File.Exists(OutputAppConfigFile.ItemSpec))
            {
                try
                {
                    var outputDoc = LoadAppConfig(OutputAppConfigFile);
                    if(outputDoc.ToString() == doc.ToString())
                    {
                        writeOutput = false;
                    }

                }
                catch(System.Xml.XmlException)
                {
                    writeOutput = true;
                }
            }


            if (AppConfigFile != null)
            {
                AppConfigFile.CopyMetadataTo(OutputAppConfigFile);
            }
            else
            {
                OutputAppConfigFile.SetMetadata(ItemMetadataNames.targetPath, TargetName);
            }

            if(writeOutput)
            {
                using (var stream = FileUtilities.OpenWrite(OutputAppConfigFile.ItemSpec, false))
                {
                    doc.Save(stream);
                }
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Determins whether the name, culture, and public key token of the given assembly name "suggestedRedirect"
        /// matches the name, culture, and publicKeyToken strings.
        /// </summary>
        private static bool IsMatch(AssemblyName suggestedRedirect, string name, string culture, string publicKeyToken)
        {
            if (String.Compare(suggestedRedirect.Name, name, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            if (ByteArrayMatchesString(suggestedRedirect.GetPublicKeyToken(), publicKeyToken))
            {
                return false;
            }

            // The binding redirect will be applied if the culture is missing from the "assemblyIdentity" node.
            // So we consider it a match if the existing binding redirect doesn't have culture specified.
            var cultureString = suggestedRedirect.CultureName;
            if (String.IsNullOrEmpty(cultureString))
            {
                // We use "neutral" for "Invariant Language (Invariant Country)" in assembly names.
                cultureString = "neutral";
            }

            if (!String.IsNullOrEmpty(culture) &&
                String.Compare(cultureString, culture, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether string "s" is the hexdecimal representation of the byte array "a".
        /// </summary>
        private static bool ByteArrayMatchesString(Byte[] a, string s)
        {
            return String.Compare(ResolveAssemblyReference.ByteArrayToString(a), s, StringComparison.OrdinalIgnoreCase) != 0;
        }

        /// <summary>
        /// Going through all the binding redirects in the runtime node, if anyone overlaps with a RAR suggested redirect,
        /// we update the existing redirect and output warning.
        /// </summary>
        private void UpdateExistingBindingRedirects(XElement runtimeNode, IDictionary<AssemblyName, string> redirects)
        {
            ErrorUtilities.VerifyThrow(runtimeNode != null, "This should not be called if the \"runtime\" node is missing.");

            var assemblyBindingNodes = runtimeNode.Nodes()
                .OfType<XElement>()
                .Where(e => e.Name.LocalName == "assemblyBinding");

            foreach (var assemblyBinding in assemblyBindingNodes)
            {
                // Each assemblyBinding section could have more than one dependentAssembly elements
                var dependentAssemblies = assemblyBinding.Nodes()
                    .OfType<XElement>()
                    .Where(e => e.Name.LocalName == "dependentAssembly");

                foreach (var dependentAssembly in dependentAssemblies)
                {
                    var assemblyIdentity = dependentAssembly
                        .Nodes()
                        .OfType<XElement>()
                        .FirstOrDefault(e => e.Name.LocalName == "assemblyIdentity");

                    if (assemblyIdentity == null)
                    {
                        // Due to MSDN documentation (https://msdn.microsoft.com/en-us/library/0ash1ksb(v=vs.110).aspx)
                        // assemblyIdentity is required subelement. Emitting a warning if it's not there.
                        Log.LogWarningWithCodeFromResources("GenerateBindingRedirects.MissingNode", "dependentAssembly", "assemblyBinding");
                        continue;
                    }

                    var bindingRedirect = dependentAssembly
                        .Nodes()
                        .OfType<XElement>()
                        .FirstOrDefault(e => e.Name.LocalName == "bindingRedirect");

                    if (bindingRedirect == null)
                    {
                        // Due to xsd schema and MSDN documentation bindingRedirect is not required subelement.
                        // Just skipping it without a warning.
                        continue;
                    }

                    var name = assemblyIdentity.Attribute("name");
                    var publicKeyToken = assemblyIdentity.Attribute("publicKeyToken");

                    if (name == null || publicKeyToken == null)
                    {
                        continue;
                    }

                    var nameValue = name.Value;
                    var publicKeyTokenValue = publicKeyToken.Value;
                    var culture = assemblyIdentity.Attribute("culture");
                    var cultureValue = culture?.Value ?? String.Empty;

                    var oldVersionAttribute = bindingRedirect.Attribute("oldVersion");
                    var newVersionAttribute = bindingRedirect.Attribute("newVersion");

                    if (oldVersionAttribute == null || newVersionAttribute == null)
                    {
                        continue;
                    }

                    var oldVersionRange = oldVersionAttribute.Value.Split('-');
                    if (oldVersionRange.Length == 0 || oldVersionRange.Length > 2)
                    {
                        continue;
                    }

                    var oldVerStrLow = oldVersionRange[0];
                    var oldVerStrHigh = oldVersionRange[oldVersionRange.Length == 1 ? 0 : 1];

                    if (!Version.TryParse(oldVerStrLow, out Version oldVersionLow))
                    {
                        Log.LogWarningWithCodeFromResources("GenerateBindingRedirects.MalformedVersionNumber", oldVerStrLow);
                        continue;
                    }

                    if (!Version.TryParse(oldVerStrHigh, out Version oldVersionHigh))
                    {
                        Log.LogWarningWithCodeFromResources("GenerateBindingRedirects.MalformedVersionNumber", oldVerStrHigh);
                        continue;
                    }

                    // We cannot do a simply dictionary lookup here because we want to allow relaxed "culture" matching:
                    // we consider it a match if the existing binding redirect doesn't specify culture in the assembly identity.
                    foreach (var entry in redirects)
                    {
                        if (IsMatch(entry.Key, nameValue, cultureValue, publicKeyTokenValue))
                        {
                            string maxVerStr = entry.Value;
                            var maxVersion = new Version(maxVerStr);

                            if (maxVersion >= oldVersionLow)
                            {
                                // Update the existing binding redirect to the RAR suggested one.
                                var newName = entry.Key.Name;
                                var newCulture = entry.Key.CultureName;
                                var newPublicKeyToken = entry.Key.GetPublicKeyToken();
                                var newProcessorArchitecture = entry.Key.ProcessorArchitecture;

                                var attributes = new List<XAttribute>(4)
                                {
                                    new XAttribute("name", newName),
                                    new XAttribute(
                                        "culture",
                                        String.IsNullOrEmpty(newCulture) ? "neutral" : newCulture),
                                    new XAttribute(
                                        "publicKeyToken",
                                        ResolveAssemblyReference.ByteArrayToString(newPublicKeyToken))
                                };
                                if (newProcessorArchitecture != 0)
                                {
                                    attributes.Add(new XAttribute("processorArchitecture", newProcessorArchitecture.ToString()));
                                }

                                assemblyIdentity.ReplaceAttributes(attributes);

                                oldVersionAttribute.Value = "0.0.0.0-" + (maxVersion >= oldVersionHigh ? maxVerStr : oldVerStrHigh);
                                newVersionAttribute.Value = maxVerStr;
                                redirects.Remove(entry.Key);

                                Log.LogWarningWithCodeFromResources("GenerateBindingRedirects.OverlappingBindingRedirect", entry.Key.ToString(), bindingRedirect.ToString());
                            }

                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Load or create App.Config
        /// </summary>
        private XDocument LoadAppConfig(ITaskItem appConfigItem)
        {
            XDocument document;
            if (appConfigItem == null)
            {
                document = new XDocument(
                    new XDeclaration("1.0", "utf-8", "true"),
                    new XElement("configuration"));
            }
            else
            {
                document = XDocument.Load(appConfigItem.ItemSpec);
                if (document.Root == null || document.Root.Name != "configuration")
                {
                    Log.LogErrorWithCodeFromResources("GenerateBindingRedirects.MissingConfigurationNode");
                    return null;
                }
            }

            return document;
        }

        /// <summary>
        /// Parse the suggested redirects from RAR and return a dictionary containing all those suggested redirects
        /// in the form of AssemblyName-MaxVersion pairs.
        /// </summary>
        private IDictionary<AssemblyName, string> ParseSuggestedRedirects()
        {
            ErrorUtilities.VerifyThrow(SuggestedRedirects != null && SuggestedRedirects.Length > 0, "This should not be called if there is no suggested redirect.");

            var map = new Dictionary<AssemblyName, string>();
            foreach (var redirect in SuggestedRedirects)
            {
                var redirectStr = redirect.ItemSpec;

                try
                {
                    var maxVerStr = redirect.GetMetadata("MaxVersion");
                    Log.LogMessageFromResources(MessageImportance.Low, "GenerateBindingRedirects.ProcessingSuggestedRedirect", redirectStr, maxVerStr);

                    var assemblyIdentity = new AssemblyName(redirectStr);

                    map.Add(assemblyIdentity, maxVerStr);
                }
                catch (Exception)
                {
                    Log.LogWarningWithCodeFromResources("GenerateBindingRedirects.MalformedAssemblyName", redirectStr);
                    continue;
                }
            }

            return map;
        }
    }
}
