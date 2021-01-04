// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tool
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            var rootCommand = new RootCommand();
            var brotli = new Command("brotli");

            var compressionLevelOption = new Option<CompressionLevel>(
                "-c",
                getDefaultValue: () => CompressionLevel.Optimal,
                description: "System.IO.Compression.CompressionLevel for the Brotli compression algorithm.");
            var sourcesOption = new Option<List<string>>(
                "-s",
                description: "A list of files to compress.") { AllowMultipleArgumentsPerToken = false };
            var outputsOption = new Option<List<string>>(
                "-o",
                "The filenames to output the compressed file to.") { AllowMultipleArgumentsPerToken = false };

            brotli.Add(compressionLevelOption);
            brotli.Add(sourcesOption);
            brotli.Add(outputsOption);

            rootCommand.Add(brotli);

            brotli.Handler = CommandHandler.Create<CompressionLevel, List<string>, List<string>>((c, s, o) =>
            {
                Parallel.For(0, s.Count, i =>
                {
                    var source = s[i];
                    var output = o[i];

                    using var sourceStream = File.OpenRead(source);
                    using var fileStream = new FileStream(output, FileMode.Create);

                    using var stream = new BrotliStream(fileStream, c);

                    sourceStream.CopyTo(stream);
                });
            });

            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
