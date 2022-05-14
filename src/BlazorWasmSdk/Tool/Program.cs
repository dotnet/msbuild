// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
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
                description: "A list of files to compress.")
            {
                AllowMultipleArgumentsPerToken = false
            };
            var outputsOption = new Option<List<string>>(
                "-o",
                "The filenames to output the compressed file to.")
            {
                AllowMultipleArgumentsPerToken = false
            };

            brotli.Add(compressionLevelOption);
            brotli.Add(sourcesOption);
            brotli.Add(outputsOption);

            rootCommand.Add(brotli);

            brotli.SetHandler((InvocationContext context) =>
            {
                var parseResults = context.ParseResult;
                var c = parseResults.GetValueForOption(compressionLevelOption);
                var s = parseResults.GetValueForOption(sourcesOption);
                var o = parseResults.GetValueForOption(outputsOption);

                Parallel.For(0, s.Count, i =>
                {
                    var source = s[i];
                    var output = o[i];
                    try
                    {
                        using var sourceStream = File.OpenRead(source);
                        using var fileStream = new FileStream(output, FileMode.Create);

                        using var stream = new BrotliStream(fileStream, c);
                        sourceStream.CopyTo(stream);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error compressing '{source}' into '{output}'");
                        Console.Error.WriteLine(ex.ToString());
                    }
                });
            });

            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
