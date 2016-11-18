// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;
using Microsoft.DotNet.Internal.ProjectModel;
using Microsoft.DotNet.Internal.ProjectModel.Files;
using Microsoft.DotNet.Tools.Common;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class MigrateAssemblyInfoRule : IMigrationRule
    {
        private const string SystemReflectionNamespace = "System.Reflection";
        private const string SystemResourcesNamespace = "System.Resources";

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> Suppresses { get; } = new Dictionary<string, IReadOnlyList<string>>
        {
            { "csc", new string[] {"CS1701", "CS1702", "CS1705" } }
        };

        private static IReadOnlyList<KnownAssemblyAttribute> GenerateAssemblyInfoWhitelist = new List<KnownAssemblyAttribute>()
        {
            new KnownAssemblyAttribute(SystemReflectionNamespace, "AssemblyCompany"),
            new KnownAssemblyAttribute(SystemReflectionNamespace, "AssemblyConfiguration"),
            new KnownAssemblyAttribute(SystemReflectionNamespace, "AssemblyCopyright"),
            new KnownAssemblyAttribute(SystemReflectionNamespace, "AssemblyDescription"),
            new KnownAssemblyAttribute(SystemReflectionNamespace, "AssemblyFileVersion"),
            new KnownAssemblyAttribute(SystemReflectionNamespace, "AssemblyInformationalVersion"),
            new KnownAssemblyAttribute(SystemReflectionNamespace, "AssemblyProduct"),
            new KnownAssemblyAttribute(SystemReflectionNamespace, "AssemblyTitle"),
            new KnownAssemblyAttribute(SystemReflectionNamespace, "AssemblyVersion"),
            new KnownAssemblyAttribute(SystemResourcesNamespace, "NeutralResourcesLanguage")
        };

        private readonly ITransformApplicator _transformApplicator;

        public MigrateAssemblyInfoRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var projectContext = migrationRuleInputs.DefaultProjectContext;
            var compilationOptions = ResolveCompilationOptions(projectContext, "Debug");
            var sources = GetCompilationSources(projectContext, compilationOptions);
            var assemblyAttributeList = GetWhitelistedKnownAssemblyAttributes(sources);

            foreach(var assemblyAttribute in assemblyAttributeList)
            {
                var propertyTransform = new AddPropertyTransform<string>(
                    assemblyAttribute.GenerateAssemblyAttributePropertyName,
                    a => "false",
                    a => true);

                _transformApplicator.Execute(
                    propertyTransform.Transform(assemblyAttribute.AttributeName),
                    migrationRuleInputs.CommonPropertyGroup,
                    true);
            }
        }

        private IEnumerable<string> GetCompilationSources(ProjectContext project, CommonCompilerOptions compilerOptions)
        {
            if (compilerOptions.CompileInclude == null)
            {
                return project.ProjectFile.Files.SourceFiles;
            }

            var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilerOptions.CompileInclude, "/", diagnostics: null);

            return includeFiles.Select(f => f.SourcePath);
        }

        private List<KnownAssemblyAttribute> GetWhitelistedKnownAssemblyAttributes(IEnumerable<string> sourceFiles)
        {
            var assemblyInfoList = new List<KnownAssemblyAttribute>();
            foreach (var sourceFile in sourceFiles)
            {
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                var root = tree.GetRoot();

                // assembly attributes can be only on first level
                var attributeListSyntaxNodes = root.ChildNodes().OfType<AttributeListSyntax>();

                assemblyInfoList.AddRange(
                    attributeListSyntaxNodes
                    .Where(node => node.Target.Identifier.Kind() == SyntaxKind.AssemblyKeyword)
                    .SelectMany(node => node.Attributes)
                    .Select(attribute => attribute.Name.ToString())
                    .Select(name =>
                        GenerateAssemblyInfoWhitelist
                        .FirstOrDefault(b => b.MatchNames.Contains(name))
                    )
                    .Where(knownAttribute => knownAttribute != null)
                );
            }

            return assemblyInfoList;
        }

        // used in incremental compilation for the key file
        private CommonCompilerOptions ResolveCompilationOptions(ProjectContext context, string configuration)
        {
            var compilerOptions = GetLanguageSpecificCompilerOptions(context, context.TargetFramework, configuration);

            // Path to strong naming key in environment variable overrides path in project.json
            var environmentKeyFile = Environment.GetEnvironmentVariable(EnvironmentNames.StrongNameKeyFile);

            if (!string.IsNullOrWhiteSpace(environmentKeyFile))
            {
                compilerOptions.KeyFile = environmentKeyFile;
            }
            else if (!string.IsNullOrWhiteSpace(compilerOptions.KeyFile))
            {
                // Resolve full path to key file
                compilerOptions.KeyFile =
                    Path.GetFullPath(Path.Combine(context.ProjectFile.ProjectDirectory, compilerOptions.KeyFile));
            }
            return compilerOptions;
        }

        private CommonCompilerOptions GetLanguageSpecificCompilerOptions(
            ProjectContext context,
            NuGetFramework framework,
            string configurationName)
        {
            var baseOption = context.ProjectFile.GetCompilerOptions(framework, configurationName);

            IReadOnlyList<string> defaultSuppresses;
            var compilerName = baseOption.CompilerName ?? "csc";
            if (Suppresses.TryGetValue(compilerName, out defaultSuppresses))
            {
                baseOption.SuppressWarnings = (baseOption.SuppressWarnings ?? Enumerable.Empty<string>()).Concat(defaultSuppresses).Distinct();
            }

            return baseOption;
        }

        private class KnownAssemblyAttribute
        {
            public string Namespace { get; }
            public string AttributeName { get; }
            public IList<string> MatchNames { get; }
            public string GenerateAssemblyAttributePropertyName { get; }

            public KnownAssemblyAttribute(string namespaceName, string attributeName)
            {
                Namespace = namespaceName;
                AttributeName = attributeName;
                GenerateAssemblyAttributePropertyName = $"Generate{attributeName}Attribute";
                MatchNames = new [] {
                    attributeName,
                    $"{attributeName}Attribute",
                    $"{namespaceName}.{attributeName}",
                    $"{namespaceName}.{attributeName}Attribute"
                };
            }
        }
    }
}