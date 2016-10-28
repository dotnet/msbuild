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
        private static IReadOnlyDictionary<string, IReadOnlyList<string>> Suppresses { get; } = new Dictionary<string, IReadOnlyList<string>>
        {
            { "csc", new string[] {"CS1701", "CS1702", "CS1705" } }
        };

        private static IReadOnlyList<string> GenerateAssemblyInfoWhitelist = new List<string>()
        {
            "AssemblyCompany",
            "AssemblyConfiguration",
            "AssemblyCopyright",
            "AssemblyDescription",
            "AssemblyFileVersion",
            "AssemblyInformationalVersion",
            "AssemblyProduct",
            "AssemblyTitle",
            "AssemblyVersion",
            "NeutralResourcesLanguage"
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
            var assemblyInfoList = GetAssemblyInfo(sources);

            foreach(var assemblyInfo in assemblyInfoList)
            {
                var propertyTransform = new AddPropertyTransform<string>(
                    $"Generate{assemblyInfo}Attribute",
                    a => "false",
                    a => true);

                _transformApplicator.Execute(
                    propertyTransform.Transform(assemblyInfo),
                    migrationRuleInputs.CommonPropertyGroup,
                    true);
            }
        }

        public IEnumerable<string> GetCompilationSources(ProjectContext project, CommonCompilerOptions compilerOptions)
        {
            if (compilerOptions.CompileInclude == null)
            {
                return project.ProjectFile.Files.SourceFiles;
            }

            var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilerOptions.CompileInclude, "/", diagnostics: null);

            return includeFiles.Select(f => f.SourcePath);
        }

        public List<string> GetAssemblyInfo(IEnumerable<string> sourceFiles)
        {
            var assemblyInfoList = new List<string>();
            foreach (var sourceFile in sourceFiles)
            {
                var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
                var root = tree.GetRoot();

                // assembly attributes can be only on first level
                foreach (var attributeListSyntax in root.ChildNodes().OfType<AttributeListSyntax>())
                {
                    if (attributeListSyntax.Target.Identifier.Kind() == SyntaxKind.AssemblyKeyword)
                    {
                        foreach (var attributeSyntax in attributeListSyntax
                            .Attributes
                            .Select(a => a.Name.ToString())
                            .Where(a => GenerateAssemblyInfoWhitelist.Contains(a)))
                        {
                            assemblyInfoList.Add(attributeSyntax);
                        }
                    }
                }
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
    }
}