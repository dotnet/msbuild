// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    [Generator]
    public partial class RazorSourceGenerator : ISourceGenerator
    {
        private static RazorSourceGenerationContext? _razorContext { get; set; }
        private static readonly SourceText ProvideApplicationPartFactoryAttributeSourceText = GetProvideApplicationPartFactorySourceText();

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var _razorContext = new RazorSourceGenerationContext(context);
            if (_razorContext is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(RazorDiagnostics.InvalidRazorContextComputedDescriptor, Location.None));
                return;
            }

            if (_razorContext.RazorFiles.Count == 0 && _razorContext.CshtmlFiles.Count == 0)
            {
                return;
            }

            if (_razorContext.SuppressRazorSourceGenerator)
            {
                return;
            }

            HandleDebugSwitch(_razorContext.WaitForDebugger);

            var tagHelpers = ResolveTagHelperDescriptors(context, _razorContext);

            AssertOrFailFast(tagHelpers.Count == 0, "No tag helpers resolved.");

            var projectEngine = RazorProjectEngine.Create(_razorContext.Configuration, _razorContext.FileSystem, b =>
            {
                b.Features.Add(new DefaultTypeNameFeature());
                b.SetRootNamespace(_razorContext.RootNamespace);

                b.Features.Add(new ConfigureRazorCodeGenerationOptions(options =>
                {
                    options.SuppressMetadataSourceChecksumAttributes = !_razorContext.GenerateMetadataSourceChecksumAttributes;
                }));

                b.Features.Add(new StaticTagHelperFeature { TagHelpers = tagHelpers, });
                b.Features.Add(new DefaultTagHelperDescriptorProvider());

                CompilerFeatures.Register(b);
                RazorExtensions.Register(b);

                b.SetCSharpLanguageVersion(((CSharpParseOptions)context.ParseOptions).LanguageVersion);
            });

            if (_razorContext.CshtmlFiles.Count != 0)
            {
                context.AddSource($"{context.Compilation.AssemblyName}.UnifiedAssembly.Info.g.cs", ProvideApplicationPartFactoryAttributeSourceText);
            }

            RazorGenerateForSourceTexts(_razorContext.CshtmlFiles, context, projectEngine);
            RazorGenerateForSourceTexts(_razorContext.RazorFiles, context, projectEngine);
        }
    }
}
