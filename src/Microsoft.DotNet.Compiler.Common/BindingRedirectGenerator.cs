// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.DotNet.ProjectModel.Compilation;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public static class BindingRedirectGenerator
    {
        private const int TokenLength = 8;
        private const string Namespace = "urn:schemas-microsoft-com:asm.v1";

        private static readonly XName ConfigurationElementName = XName.Get("configuration");
        private static readonly XName RuntimeElementName = XName.Get("runtime");
        private static readonly XName AssemblyBindingElementName = XName.Get("assemblyBinding", Namespace);
        private static readonly XName DependentAssemblyElementName = XName.Get("dependentAssembly", Namespace);
        private static readonly XName AssemblyIdentityElementName = XName.Get("assemblyIdentity", Namespace);
        private static readonly XName BindingRedirectElementName = XName.Get("bindingRedirect", Namespace);

        private static readonly XName NameAttributeName = XName.Get("name");
        private static readonly XName PublicKeyTokenAttributeName = XName.Get("publicKeyToken");
        private static readonly XName CultureAttributeName = XName.Get("culture");
        private static readonly XName OldVersionAttributeName = XName.Get("oldVersion");
        private static readonly XName NewVersionAttributeName = XName.Get("newVersion");

        private static SHA1 Sha1 { get; } = SHA1.Create();

        public static void GenerateBindingRedirects(this IEnumerable<LibraryExport> dependencies, IEnumerable<string> configFiles)
        {
            var redirects = CollectRedirects(dependencies);

            if (!redirects.Any())
            {
                // No redirects required
                return;
            }

            foreach (var configFile in configFiles)
            {
                GenerateBindingRedirects(configFile, redirects);
            }
        }

        internal static void GenerateBindingRedirects(string configFile, AssemblyRedirect[] bindingRedirects)
        {
            XDocument configRoot = File.Exists(configFile) ? XDocument.Load(configFile) : new XDocument();
            var configuration = GetOrAddElement(configRoot, ConfigurationElementName);
            var runtime = GetOrAddElement(configuration, RuntimeElementName);
            var assemblyBindings = GetOrAddElement(runtime, AssemblyBindingElementName);

            foreach (var redirect in bindingRedirects)
            {
                AddDependentAssembly(redirect, assemblyBindings);
            }

            using (var fileStream = File.Open(configFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                configRoot.Save(fileStream);
            }
        }

        private static void AddDependentAssembly(AssemblyRedirect redirect, XElement assemblyBindings)
        {
            var dependencyElement = assemblyBindings.Elements(DependentAssemblyElementName)
                .FirstOrDefault(element => IsSameAssembly(redirect, element));

            if (dependencyElement == null)
            {
                dependencyElement = new XElement(DependentAssemblyElementName,
                    new XElement(AssemblyIdentityElementName,
                                new XAttribute(NameAttributeName, redirect.From.Name),
                                new XAttribute(PublicKeyTokenAttributeName, redirect.From.PublicKeyToken),
                                new XAttribute(CultureAttributeName, redirect.From.Culture)
                            )
                        );
                assemblyBindings.Add(dependencyElement);
            }

            bool redirectExists = dependencyElement.Elements(BindingRedirectElementName).Any(element => IsSameRedirect(redirect, element));

            if (!redirectExists)
            {
                dependencyElement.Add(new XElement(BindingRedirectElementName,
                        new XAttribute(OldVersionAttributeName, redirect.From.Version),
                        new XAttribute(NewVersionAttributeName, redirect.To.Version)
                        ));
            }
        }

        private static bool IsSameAssembly(AssemblyRedirect redirect, XElement dependentAssemblyElement)
        {
            var identity = dependentAssemblyElement.Element(AssemblyIdentityElementName);
            if (identity == null)
            {
                return false;
            }
            return (string)identity.Attribute(NameAttributeName) == redirect.From.Name &&
                   (string)identity.Attribute(PublicKeyTokenAttributeName) == redirect.From.PublicKeyToken &&
                   (string)identity.Attribute(CultureAttributeName) == redirect.From.Culture;
        }

        private static bool IsSameRedirect(AssemblyRedirect redirect, XElement bindingRedirectElement)
        {
            if (bindingRedirectElement == null)
            {
                return false;
            }
            return (string)bindingRedirectElement.Attribute(OldVersionAttributeName) == redirect.From.Version.ToString() &&
                   (string)bindingRedirectElement.Attribute(NewVersionAttributeName) == redirect.To.Version.ToString();
        }

        private static XElement GetOrAddElement(XContainer parent, XName elementName)
        {
            XElement element;
            if (parent.Element(elementName) != null)
            {
                element = parent.Element(elementName);
            }
            else
            {
                element = new XElement(elementName);
                parent.Add(element);
            }
            return element;
        }

        internal static AssemblyRedirect[] CollectRedirects(IEnumerable<LibraryExport> dependencies)
        {
            var runtimeAssemblies = dependencies
                .SelectMany(d => d.RuntimeAssemblyGroups.GetDefaultAssets())
                .Select(GetAssemblyInfo);

            return CollectRedirects(runtimeAssemblies);
        }

        internal static AssemblyRedirect[] CollectRedirects(IEnumerable<AssemblyReferenceInfo> runtimeAssemblies)
        {
            var assemblyLookup = runtimeAssemblies.ToLookup(r => r.Identity.ToLookupKey());

            var redirectAssemblies = new HashSet<AssemblyRedirect>();
            foreach (var assemblyReferenceInfo in assemblyLookup)
            {
                // Using .First here is not exactly valid, but we don't know which one gets copied to
                // output so we just use first
                var references = assemblyReferenceInfo.First().References;
                foreach (var referenceIdentity in references)
                {
                    var targetAssemblies = assemblyLookup[referenceIdentity.ToLookupKey()];
                    if (!targetAssemblies.Any())
                    {
                        continue;
                    }
                    var targetAssemblyIdentity = targetAssemblies.First();
                    if (targetAssemblyIdentity.Identity.Version != referenceIdentity.Version)
                    {
                        if (targetAssemblyIdentity.Identity.PublicKeyToken != null)
                        {
                            redirectAssemblies.Add(new AssemblyRedirect()
                            {
                                From = referenceIdentity,
                                To = targetAssemblyIdentity.Identity
                            });
                        }
                    }
                }
            }

            return redirectAssemblies.ToArray();
        }

        private static AssemblyReferenceInfo GetAssemblyInfo(LibraryAsset arg)
        {
            using (var peReader = new PEReader(File.OpenRead(arg.ResolvedPath)))
            {
                var metadataReader = peReader.GetMetadataReader();

                var definition = metadataReader.GetAssemblyDefinition();

                var identity = new AssemblyIdentity(
                    metadataReader.GetString(definition.Name),
                    definition.Version,
                    metadataReader.GetString(definition.Culture),
                    GetPublicKeyToken(metadataReader.GetBlobBytes(definition.PublicKey))
                );

                var references = new List<AssemblyIdentity>(metadataReader.AssemblyReferences.Count);

                foreach (var assemblyReferenceHandle in metadataReader.AssemblyReferences)
                {
                    var assemblyReference = metadataReader.GetAssemblyReference(assemblyReferenceHandle);
                    references.Add(new AssemblyIdentity(
                        metadataReader.GetString(assemblyReference.Name),
                        assemblyReference.Version,
                        metadataReader.GetString(assemblyReference.Culture),
                        GetPublicKeyToken(metadataReader.GetBlobBytes(assemblyReference.PublicKeyOrToken))
                    ));
                }

                return new AssemblyReferenceInfo(identity, references.ToArray());
            }
        }

        private static string GetPublicKeyToken(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return null;
            }

            byte[] token;
            if (bytes.Length == TokenLength)
            {
                token = bytes;
            }
            else
            {
                token = new byte[TokenLength];
                var sha1 = Sha1.ComputeHash(bytes);
                Array.Copy(sha1, sha1.Length - TokenLength, token, 0, TokenLength);
                Array.Reverse(token);
            }

            var hex = new StringBuilder(TokenLength * 2);
            foreach (var b in token)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }

        internal struct AssemblyRedirect
        {
            public AssemblyRedirect(AssemblyIdentity from, AssemblyIdentity to)
            {
                From = from;
                To = to;
            }

            public AssemblyIdentity From { get; set; }

            public AssemblyIdentity To { get; set; }
        }

        internal struct AssemblyIdentity
        {
            public AssemblyIdentity(string name, Version version, string culture, string publicKeyToken)
            {
                Name = name;
                Version = version;
                Culture = string.IsNullOrEmpty(culture) ? "neutral" : culture;
                PublicKeyToken = publicKeyToken;
            }

            public string Name { get; }

            public Version Version { get; }

            public string Culture { get; }

            public string PublicKeyToken { get; }

            public Tuple<string, string, string> ToLookupKey() => Tuple.Create(Name, Culture, PublicKeyToken);

            public override string ToString()
            {
                return $"{Name} {Version} {Culture} {PublicKeyToken}";
            }
        }

        internal struct AssemblyReferenceInfo
        {
            public AssemblyReferenceInfo(AssemblyIdentity identity, AssemblyIdentity[] references)
            {
                Identity = identity;
                References = references;
            }

            public AssemblyIdentity Identity { get; }

            public AssemblyIdentity[] References { get; }

            public override string ToString()
            {
                return $"{Identity} Reference count: {References.Length}";
            }
        }
    }
}