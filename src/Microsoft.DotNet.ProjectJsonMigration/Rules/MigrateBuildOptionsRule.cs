// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.Cli.Utils;
using Newtonsoft.Json.Linq;

using Project = Microsoft.DotNet.ProjectModel.Project;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    // TODO: Should All build options be protected by a configuration condition?
    //       This will prevent the entire merge issue altogether and sidesteps the problem of having a duplicate include with different excludes...
    public class MigrateBuildOptionsRule : IMigrationRule
    {
        private AddPropertyTransform<CommonCompilerOptions>[] EmitEntryPointTransforms
            => new AddPropertyTransform<CommonCompilerOptions>[]
            {
                new AddPropertyTransform<CommonCompilerOptions>("OutputType", "Exe",
                    compilerOptions => compilerOptions.EmitEntryPoint != null && compilerOptions.EmitEntryPoint.Value),
                new AddPropertyTransform<CommonCompilerOptions>("TargetExt", ".dll",
                    compilerOptions => compilerOptions.EmitEntryPoint != null && compilerOptions.EmitEntryPoint.Value),
                new AddPropertyTransform<CommonCompilerOptions>("OutputType", "Library",
                    compilerOptions => compilerOptions.EmitEntryPoint == null || !compilerOptions.EmitEntryPoint.Value)
            };

        private AddPropertyTransform<CommonCompilerOptions>[] KeyFileTransforms
            => new AddPropertyTransform<CommonCompilerOptions>[]
            {
                new AddPropertyTransform<CommonCompilerOptions>("AssemblyOriginatorKeyFile",
                    compilerOptions => compilerOptions.KeyFile,
                    compilerOptions => !string.IsNullOrEmpty(compilerOptions.KeyFile)),
                new AddPropertyTransform<CommonCompilerOptions>("SignAssembly",
                    "true",
                    compilerOptions => !string.IsNullOrEmpty(compilerOptions.KeyFile))
            };

        private AddPropertyTransform<CommonCompilerOptions> DefineTransform => new AddPropertyTransform<CommonCompilerOptions>(
            "DefineConstants", 
            compilerOptions => string.Join(";", compilerOptions.Defines),
            compilerOptions => compilerOptions.Defines != null && compilerOptions.Defines.Any());

        private AddPropertyTransform<CommonCompilerOptions> NoWarnTransform => new AddPropertyTransform<CommonCompilerOptions>(
            "NoWarn",
            compilerOptions => string.Join(";", compilerOptions.SuppressWarnings),
            compilerOptions => compilerOptions.SuppressWarnings != null && compilerOptions.SuppressWarnings.Any());

        private AddPropertyTransform<CommonCompilerOptions> PreserveCompilationContextTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("PreserveCompilationContext",
                compilerOptions => compilerOptions.PreserveCompilationContext.ToString().ToLower(),
                compilerOptions => compilerOptions.PreserveCompilationContext != null && compilerOptions.PreserveCompilationContext.Value);

        private AddPropertyTransform<CommonCompilerOptions> WarningsAsErrorsTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("WarningsAsErrors",
                compilerOptions => compilerOptions.WarningsAsErrors.ToString().ToLower(),
                compilerOptions => compilerOptions.WarningsAsErrors != null && compilerOptions.WarningsAsErrors.Value);

        private AddPropertyTransform<CommonCompilerOptions> AllowUnsafeTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("AllowUnsafeBlocks",
                compilerOptions => compilerOptions.AllowUnsafe.ToString().ToLower(),
                compilerOptions => compilerOptions.AllowUnsafe != null && compilerOptions.AllowUnsafe.Value);

        private AddPropertyTransform<CommonCompilerOptions> OptimizeTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("Optimize",
                compilerOptions => compilerOptions.Optimize.ToString().ToLower(),
                compilerOptions => compilerOptions.Optimize != null && compilerOptions.Optimize.Value);

        private AddPropertyTransform<CommonCompilerOptions> PlatformTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("PlatformTarget",
                compilerOptions => compilerOptions.Platform,
                compilerOptions => !string.IsNullOrEmpty(compilerOptions.Platform));

        private AddPropertyTransform<CommonCompilerOptions> LanguageVersionTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("LangVersion",
                compilerOptions => compilerOptions.LanguageVersion,
                compilerOptions => !string.IsNullOrEmpty(compilerOptions.LanguageVersion));

        private AddPropertyTransform<CommonCompilerOptions> DelaySignTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("DelaySign",
                compilerOptions => compilerOptions.DelaySign.ToString().ToLower(),
                compilerOptions => compilerOptions.DelaySign != null && compilerOptions.DelaySign.Value);

        private AddPropertyTransform<CommonCompilerOptions> PublicSignTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("PublicSign",
                compilerOptions => compilerOptions.PublicSign.ToString().ToLower(),
                compilerOptions => compilerOptions.PublicSign != null && compilerOptions.PublicSign.Value);

        private AddPropertyTransform<CommonCompilerOptions> DebugTypeTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("DebugType",
                compilerOptions => compilerOptions.DebugType,
                compilerOptions => !string.IsNullOrEmpty(compilerOptions.DebugType));

        private AddPropertyTransform<CommonCompilerOptions> XmlDocTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("GenerateDocumentationFile",
                compilerOptions => compilerOptions.GenerateXmlDocumentation.ToString().ToLower(),
                compilerOptions => compilerOptions.GenerateXmlDocumentation != null && compilerOptions.GenerateXmlDocumentation.Value);

        // TODO: https://github.com/dotnet/sdk/issues/67
        private AddPropertyTransform<CommonCompilerOptions> XmlDocTransformFilePath =>
            new AddPropertyTransform<CommonCompilerOptions>("DocumentationFile",
                @"$(OutputPath)\$(AssemblyName).xml",
                compilerOptions => compilerOptions.GenerateXmlDocumentation != null && compilerOptions.GenerateXmlDocumentation.Value);

        private AddPropertyTransform<CommonCompilerOptions> OutputNameTransform =>
            new AddPropertyTransform<CommonCompilerOptions>("AssemblyName",
                compilerOptions => compilerOptions.OutputName,
                compilerOptions => !string.IsNullOrEmpty(compilerOptions.OutputName));

        private IncludeContextTransform CompileFilesTransform =>
            new IncludeContextTransform("Compile", transformMappings: false);

        private IncludeContextTransform EmbedFilesTransform =>
            new IncludeContextTransform("EmbeddedResource", transformMappings: false);

        private IncludeContextTransform CopyToOutputFilesTransform =>
            new IncludeContextTransform("Content", transformMappings: true)
            .WithMetadata("CopyToOutputDirectory", "PreserveNewest");

        private Func<CommonCompilerOptions, string, IEnumerable<ProjectItemElement>> CompileFilesTransformExecute =>
            (compilerOptions, projectDirectory) =>
                    CompileFilesTransform.Transform(GetCompileIncludeContext(compilerOptions, projectDirectory));

        private Func<CommonCompilerOptions, string, IEnumerable<ProjectItemElement>> EmbedFilesTransformExecute =>
            (compilerOptions, projectDirectory) =>
                    EmbedFilesTransform.Transform(GetEmbedIncludeContext(compilerOptions, projectDirectory));

        private Func<CommonCompilerOptions, string, IEnumerable<ProjectItemElement>> CopyToOutputFilesTransformExecute =>
            (compilerOptions, projectDirectory) =>
                    CopyToOutputFilesTransform.Transform(GetCopyToOutputIncludeContext(compilerOptions, projectDirectory));

        private string _configuration;
        private ProjectPropertyGroupElement _configurationPropertyGroup;
        private ProjectItemGroupElement _configurationItemGroup;

        private List<AddPropertyTransform<CommonCompilerOptions>> _propertyTransforms;
        private List<Func<CommonCompilerOptions, string, IEnumerable<ProjectItemElement>>> _includeContextTransformExecutes;

        private ITransformApplicator _transformApplicator;

        public MigrateBuildOptionsRule(ITransformApplicator transformApplicator = null)
        {
            _transformApplicator = transformApplicator ?? new TransformApplicator();
            ConstructTransformLists();
        }

        public MigrateBuildOptionsRule(
            string configuration,
            ProjectPropertyGroupElement configurationPropertyGroup,
            ProjectItemGroupElement configurationItemGroup,
            ITransformApplicator transformApplicator = null)
        {
            _configuration = configuration;
            _configurationPropertyGroup = configurationPropertyGroup;
            _configurationItemGroup = configurationItemGroup;
            _transformApplicator = transformApplicator ?? new TransformApplicator();

            ConstructTransformLists();
        }

        private void ConstructTransformLists()
        {
            _propertyTransforms = new List<AddPropertyTransform<CommonCompilerOptions>>()
            {
                DefineTransform,
                NoWarnTransform,
                WarningsAsErrorsTransform,
                AllowUnsafeTransform,
                OptimizeTransform,
                PlatformTransform,
                LanguageVersionTransform,
                DelaySignTransform,
                PublicSignTransform,
                DebugTypeTransform,
                OutputNameTransform,
                XmlDocTransform,
                XmlDocTransformFilePath,
                PreserveCompilationContextTransform
            };

            _propertyTransforms.AddRange(EmitEntryPointTransforms);
            _propertyTransforms.AddRange(KeyFileTransforms);

            _includeContextTransformExecutes = new List<Func<CommonCompilerOptions, string, IEnumerable<ProjectItemElement>>>()
            {
                CompileFilesTransformExecute,
                EmbedFilesTransformExecute,
                CopyToOutputFilesTransformExecute
            };
        }

        public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
        {
            var csproj = migrationRuleInputs.OutputMSBuildProject;
            var projectContext = migrationRuleInputs.DefaultProjectContext;

            var propertyGroup = _configurationPropertyGroup ?? migrationRuleInputs.CommonPropertyGroup;
            var itemGroup = _configurationItemGroup ?? migrationRuleInputs.CommonItemGroup;

            var compilerOptions = projectContext.ProjectFile.GetCompilerOptions(projectContext.TargetFramework, null);
            var configurationCompilerOptions =
                projectContext.ProjectFile.GetCompilerOptions(projectContext.TargetFramework, _configuration);

            // If we're in a configuration, we need to be careful not to overwrite values from BuildOptions
            // without a configuration
            if (_configuration == null)
            {
                CleanExistingProperties(csproj);

                PerformPropertyAndItemMappings(
                    compilerOptions,
                    propertyGroup,
                    itemGroup,
                    _transformApplicator,
                    migrationSettings.ProjectDirectory);
            }
            else
            {
                PerformConfigurationPropertyAndItemMappings(
                    compilerOptions,
                    configurationCompilerOptions,
                    propertyGroup,
                    itemGroup,
                    _transformApplicator,
                    migrationSettings.ProjectDirectory);
            }
        }

        private void PerformConfigurationPropertyAndItemMappings(
            CommonCompilerOptions compilerOptions,
            CommonCompilerOptions configurationCompilerOptions,
            ProjectPropertyGroupElement propertyGroup,
            ProjectItemGroupElement itemGroup,
            ITransformApplicator transformApplicator,
            string projectDirectory)
        {
            foreach (var transform in _propertyTransforms)
            {
                var nonConfigurationOutput = transform.Transform(compilerOptions);
                var configurationOutput = transform.Transform(configurationCompilerOptions);

                if (!PropertiesAreEqual(nonConfigurationOutput, configurationOutput))
                {
                    transformApplicator.Execute(configurationOutput, propertyGroup);
                }
            }

            foreach (var includeContextTransformExecute in _includeContextTransformExecutes)
            {
                var nonConfigurationOutput = includeContextTransformExecute(compilerOptions, projectDirectory);
                var configurationOutput = includeContextTransformExecute(configurationCompilerOptions, projectDirectory).ToArray();

                if (configurationOutput != null && nonConfigurationOutput != null)
                {
                    // TODO: HACK: this is leaky, see top comments, the throw at least covers the scenario
                    ThrowIfConfigurationHasAdditionalExcludes(configurationOutput, nonConfigurationOutput);
                    RemoveCommonIncludes(configurationOutput, nonConfigurationOutput);
                    configurationOutput = configurationOutput.Where(i => i != null && !string.IsNullOrEmpty(i.Include)).ToArray();
                }

                // Don't merge with existing items when doing a configuration
                transformApplicator.Execute(configurationOutput, itemGroup, mergeExisting: false);
            }
        }

        private void ThrowIfConfigurationHasAdditionalExcludes(IEnumerable<ProjectItemElement> configurationOutput, IEnumerable<ProjectItemElement> nonConfigurationOutput)
        {
            foreach (var item1 in configurationOutput)
            {
                if (item1 == null)
                {
                    continue;
                }

                var item2Excludes = new HashSet<string>();
                foreach (var item2 in nonConfigurationOutput)
                {
                    if (item2 != null)
                    {
                        item2Excludes.UnionWith(item2.Excludes());
                    }
                }
                var configurationHasAdditionalExclude =
                    item1.Excludes().Any(exclude => item2Excludes.All(item2Exclude => item2Exclude != exclude));

                if (configurationHasAdditionalExclude)
                {
                    Console.WriteLine("EXCLUDE");
                    Console.WriteLine(item1.Exclude);
                    Console.WriteLine(item2Excludes.ToString());
                    throw new Exception("Unable to migrate projects with excluded files in configurations.");
                }
            }
        }

        private void RemoveCommonIncludes(IEnumerable<ProjectItemElement> itemsToRemoveFrom,
            IEnumerable<ProjectItemElement> otherItems)
        {
            foreach (var item1 in itemsToRemoveFrom)
            {
                if (item1 == null)
                {
                    continue;
                }
                foreach (
                    var item2 in
                    otherItems.Where(
                        i => i != null && string.Equals(i.ItemType, item1.ItemType, StringComparison.Ordinal)))
                {
                    item1.Include = string.Join(";", item1.Includes().Except(item2.Includes()));
                }
            }
        }

        private bool PropertiesAreEqual(ProjectPropertyElement nonConfigurationOutput, ProjectPropertyElement configurationOutput)
        {
            if (configurationOutput != null && nonConfigurationOutput != null)
            {
                return string.Equals(nonConfigurationOutput.Value, configurationOutput.Value, StringComparison.Ordinal);
            }

            return configurationOutput == nonConfigurationOutput;
        }

        private void PerformPropertyAndItemMappings(
            CommonCompilerOptions compilerOptions,
            ProjectPropertyGroupElement propertyGroup, 
            ProjectItemGroupElement itemGroup,
            ITransformApplicator transformApplicator,
            string projectDirectory)
        {
            foreach (var transform in _propertyTransforms)
            {
                transformApplicator.Execute(transform.Transform(compilerOptions), propertyGroup);
            }

            foreach (var includeContextTransformExecute in _includeContextTransformExecutes)
            {
                transformApplicator.Execute(
                    includeContextTransformExecute(compilerOptions, projectDirectory),
                    itemGroup,
                    mergeExisting: true);
            }
        }

        private void CleanExistingProperties(ProjectRootElement csproj)
        {
            var existingPropertiesToRemove = new [] {"OutputType", "TargetExt"};

            foreach (var propertyName in existingPropertiesToRemove)
            {
                var properties = csproj.Properties.Where(p => p.Name == propertyName);

                foreach (var property in properties)
                {
                    property.Parent.RemoveChild(property);
                }
            }
        }

        private IncludeContext GetCompileIncludeContext(CommonCompilerOptions compilerOptions, string projectDirectory)
        {
            // Defaults from src/Microsoft.DotNet.ProjectModel/ProjectReader.cs #L596
            return compilerOptions.CompileInclude ??
                new IncludeContext(
                    projectDirectory,
                    "compile",
                    new JObject(),
                    ProjectFilesCollection.DefaultCompileBuiltInPatterns,
                    ProjectFilesCollection.DefaultBuiltInExcludePatterns);
        }

        private IncludeContext GetEmbedIncludeContext(CommonCompilerOptions compilerOptions, string projectDirectory)
        {
            // Defaults from src/Microsoft.DotNet.ProjectModel/ProjectReader.cs #L602
            return compilerOptions.EmbedInclude ??
                new IncludeContext(
                    projectDirectory,
                    "embed",
                    new JObject(),
                    ProjectFilesCollection.DefaultResourcesBuiltInPatterns,
                    ProjectFilesCollection.DefaultBuiltInExcludePatterns);
        }

        private IncludeContext GetCopyToOutputIncludeContext(CommonCompilerOptions compilerOptions, string projectDirectory)
        {
            // Defaults from src/Microsoft.DotNet.ProjectModel/ProjectReader.cs #608
            return compilerOptions.CopyToOutputInclude ??
                new IncludeContext(
                    projectDirectory,
                    "copyToOutput",
                    new JObject(),
                    null,
                    ProjectFilesCollection.DefaultPublishExcludePatterns);
        }
    }
}
