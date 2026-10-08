// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures what task progress reporting costs the <c>DownloadFile</c> task.
/// </summary>
/// <remarks>
/// <c>DownloadFile</c> reports bytes transferred, so it calls into the reporter once per buffer
/// written rather than once per item. The transfer buffer is rented from <see cref="System.Buffers.ArrayPool{T}"/>
/// rather than allocated per download, and this benchmark exists to keep that true: a regression
/// would show up as roughly 80 KiB of extra allocation for every download. The response is served
/// from memory so the measurement reflects the task rather than a network.
/// </remarks>
[MemoryDiagnoser]
public class DownloadFileProgressBenchmark
{
    private const int PayloadBytes = 16 * 1024 * 1024;

    private byte[] _payload = null!;
    private string _destinationFolder = null!;
    private IBuildEngine _engineWithoutProgress = null!;
    private IBuildEngine _engineWithProgress = null!;

    /// <summary>
    /// Whether the host supports progress reporting.
    /// </summary>
    [Params(false, true)]
    public bool ProgressSupported { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _payload = new byte[PayloadBytes];
        new Random(42).NextBytes(_payload);

        _destinationFolder = Path.Combine(Path.GetTempPath(), "MSBuild.Benchmarks.DownloadProgress");
        Directory.CreateDirectory(_destinationFolder);

        _engineWithoutProgress = new BenchmarkBuildEngine(supportsProgress: false);
        _engineWithProgress = new BenchmarkBuildEngine(supportsProgress: true);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_destinationFolder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Benchmark]
    public bool Download()
    {
        DownloadFile download = new()
        {
            BuildEngine = ProgressSupported ? _engineWithProgress : _engineWithoutProgress,
            SourceUrl = "https://benchmark.invalid/payload.bin",
            DestinationFolder = new TaskItemStub(_destinationFolder),
            SkipUnchangedFiles = false,
            HttpMessageHandler = new InMemoryHandler(_payload),
        };

        return download.Execute();
    }

    /// <summary>
    /// Serves the payload from memory so the benchmark does not depend on a network.
    /// </summary>
    private sealed class InMemoryHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
                RequestMessage = request,
            });
    }
}
