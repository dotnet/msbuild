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
    public partial class RazorSourceGenerator
    {
        private static readonly ConcurrentDictionary<string, (SourceText, SourceText)> _sourceTextCache = new();

        private void RazorGenerateForSourceTexts(IReadOnlyList<RazorInputItem> files, GeneratorExecutionContext context, RazorProjectEngine projectEngine)
        {
            if (files.Count == 0)
            {
                return;
            }

            var arraypool = ArrayPool<(string, SourceText?)>.Shared;
            var outputs = arraypool.Rent(files.Count);

            Parallel.For(0, files.Count, GetParallelOptions(context), i =>
            {
                outputs[i] = ResolveGeneratedSourceTextFromFile(files[i], projectEngine, context);
            });

            for (var i = 0; i < files.Count; i++)
            {
                var (hint, sourceText) = outputs[i];
                if (sourceText != null)
                {
                    context.AddSource(hint, sourceText);
                }
            }

            arraypool.Return(outputs);
        }

        private static (string, SourceText?) ResolveGeneratedSourceTextFromFile(RazorInputItem file, RazorProjectEngine projectEngine, GeneratorExecutionContext context)
        {
            var hint = GetIdentifierFromPath(file.NormalizedPath);

            var entryFound = _sourceTextCache.TryGetValue(hint, out (SourceText cachedSourceText, SourceText cachedGeneratedSourceText) cachedValues);

            var sourceText = file.AdditionalText.GetText();

            if (sourceText is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(RazorDiagnostics.SourceTextNotFoundDescriptor, Location.None, hint));
                return (hint, null);
            }

            var checksum = sourceText.GetChecksum();
            var cachedSourceText = cachedValues.cachedSourceText;
            if (entryFound && cachedSourceText.GetChecksum().Equals(checksum))
            {
                return (hint, cachedValues.cachedGeneratedSourceText);
            }

            var projectItem = projectEngine.FileSystem.GetItem(file.NormalizedPath, file.FileKind);
            var codeDocument = projectEngine.Process(projectItem);
            var csharpDocument = codeDocument.GetCSharpDocument();

            for (var j = 0; j < csharpDocument.Diagnostics.Count; j++)
            {
                var razorDiagnostic = csharpDocument.Diagnostics[j];
                var csharpDiagnostic = razorDiagnostic.AsDiagnostic();
                context.ReportDiagnostic(csharpDiagnostic);
            }

            var generatedCode = csharpDocument.GeneratedCode;
            var generatedSourceText = SourceText.From(generatedCode, Encoding.UTF8);

            if (_sourceTextCache.Count > 200)
            {
                _sourceTextCache.Clear();
            }
                    
            _sourceTextCache[hint] = (sourceText, generatedSourceText);
            return (hint, generatedSourceText);
        }
    }
}
