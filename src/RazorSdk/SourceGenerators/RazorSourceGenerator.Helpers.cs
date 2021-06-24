// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public partial class RazorSourceGenerator
    {
        private static string GetIdentifierFromPath(string filePath)
        {
            var builder = new StringBuilder(filePath.Length);

            for (var i = 0; i < filePath.Length; i++)
            {
                switch (filePath[i])
                {
                    case ':' or '\\' or '/':
                    case char ch when !char.IsLetterOrDigit(ch):
                        builder.Append('_');
                        break;
                    default:
                        builder.Append(filePath[i]);
                        break;
                }
            }

            return builder.ToString();
        }

        private static void HandleDebugSwitch(bool waitForDebugger)
        {
            if (waitForDebugger)
            {
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(3000);
                }
            }
        }

        private static RazorProjectEngine GetDiscoveryProjectEngine(StaticCompilationTagHelperFeature tagHelperFeature, IEnumerable<MetadataReference> references, IEnumerable<SourceGeneratorProjectItem> items, string rootNamespace)
        {
            var config = RazorConfiguration.Create(RazorLanguageVersion.Latest, "default", Enumerable.Empty<RazorExtension>(), true);

            var fileSystem = new VirtualRazorProjectFileSystem();
            foreach (var item in items)
            {
                fileSystem.Add(item);
            }

            var discoveryProjectEngine = RazorProjectEngine.Create(config, fileSystem, b =>
            {
                b.Features.Add(new DefaultTypeNameFeature());
                b.Features.Add(new ConfigureRazorCodeGenerationOptions(options =>
                {
                    options.SuppressPrimaryMethodBody = true;
                    options.SuppressChecksum = true;
                }));

                b.SetRootNamespace(rootNamespace);

                b.Features.Add(new DefaultMetadataReferenceFeature { References = references.ToList() });

                b.Features.Add(tagHelperFeature);
                b.Features.Add(new DefaultTagHelperDescriptorProvider());

                CompilerFeatures.Register(b);
                RazorExtensions.Register(b);

                b.SetCSharpLanguageVersion(LanguageVersion.Preview);
            });

            return discoveryProjectEngine;
        }

        private static RazorProjectEngine GetGenerationProjectEngine(IReadOnlyList<TagHelperDescriptor> tagHelpers, IEnumerable<SourceGeneratorProjectItem> items, RazorSourceGenerationOptions razorSourceGeneratorOptions)
        {
            var fileSystem = new VirtualRazorProjectFileSystem();
            foreach (var item in items)
            {
                fileSystem.Add(item);
            }

            var projectEngine = RazorProjectEngine.Create(razorSourceGeneratorOptions.Configuration, fileSystem, b =>
            {
                b.Features.Add(new DefaultTypeNameFeature());
                b.SetRootNamespace(razorSourceGeneratorOptions.RootNamespace);

                b.Features.Add(new ConfigureRazorCodeGenerationOptions(options =>
                {
                    options.SuppressMetadataSourceChecksumAttributes = !razorSourceGeneratorOptions.GenerateMetadataSourceChecksumAttributes;
                }));

                b.Features.Add(new StaticTagHelperFeature { TagHelpers = tagHelpers });
                b.Features.Add(new DefaultTagHelperDescriptorProvider());

                CompilerFeatures.Register(b);
                RazorExtensions.Register(b);

                b.SetCSharpLanguageVersion(LanguageVersion.Preview);
            });

            return projectEngine;
        }

        private static TFeature? GetFeature<TFeature>(RazorProjectEngine engine)
        {
            var count = engine.EngineFeatures.Count;
            for (var i = 0; i < count; i++)
            {
                if (engine.EngineFeatures[i] is TFeature feature)
                {
                    return feature;
                }
            }

            return default;
        }
    }
}