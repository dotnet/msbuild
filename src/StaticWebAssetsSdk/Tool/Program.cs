// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.IO.Compression;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tool
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            CliRootCommand rootCommand = new();
            CliCommand brotli = new("brotli");

            CliOption<CompressionLevel> compressionLevelOption = new("-c")
            {
                DefaultValueFactory = (_) => CompressionLevel.SmallestSize,
                Description = "System.IO.Compression.CompressionLevel for the Brotli compression algorithm."
            };
            CliOption<List<string>> sourcesOption = new("-s")
            {
                Description = "A list of files to compress.",
                AllowMultipleArgumentsPerToken = false
            };
            CliOption<List<string>> outputsOption = new("-o")
            {
                Description = "The filenames to output the compressed file to.",
                AllowMultipleArgumentsPerToken = false
            };

            brotli.Add(compressionLevelOption);
            brotli.Add(sourcesOption);
            brotli.Add(outputsOption);

            rootCommand.Add(brotli);

            brotli.SetAction((ParseResult parseResults) =>
            {
                var c = parseResults.GetValue(compressionLevelOption);
                var s = parseResults.GetValue(sourcesOption);
                var o = parseResults.GetValue(outputsOption);

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

            return rootCommand.Parse(args).Invoke();
        }
    }
}
