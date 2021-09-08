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
        private static RazorSourceGeneratorEventSource Log => RazorSourceGeneratorEventSource.Log;

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var razorSourceGeneratorOptionsWithDiagnostics = context.AnalyzerConfigOptionsProvider
                .Combine(context.ParseOptionsProvider)
                .Select(ComputeRazorSourceGeneratorOptions);
            var razorSourceGeneratorOptions = razorSourceGeneratorOptionsWithDiagnostics.ReportDiagnostics(context);

            var sourceItemsWithDiagnostics = context.AdditionalTextsProvider
                .Where(static (file) => file.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || file.Path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                .Combine(context.AnalyzerConfigOptionsProvider)
                .Select(ComputeProjectItems);

            var sourceItems = sourceItemsWithDiagnostics
                .ReportDiagnostics(context);

            var hasRazorFiles = sourceItems.Collect()
                .Select(static (sourceItems, _) => sourceItems.Any());

            var importFiles = sourceItems.Where(static file =>
            {
                var path = file.FilePath;
                if (path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    return string.Equals(fileName, "_Imports", StringComparison.OrdinalIgnoreCase);
                }
                else if (path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    return string.Equals(fileName, "_ViewImports", StringComparison.OrdinalIgnoreCase);
                }

                return false;
            });

            var componentFiles = sourceItems.Where(static file => file.FilePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase));

            var generatedDeclarationCode = componentFiles
                .Combine(importFiles.Collect())
                .Combine(razorSourceGeneratorOptions)
                .Select(static (pair, _) =>
                {

                    var ((sourceItem, importFiles), razorSourceGeneratorOptions) = pair;
                    RazorSourceGeneratorEventSource.Log.GenerateDeclarationCodeStart(sourceItem.FilePath);

                    if (razorSourceGeneratorOptions.SuppressRazorSourceGenerator)
                    {
                        RazorSourceGeneratorEventSource.Log.GenerateDeclarationCodeStop(sourceItem.FilePath);
                        return null;
                    }

                    var projectEngine = GetDeclarationProjectEngine(sourceItem, importFiles, razorSourceGeneratorOptions);

                    var codeGen = projectEngine.Process(sourceItem);

                    var result = codeGen.GetCSharpDocument().GeneratedCode;

                    RazorSourceGeneratorEventSource.Log.GenerateDeclarationCodeStop(sourceItem.FilePath);

                    return result;
                });

            var generatedDeclarationSyntaxTrees = generatedDeclarationCode
                .Combine(context.ParseOptionsProvider)
                .Select(static (pair, _) =>
                {
                    var (generatedDeclarationCode, parseOptions) = pair;
                    if (generatedDeclarationCode is null)
                    {
                        return null;
                    }

                    return CSharpSyntaxTree.ParseText(generatedDeclarationCode, (CSharpParseOptions)parseOptions);
                });

            var tagHelpersFromCompilation = context.CompilationProvider
                .Combine(generatedDeclarationSyntaxTrees.Collect())
                .Combine(razorSourceGeneratorOptions)
                .Select(static (pair, _) =>
                {
                    RazorSourceGeneratorEventSource.Log.DiscoverTagHelpersFromCompilationStart();

                    var ((compilation, generatedDeclarationSyntaxTrees), razorSourceGeneratorOptions) = pair;

                    if (razorSourceGeneratorOptions.SuppressRazorSourceGenerator)
                    {
                        RazorSourceGeneratorEventSource.Log.DiscoverTagHelpersFromCompilationStop();
                        return ImmutableArray<TagHelperDescriptor>.Empty;
                    }

                    var tagHelperFeature = new StaticCompilationTagHelperFeature();
                    var discoveryProjectEngine = GetDiscoveryProjectEngine(compilation.References.ToImmutableArray(), tagHelperFeature);

                    var compilationWithDeclarations = compilation.AddSyntaxTrees(generatedDeclarationSyntaxTrees);

                    tagHelperFeature.Compilation = compilationWithDeclarations;
                    tagHelperFeature.TargetAssembly = compilationWithDeclarations.Assembly;

                    var result = (IList<TagHelperDescriptor>)tagHelperFeature.GetDescriptors();
                    RazorSourceGeneratorEventSource.Log.DiscoverTagHelpersFromCompilationStop();
                    return result;
                })
                .WithLambdaComparer(static (a, b) =>
                {
                    if (a.Count != b.Count)
                    {
                        return false;
                    }

                    for (var i = 0; i < a.Count; i++)
                    {
                        if (!a[i].Equals(b[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                }, getHashCode: static a => a.Count);

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
                    RazorSourceGeneratorEventSource.Log.DiscoverTagHelpersFromReferencesStart();

                    var ((compilation, razorSourceGeneratorOptions), hasRazorFiles) = pair;

                    if (razorSourceGeneratorOptions.SuppressRazorSourceGenerator)
                    {
                        RazorSourceGeneratorEventSource.Log.DiscoverTagHelpersFromReferencesStop();
                        return ImmutableArray<TagHelperDescriptor>.Empty;
                    }

                    if (!hasRazorFiles)
                    {
                        // If there's no razor code in this app, don't do anything.
                        RazorSourceGeneratorEventSource.Log.DiscoverTagHelpersFromReferencesStop();
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

                    RazorSourceGeneratorEventSource.Log.DiscoverTagHelpersFromReferencesStop();
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

                    RazorSourceGeneratorEventSource.Log.RazorCodeGenerateStart(sourceItem.FilePath);

                    if (razorSourceGeneratorOptions.SuppressRazorSourceGenerator)
                    {
                        RazorSourceGeneratorEventSource.Log.RazorCodeGenerateStop(sourceItem.FilePath);
                        return default;
                    }

                    // Add a generated suffix so tools, such as coverlet, consider the file to be generated
                    var hintName = GetIdentifierFromPath(sourceItem.RelativePhysicalPath) + ".g.cs";

                    var projectEngine = GetGenerationProjectEngine(allTagHelpers, sourceItem, imports, razorSourceGeneratorOptions);

                    var codeDocument = projectEngine.Process(sourceItem);
                    var csharpDocument = codeDocument.GetCSharpDocument();

                    RazorSourceGeneratorEventSource.Log.RazorCodeGenerateStop(sourceItem.FilePath);
                    return (hintName, csharpDocument);
                })
                .WithLambdaComparer(static (a, b) =>
                {
                    if (a.hintName is null)
                    {
                        // Source generator is suppressed.
                        return false;
                    }

                    if (a.csharpDocument.Diagnostics.Count > 0 || b.csharpDocument.Diagnostics.Count > 0)
                    {
                        // if there are any diagnostics, treat the documents as unequal and force RegisterSourceOutput to be called uncached.
                        return false;
                    }

                    return string.Equals(a.csharpDocument.GeneratedCode, b.csharpDocument.GeneratedCode, StringComparison.Ordinal);
                }, static a => StringComparer.Ordinal.GetHashCode(a.csharpDocument));

            context.RegisterSourceOutput(generatedOutput, static (context, pair) =>
            {
                var (hintName, csharpDocument) = pair;
                RazorSourceGeneratorEventSource.Log.AddSyntaxTrees(hintName);
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
