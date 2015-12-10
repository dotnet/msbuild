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

namespace Microsoft.DotNet.Tools.Compiler
{
    internal class BindingRedirectGenerator
    {
        private const int TokenLength = 8;
        private const string Namespace = "urn:schemas-microsoft-com:asm.v1";

        private static XName ConfigurationElementName = XName.Get("configuration");
        private static XName RuntimeElementName = XName.Get("runtime");
        private static XName AssemblyBindingElementName = XName.Get("assemblyBinding", Namespace);
        private static XName DependentAssemblyElementName = XName.Get("dependentAssembly", Namespace);
        private static XName AssemblyIdentityElementName = XName.Get("assemblyIdentity", Namespace);
        private static XName BindingRedirectElementName = XName.Get("bindingRedirect", Namespace);

        private static XName NameAttributeName = XName.Get("name");
        private static XName PublicKeyTokenAttributeName = XName.Get("publicKeyToken");
        private static XName CultureAttributeName = XName.Get("culture");
        private static XName OldVersionAttributeName = XName.Get("oldVersion");
        private static XName NewVersionAttributeName = XName.Get("newVersion");

        private readonly SHA1 _sha1 = SHA1.Create();

        public XDocument Generate(IEnumerable<LibraryExport> dependencies)
        {
            var redirects = CollectRedirects(dependencies);
            
            if (!redirects.Any())
            {
                // No redirects required
                return null;
            }

            var document = new XDocument(
                new XElement(ConfigurationElementName,
                    new XElement(RuntimeElementName,
                        new XElement(AssemblyBindingElementName,
                            redirects.Select(GetDependentAssembly)
                            )
                        )
                    )
                );
            return document;
        }

        private XElement GetDependentAssembly(AssemblyRedirect redirect)
        {
            var culture = string.IsNullOrEmpty(redirect.From.Culture) ? "neutral" : redirect.From.Culture;

            return new XElement(DependentAssemblyElementName,
                new XElement(AssemblyIdentityElementName,
                    new XAttribute(NameAttributeName, redirect.From.Name),
                    new XAttribute(PublicKeyTokenAttributeName, redirect.From.PublicKeyToken),
                    new XAttribute(CultureAttributeName, culture)
                    ),
                new XElement(BindingRedirectElementName,
                    new XAttribute(OldVersionAttributeName, redirect.From.Version),
                    new XAttribute(NewVersionAttributeName, redirect.To.Version)
                    )
                );
        }

        private AssemblyRedirect[] CollectRedirects(IEnumerable<LibraryExport> dependencies)
        {
            var allRuntimeAssemblies = dependencies.SelectMany(d => d.RuntimeAssemblies).Select(GetAssemblyInfo).ToArray();
            var assemblyLookup = allRuntimeAssemblies.ToDictionary(r => r.Identity.ToLookupKey());

            var redirectAssemblies = new HashSet<AssemblyRedirect>();
            foreach (var assemblyReferenceInfo in allRuntimeAssemblies)
            {
                foreach (var referenceIdentity in assemblyReferenceInfo.References)
                {
                    AssemblyReferenceInfo targetAssemblyIdentity;
                    if (assemblyLookup.TryGetValue(referenceIdentity.ToLookupKey(), out targetAssemblyIdentity)
                        && targetAssemblyIdentity.Identity.Version != referenceIdentity.Version)
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

        private AssemblyReferenceInfo GetAssemblyInfo(LibraryAsset arg)
        {
            using (var peReader = new PEReader(File.OpenRead(arg.ResolvedPath)))
            {
                var metadataReader = peReader.GetMetadataReader();

                var definition = metadataReader.GetAssemblyDefinition();

                var publicKey = metadataReader.GetBlobBytes(definition.PublicKey);
                var publicKeyToken = GetPublicKeyToken(publicKey);
                
                var identity = new AssemblyIdentity(
                    metadataReader.GetString(definition.Name),
                    definition.Version,
                    metadataReader.GetString(definition.Culture),
                    publicKeyToken
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

        private string GetPublicKeyToken(byte[] bytes)
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
                var sha1 = _sha1.ComputeHash(bytes);
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

        private struct AssemblyRedirect
        {
            public AssemblyRedirect(AssemblyIdentity from, AssemblyIdentity to)
            {
                From = from;
                To = to;
            }

            public AssemblyIdentity From { get; set; }

            public AssemblyIdentity To { get; set; }
        }

        private struct AssemblyIdentity
        {
            public AssemblyIdentity(string name, Version version, string culture, string publicKeyToken)
            {
                Name = name;
                Version = version;
                Culture = culture;
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

        private struct AssemblyReferenceInfo
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