// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    internal class RazorSourceGenerationContext
    {
        public string RootNamespace { get; private set; } = "ASP";

        public IReadOnlyList<RazorInputItem> RazorFiles { get; private set; } = Array.Empty<RazorInputItem>();

        public IReadOnlyList<RazorInputItem> CshtmlFiles { get; private set; } = Array.Empty<RazorInputItem>();

        public VirtualRazorProjectFileSystem FileSystem { get; private set; }

        public RazorConfiguration Configuration { get; private set; }

        /// <summary>
        /// Gets a flag that determines if the source generator waits for the debugger to attach.
        /// <para>
        /// To configure this using MSBuild, use the <c>_RazorSourceGeneratorDebug</c> property.
        /// For instance <c>dotnet msbuild /p:_RazorSourceGeneratorDebug=true</c>
        /// </para>
        /// </summary>
        public bool WaitForDebugger { get; private set; }

        /// <summary>
        /// Gets a flag that determines if generated Razor views and Pages includes the <c>RazorSourceChecksumAttribute</c>.
        /// </summary>
        public bool GenerateMetadataSourceChecksumAttributes { get; private set; }

        /// <summary>
        /// Gets a flag that determines if the source generator should no-op.
        /// <para>
        /// This flag exists to support scenarios in VS where design-time and EnC builds need
        /// to run without invoking the source generator to avoid duplicate types being produced.
        /// The property is set by the SDK via an editor config.
        /// </para>
        /// </summary>
        public bool SuppressRazorSourceGenerator { get; private set; }

        public RazorSourceGenerationContext(GeneratorExecutionContext context)
        {
            var globalOptions = context.AnalyzerConfigOptions.GlobalOptions;

            if (!globalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace))
            {
                rootNamespace = "ASP";
            }

            var razorLanguageVersion = RazorLanguageVersion.Latest;
            if (!globalOptions.TryGetValue("build_property.RazorLangVersion", out var razorLanguageVersionString) ||
                !RazorLanguageVersion.TryParse(razorLanguageVersionString, out razorLanguageVersion))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RazorDiagnostics.InvalidRazorLangVersionDescriptor,
                    Location.None,
                    razorLanguageVersionString));
            }

            if (!globalOptions.TryGetValue("build_property.RazorConfiguration", out var configurationName))
            {
                configurationName = "default";
            }

            globalOptions.TryGetValue("build_property._RazorSourceGeneratorDebug", out var waitForDebugger);

            globalOptions.TryGetValue("build_property.SuppressRazorSourceGenerator", out var suppressRazorSourceGenerator);

            globalOptions.TryGetValue("build_property.GenerateRazorMetadataSourceChecksumAttributes", out var generateMetadataSourceChecksumAttributes);

            var razorConfiguration = RazorConfiguration.Create(razorLanguageVersion, configurationName, Enumerable.Empty<RazorExtension>(), true);
            var (razorFiles, cshtmlFiles) = GetRazorInputs(context);
            var fileSystem = GetVirtualFileSystem(context, razorFiles, cshtmlFiles);

            RootNamespace = rootNamespace;
            Configuration = razorConfiguration;
            FileSystem = fileSystem;
            RazorFiles = razorFiles;
            CshtmlFiles = cshtmlFiles;
            WaitForDebugger = waitForDebugger == "true";
            SuppressRazorSourceGenerator = suppressRazorSourceGenerator == "true";
            GenerateMetadataSourceChecksumAttributes = generateMetadataSourceChecksumAttributes == "true";
        }

        private static VirtualRazorProjectFileSystem GetVirtualFileSystem(GeneratorExecutionContext context, IReadOnlyList<RazorInputItem> razorFiles, IReadOnlyList<RazorInputItem> cshtmlFiles)
        {
            var fileSystem = new VirtualRazorProjectFileSystem();
            for (var i = 0; i < razorFiles.Count; i++)
            {
                var item = razorFiles[i];
                fileSystem.Add(new SourceGeneratorProjectItem(
                    basePath: "/",
                    filePath: item.NormalizedPath,
                    relativePhysicalPath: item.RelativePath,
                    fileKind: FileKinds.Component,
                    item.AdditionalText,
                    cssScope: item.CssScope,
                    context: context));
            }

            for (var i = 0; i < cshtmlFiles.Count; i++)
            {
                var item = cshtmlFiles[i];
                fileSystem.Add(new SourceGeneratorProjectItem(
                    basePath: "/",
                    filePath: item.NormalizedPath,
                    relativePhysicalPath: item.RelativePath,
                    fileKind: FileKinds.Legacy,
                    item.AdditionalText,
                    cssScope: item.CssScope,
                    context: context));
            }

            return fileSystem;
        }

        private static (IReadOnlyList<RazorInputItem> razorFiles, IReadOnlyList<RazorInputItem> cshtmlFiles) GetRazorInputs(GeneratorExecutionContext context)
        {
            List<RazorInputItem> razorFiles = new();
            List<RazorInputItem> cshtmlFiles = new();

            foreach (var item in context.AdditionalFiles)
            {
                var path = item.Path;
                var isComponent = path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);
                var isRazorView = path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase);

                if (!isComponent && !isRazorView)
                {
                    continue;
                }

                var options = context.AnalyzerConfigOptions.GetOptions(item);
                if (!options.TryGetValue("build_metadata.AdditionalFiles.TargetPath", out var relativePath))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        RazorDiagnostics.TargetPathNotProvided,
                        Location.None,
                        item.Path));
                    continue;
                }

                relativePath = Encoding.UTF8.GetString(Convert.FromBase64String(relativePath));

                options.TryGetValue("build_metadata.AdditionalFiles.CssScope", out var cssScope);

                var fileKind = isComponent ? FileKinds.GetComponentFileKindFromFilePath(item.Path) : FileKinds.Legacy;

                var inputItem = new RazorInputItem(item, relativePath, fileKind, cssScope);

                if (isComponent)
                {
                    razorFiles.Add(inputItem);
                }
                else
                {
                    cshtmlFiles.Add(inputItem);
                }
            }

            return (razorFiles, cshtmlFiles);
        }
    }
}
