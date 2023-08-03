// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization.Json;
using System.Text.Json;
using Microsoft.DotNet.Watcher.Internal;

namespace Microsoft.DotNet.Watcher.Tools;

public class FileSetSerializerTests
{
    [Fact]
    public async Task Serialize()
    {
        var fileSetResult = new MSBuildFileSetResult()
        {
            IsNetCoreApp = true,
            TargetFrameworkVersion = "net5.0",
            RuntimeIdentifier = "win-arm64",
            DefaultAppHostRuntimeIdentifier = "",
            RunCommand = "run",
            RunArguments = "args",
            RunWorkingDirectory = "dir",
            Projects = new() { { "proj", new() { Files = new() { "a.cs", "b.cs" }, StaticFiles = new() { new() { FilePath = "path1", StaticWebAssetPath = "path2" } } } } }
        };

        using var stream = new MemoryStream();

        var serializer = new DataContractJsonSerializer(fileSetResult.GetType(), new DataContractJsonSerializerSettings
        {
            UseSimpleDictionaryFormat = true,
        });

        using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, ownsStream: false, indent: true))
        {
            serializer.WriteObject(writer, fileSetResult);
        }

        stream.Position = 0;

        await JsonSerializer.DeserializeAsync<MSBuildFileSetResult>(stream, cancellationToken: CancellationToken.None);
    }
}
