// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public class AssemblyInfoFileGenerator
    {
        public static string Generate(AssemblyInfoOptions metadata, IEnumerable<string> sourceFiles)
        {
            var projectAttributes = GetProjectAttributes(metadata);

            var existingAttributes = new List<Type>();
            foreach (var sourceFile in sourceFiles)
            {
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                var root = tree.GetRoot();

                // assembly attributes can be only on first level
                foreach (var attributeListSyntax in root.ChildNodes().OfType<AttributeListSyntax>())
                {
                    if (attributeListSyntax.Target.Identifier.Kind() == SyntaxKind.AssemblyKeyword)
                    {
                        foreach (var attributeSyntax in attributeListSyntax.Attributes)
                        {
                            var projectAttribute = projectAttributes.FirstOrDefault(attribute => IsSameAttribute(attribute.Key, attributeSyntax));
                            if (projectAttribute.Key != null)
                            {
                                existingAttributes.Add(projectAttribute.Key);
                            }
                        }
                    }
                }
            }

            return string.Join(Environment.NewLine, projectAttributes
                .Where(projectAttribute => projectAttribute.Value != null && !existingAttributes.Contains(projectAttribute.Key))
                .Select(projectAttribute => $"[assembly:{projectAttribute.Key.FullName}(\"{projectAttribute.Value}\")]"));
        }

        public static string GenerateFSharp(AssemblyInfoOptions metadata)
        {
            var projectAttributes = GetProjectAttributes(metadata);

            return string.Join(Environment.NewLine,
                new[] { "namespace System", Environment.NewLine, Environment.NewLine } 
                .Concat(projectAttributes.Select(projectAttribute => $"[<assembly:{projectAttribute.Key.FullName}(\"{projectAttribute.Value}\")>]"))
                .Concat(new[] { "do ()", Environment.NewLine }));
        }

        private static Dictionary<Type, string> GetProjectAttributes(AssemblyInfoOptions metadata)
        {
            return new Dictionary<Type, string>()
            {
                [typeof(AssemblyTitleAttribute)] = EscapeCharacters(metadata.Title),
                [typeof(AssemblyDescriptionAttribute)] = EscapeCharacters(metadata.Description),
                [typeof(AssemblyCopyrightAttribute)] = EscapeCharacters(metadata.Copyright),
                [typeof(AssemblyFileVersionAttribute)] = EscapeCharacters(metadata.AssemblyFileVersion?.ToString()),
                [typeof(AssemblyVersionAttribute)] = EscapeCharacters(metadata.AssemblyVersion?.ToString()),
                [typeof(AssemblyInformationalVersionAttribute)] = EscapeCharacters(metadata.InformationalVersion),
                [typeof(AssemblyCultureAttribute)] = EscapeCharacters(metadata.Culture),
                [typeof(NeutralResourcesLanguageAttribute)] = EscapeCharacters(metadata.NeutralLanguage),
                [typeof(TargetFrameworkAttribute)] = EscapeCharacters(metadata.TargetFramework)
            };
        }

        private static bool IsSameAttribute(Type attributeType, AttributeSyntax attributeSyntax)
        {
            var name = attributeSyntax.Name.ToString();
            // This check is quite stupid but we can not do more without semantic model
            return attributeType.FullName.StartsWith(name) || attributeType.Name.StartsWith(name);
        }

        private static string EscapeCharacters(string str)
        {
            return str != null ? SymbolDisplay.FormatLiteral(str, quote: false) : null;
        }
    }
}