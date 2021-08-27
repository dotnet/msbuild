// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    [Generator]
    public partial class RazorSourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var razorSourceGeneratorOptionsWithDiagnostics = context.AnalyzerConfigOptionsProvider
                .Combine(context.ParseOptionsProvider)
                .Select(ComputeRazorSourceGeneratorOptions);
            var razorSourceGeneratorOptions = razorSourceGeneratorOptionsWithDiagnostics.ReportDiagnostics(context);

            var sourceItemsWithDiagnostics = context.AdditionalTextsProvider
                .Where(static (file) => file.Path.EndsWith(".razor", StringComparison.Ordinal) || file.Path.EndsWith(".cshtml", StringComparison.Ordinal))
                .Combine(context.AnalyzerConfigOptionsProvider)
                .Select(ComputeProjectItems);

            var sourceItems = sourceItemsWithDiagnostics
                .ReportDiagnostics(context);

            var hasRazorFiles = sourceItems.Collect()
                .Select(static (sourceItems, _) => sourceItems.Any());

            var importFiles = sourceItems.Where(static file =>
            {
                var path = file.FilePath;
                if (path.EndsWith(".razor", StringComparison.Ordinal))
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    return string.Equals(fileName, "_Imports", StringComparison.OrdinalIgnoreCase);
                }
                else if (path.EndsWith(".cshtml", StringComparison.Ordinal))
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    return string.Equals(fileName, "_ViewImports", StringComparison.OrdinalIgnoreCase);
                }

                return false;
            });

            var generatedDeclarationCode = razorSourceGeneratorOptions
                .Combine(sourceItems.Collect())
                .Select(static (pair, _) =>
                {
                    var (razorSourceGeneratorOptions, sourceItems) = pair;

                    if (razorSourceGeneratorOptions.SuppressRazorSourceGenerator)
                    {
                        return ImmutableArray<string>.Empty;
                    }

                    if (sourceItems.IsEmpty)
                    {
                        // If there's no razor code in this app, don't do anything.
                        return ImmutableArray<string>.Empty;
                    }

                    var projectEngine = GetDeclarationProjectEngine(sourceItems, razorSourceGeneratorOptions);

                    var generatedCode = ImmutableArray.CreateBuilder<string>(sourceItems.Length);
                    foreach (var file in sourceItems)
                    {
                        var codeGen = projectEngine.Process(file);
                        generatedCode.Add(codeGen.GetCSharpDocument().GeneratedCode);
                    }

                    return generatedCode.ToImmutable();
                })
                .WithLambdaComparer(static (a, b) => a.SequenceEqual(b, StringComparer.Ordinal), static a => a.Length);

            var tagHelpersFromCompilation = context.CompilationProvider
                .Combine(generatedDeclarationCode)
                .Combine(context.ParseOptionsProvider)
                .Combine(razorSourceGeneratorOptions)
                .Select(static (pair, _) =>
                {
                    var (((compilation, generatedDeclarationCode), parseOptions), razorSourceGeneratorOptions) = pair;

                    if (razorSourceGeneratorOptions.SuppressRazorSourceGenerator)
                    {
                        return ImmutableArray<TagHelperDescriptor>.Empty;
                    }

                    if (generatedDeclarationCode.IsEmpty)
                    {
                        // If there's no razor code in this app, don't do anything.
                        return ImmutableArray<TagHelperDescriptor>.Empty;
                    }

                    var tagHelperFeature = new StaticCompilationTagHelperFeature();
                    var discoveryProjectEngine = GetDiscoveryProjectEngine(compilation.References.ToImmutableArray(), tagHelperFeature);

                    var syntaxTrees = generatedDeclarationCode.Select(c => CSharpSyntaxTree.ParseText(c, (CSharpParseOptions)parseOptions));

                    var compilationWithDeclarations = compilation.AddSyntaxTrees(syntaxTrees);

                    tagHelperFeature.Compilation = compilationWithDeclarations;
                    tagHelperFeature.TargetAssembly = compilationWithDeclarations.Assembly;

                    return (IList<TagHelperDescriptor>)tagHelperFeature.GetDescriptors();
                });

            var tagHelpersFromReferences = context.CompilationProvider
                .Combine(razorSourceGeneratorOptions)
                .Combine(hasRazorFiles)
                .WithLambdaComparer(static (a, b) =>
                {
                    var ((compilationA, razorSourceGeneratorOptionsA), hasRazorFilesA) = a;
                    var ((compilationB, razorSourceGeneratorOptionsB), hasRazorFilesB) = b;

                    if (!compilationA.References.SequenceEqual(compilationB.References))
                    {
                        return false;
                    }

                    if (razorSourceGeneratorOptionsA != razorSourceGeneratorOptionsB)
                    {
                        return false;
                    }

                    return hasRazorFilesA == hasRazorFilesB;
                },
                static item =>
                {
                    // we'll use the number of references as a hashcode.
                    var ((compilationA, razorSourceGeneratorOptionsA), hasRazorFilesA) = item;
                    return compilationA.References.GetHashCode();
                })
                .Select(static (pair, _) =>
                {
                    var ((compilation, razorSourceGeneratorOptions), hasRazorFiles) = pair;

                    if (razorSourceGeneratorOptions.SuppressRazorSourceGenerator)
                    {
                        return ImmutableArray<TagHelperDescriptor>.Empty;
                    }

                    if (!hasRazorFiles)
                    {
                        // If there's no razor code in this app, don't do anything.
                        return ImmutableArray<TagHelperDescriptor>.Empty;
                    }

                    var tagHelperFeature = new StaticCompilationTagHelperFeature();
                    var discoveryProjectEngine = GetDiscoveryProjectEngine(compilation.References.ToImmutableArray(), tagHelperFeature);

                    List<TagHelperDescriptor> descriptors = new();
                    tagHelperFeature.Compilation = compilation;
                    foreach (var reference in compilation.References)
                    {
                        if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                        {
                            tagHelperFeature.TargetAssembly = assembly;
                            descriptors.AddRange(tagHelperFeature.GetDescriptors());
                        }
                    }
                    return (ICollection<TagHelperDescriptor>)descriptors;
                });

            var allTagHelpers = tagHelpersFromCompilation
                .Combine(tagHelpersFromReferences)
                .Select(static (pair, _) =>
                {
                    var (tagHelpersFromCompilation, tagHelpersFromReferences) = pair;
                    var count = tagHelpersFromCompilation.Count + tagHelpersFromReferences.Count;
                    if (count == 0)
                    {
                        return Array.Empty<TagHelperDescriptor>();
                    }

                    var allTagHelpers = new TagHelperDescriptor[count];
                    tagHelpersFromCompilation.CopyTo(allTagHelpers, 0);
                    tagHelpersFromReferences.CopyTo(allTagHelpers, tagHelpersFromCompilation.Count);

                    return allTagHelpers;
                });

            var generatedOutput = sourceItems
                .Combine(importFiles.Collect())
                .Combine(allTagHelpers)
                .Combine(razorSourceGeneratorOptions)
                .Combine(context.ParseOptionsProvider)
                .Select(static (pair, _) =>
                {
                    var ((((sourceItem, imports), allTagHelpers), razorSourceGeneratorOptions), parserOptions) = pair;
                    if (razorSourceGeneratorOptions.SuppressRazorSourceGenerator)
                    {
                        return default;
                    }

                    // Add a generated suffix so tools, such as coverlet, consider the file to be generated
                    var hintName = GetIdentifierFromPath(sourceItem.RelativePhysicalPath) + ".g.cs";

                    var projectEngine = GetGenerationProjectEngine(allTagHelpers, sourceItem, imports, razorSourceGeneratorOptions);

                    var codeDocument = projectEngine.Process(sourceItem);
                    var csharpDocument = codeDocument.GetCSharpDocument();

                    return (hintName, csharpDocument);
                });

            context.RegisterSourceOutput(generatedOutput, static (context, pair) =>
            {
                var (hintName, csharpDocument) = pair;
                if (hintName is null)
                {
                    // Source generator is suppressed.
                    return;
                }

                for (var i = 0; i < csharpDocument.Diagnostics.Count; i++)
                {
                    var razorDiagnostic = csharpDocument.Diagnostics[i];
                    var csharpDiagnostic = razorDiagnostic.AsDiagnostic();
                    context.ReportDiagnostic(csharpDiagnostic);
                }

                context.AddSource(hintName, csharpDocument.GeneratedCode);
            });
        }
    }
}
