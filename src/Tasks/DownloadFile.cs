﻿﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = System.Threading.Tasks.Task;

#nullable enable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a task that can download a file.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public sealed class DownloadFile : TaskExtension, ICancelableTask, IIncrementalTask, IMultiThreadableTask
    {
        /// <summary>
        /// The buffer size <see cref="Stream.CopyToAsync(Stream)"/> uses when the caller does not supply one.
        /// The overload that accepts a cancellation token but not a buffer size is unavailable on .NET Framework,
        /// so the default is restated here rather than left to the runtime.
        /// </summary>
        private const int DefaultCopyBufferSize = 81920;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Gets or sets an optional filename for the destination file.  By default, the filename is derived from the <see cref="SourceUrl"/> if possible.
        /// </summary>
        public ITaskItem? DestinationFileName { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> that specifies the destination folder to download the file to.
        /// </summary>
        [Required]
        public ITaskItem? DestinationFolder { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> that contains details about the downloaded file.
        /// </summary>
        [Output]
        public ITaskItem? DownloadedFile { get; set; }

        /// <summary>
        /// Gets or sets an optional number of times to retry if possible.
        /// </summary>
        public int Retries { get; set; }

        /// <summary>
        /// Gets or sets the number of milliseconds to wait before retrying.
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 5 * 1000;

        /// <summary>
        /// Gets or sets an optional value indicating whether or not the download should be skipped if the file is up-to-date.
        /// </summary>
        public bool SkipUnchangedFiles { get; set; } = true;

        /// <summary>
        /// Gets or sets the URL to download.
        /// </summary>
        [Required]
        public string SourceUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of milliseconds to wait before the request times out.
        /// </summary>
        public int Timeout { get; set; } = 100_000;

        public bool FailIfNotIncremental { get; set; }

        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        /// <summary>
        /// Gets or sets a <see cref="HttpMessageHandler"/> to use.  This is used by unit tests to mock a connection to a remote server.
        /// </summary>
        internal HttpMessageHandler? HttpMessageHandler { get; set; }

        /// <inheritdoc cref="ICancelableTask.Cancel"/>
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        private async Task<bool> ExecuteAsync()
        {
            if (!Uri.TryCreate(SourceUrl, UriKind.Absolute, out Uri? uri))
            {
                Log.LogErrorWithCodeFromResources("DownloadFile.ErrorInvalidUrl", SourceUrl);
                return false;
            }

            int retryAttemptCount = 0;
            ITaskProgressReporter? progress = null;

            ITaskProgressReporter? GetProgressReporter(string filename)
            {
                return progress ??= (BuildEngine as IBuildEngine10)?.EngineServices.CreateTaskProgressReporter(
                    $"Downloading {filename}",
                    TaskProgressUnit.Bytes);
            }

            CancellationToken cancellationToken = _cancellationTokenSource.Token;

            try
            {
                while (true)
                {
                    try
                    {
                        await DownloadAsync(uri, cancellationToken, GetProgressReporter);
                        progress?.Complete("Download complete");
                        break;
                    }
                    catch (OperationCanceledException e) when (e.CancellationToken == cancellationToken)
                    {
                        // This task is being cancelled. Exit the loop.
                        progress?.Cancel("Download canceled");
                        break;
                    }
                    catch (Exception e)
                    {
                        bool canRetry = IsRetriable(e, out Exception actualException) && retryAttemptCount++ < Retries;

                        if (canRetry)
                        {
                            Log.LogWarningWithCodeFromResources("DownloadFile.Retrying", SourceUrl, retryAttemptCount + 1, RetryDelayMilliseconds, actualException.Message);

                            try
                            {
                                await Task.Delay(RetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException delayException) when (delayException.CancellationToken == cancellationToken)
                            {
                                // This task is being cancelled, exit the loop
                                progress?.Cancel("Download canceled");
                                break;
                            }
                        }
                        else
                        {
                            string flattenedMessage = TaskLoggingHelper.GetInnerExceptionMessageString(e);
                            Log.LogErrorWithCodeFromResources("DownloadFile.ErrorDownloading", SourceUrl, flattenedMessage);
                            Log.LogMessage(MessageImportance.Low, actualException.ToString());
                            progress?.Fail("Download failed");
                            break;
                        }
                    }
                }
            }
            finally
            {
                progress?.Dispose();
            }

            return !_cancellationTokenSource.IsCancellationRequested && !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Attempts to download the file.
        /// </summary>
        /// <param name="uri">The parsed <see cref="Uri"/> of the request.</param>
        /// <param name="cancellationToken">The cancellation token for the task.</param>
        /// <param name="progressFactory">Creates the reporter for the current download operation after the transfer is known to be necessary. Accepts the name of the file being downloaded.</param>
        private async Task DownloadAsync(Uri uri, CancellationToken cancellationToken, Func<string, ITaskProgressReporter?> progressFactory)
        {
            // The main reason to use HttpClient vs WebClient is because we can pass a message handler for unit tests to mock
#pragma warning disable CA2000 // Dispose objects before losing scope because HttpClientHandler is disposed by HTTPClient.Dispose()
            using (var client = new HttpClient(HttpMessageHandler ?? new HttpClientHandler(), disposeHandler: true) { Timeout = TimeSpan.FromMilliseconds(Timeout) })
            {
                // Only get the response without downloading the file so we can determine if the file is already up-to-date
                using (HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        response.EnsureSuccessStatusCode();
                    }
#if NET
                    catch (HttpRequestException)
                    {
                        throw;
#else
                    catch (HttpRequestException e)
                    {
                        // MSBuild History: CustomHttpRequestException was created as a wrapper over HttpRequestException
                        // so it could include the StatusCode. As of net5.0, the statuscode is now in HttpRequestException.
                        throw new CustomHttpRequestException(e.Message, e.InnerException, response.StatusCode);
#endif
                    }

                    if (!TryGetFileName(uri, out string filename))
                    {
                        Log.LogErrorWithCodeFromResources("DownloadFile.ErrorUnknownFileName", SourceUrl, nameof(DestinationFileName));
                        return;
                    }

                    AbsolutePath destinationFolderPath = TaskEnvironment.GetAbsolutePath(DestinationFolder!.ItemSpec);
                    DirectoryInfo destinationDirectory = Directory.CreateDirectory(destinationFolderPath);

                    var destinationFile = new FileInfo(Path.Combine(destinationDirectory.FullName, filename));

                    // The file is considered up-to-date if its the same length.  This could be inaccurate, we can consider alternatives in the future
                    if (ShouldSkip(response, destinationFile))
                    {
                        Log.LogMessageFromResources(MessageImportance.Normal, "DownloadFile.DidNotDownloadBecauseOfFileMatch", SourceUrl, destinationFile.FullName, nameof(SkipUnchangedFiles), "true");

                        DownloadedFile = new TaskItem(destinationFile.FullName);

                        return;
                    }
                    else if (FailIfNotIncremental)
                    {
                        Log.LogErrorFromResources("DownloadFile.Downloading", SourceUrl, destinationFile.FullName, response.Content.Headers.ContentLength);
                        return;
                    }

                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ITaskProgressReporter? progress = progressFactory(filename);
                        progress?.Report(new TaskProgressUpdate(0, response.Content.Headers.ContentLength, "Downloading"));

                        using (var target = new FileStream(destinationFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            // Terminal Logger renders a progress row for this download, which
                            // reports the same operation this message announces. Under the change
                            // wave the message drops to Normal so it is not shown twice; it is
                            // still written to binary logs and to console output at normal verbosity.
                            Log.LogMessageFromResources(
                                ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_13) ? MessageImportance.Normal : MessageImportance.High,
                                "DownloadFile.Downloading",
                                SourceUrl,
                                destinationFile.FullName,
                                response.Content.Headers.ContentLength);
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                            using (Stream responseStream = await response.Content.ReadAsStreamAsync(
#if NET
                            cancellationToken
#endif
                            ).ConfigureAwait(false))
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                            {
                                using (var progressStream = new ProgressReportingStream(target, progress, response.Content.Headers.ContentLength))
                                {
                                    await responseStream.CopyToAsync(progressStream, DefaultCopyBufferSize, cancellationToken).ConfigureAwait(false);
                                }
                            }

                            DownloadedFile = new TaskItem(destinationFile.FullName);
                        }
                    }
                    finally
                    {
                        if (DownloadedFile == null)
                        {
                            // Delete the file if anything goes wrong during download.  This could be destructive but we don't want to leave
                            // partially downloaded files on disk either.  Alternatively we could download to a temporary location and copy
                            // on success but we are concerned about the added I/O
                            destinationFile.Delete();
                        }
                    }
                }
            }
#pragma warning restore CA2000 // Dispose objects before losing scope
        }

        /// <summary>
        /// Determines if the specified exception is considered retriable.
        /// </summary>
        /// <param name="exception">The originally thrown exception.</param>
        /// <param name="actualException">The actual exception to be used for logging errors.</param>
        /// <returns><code>true</code> if the exception is retriable, otherwise <code>false</code>.</returns>
        private static bool IsRetriable(Exception exception, out Exception actualException)
        {
            actualException = exception;

            // Get aggregate inner exception
            if (actualException is AggregateException aggregateException && aggregateException.InnerException != null)
            {
                actualException = aggregateException.InnerException;
            }

            // Some HttpRequestException have an inner exception that has the real error
            if (actualException is HttpRequestException httpRequestException)
            {
                if (httpRequestException.InnerException != null)
                {
                    actualException = httpRequestException.InnerException;

                    // An IOException inside of a HttpRequestException means that something went wrong while downloading
                    if (actualException is IOException)
                    {
                        return true;
                    }
                }

#if NET
                // net5.0 included StatusCode in the HttpRequestException.
                switch (httpRequestException.StatusCode)
                {
                    case HttpStatusCode.InternalServerError:
                    case HttpStatusCode.RequestTimeout:
                        return true;
                }
            }
#else
            }

            // framework workaround for HttpRequestException not containing StatusCode
            if (actualException is CustomHttpRequestException customHttpRequestException)
            {
                switch (customHttpRequestException.StatusCode)
                {
                    case HttpStatusCode.InternalServerError:
                    case HttpStatusCode.RequestTimeout:
                        return true;
                }
            }
#endif

            if (actualException is WebException webException)
            {
                // WebException is thrown when accessing the Content of the response
                switch (webException.Status)
                {
                    // Don't retry on anything that cannot be compensated for
                    case WebExceptionStatus.TrustFailure:
                    case WebExceptionStatus.MessageLengthLimitExceeded:
                    case WebExceptionStatus.RequestProhibitedByCachePolicy:
                    case WebExceptionStatus.RequestProhibitedByProxy:
                        return false;

                    default:
                        // Retry on all other WebExceptions
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to get the file name to use when downloading the file.
        /// </summary>
        /// <param name="requestUri">The uri we sent request to.</param>
        /// <param name="filename">Receives the name of the file.</param>
        /// <returns><code>true</code> if a file name could be determined, otherwise <code>false</code>.</returns>
        private bool TryGetFileName(Uri requestUri, out string filename)
        {
            if (requestUri == null)
            {
                throw new ArgumentNullException(nameof(requestUri));
            }

            // Not all URIs contain a file name so users will have to specify one
            // Example: http://www.download.com/file/1/

            filename = DestinationFileName is { ItemSpec: string specifiedName } && !string.IsNullOrWhiteSpace(specifiedName)
                ? specifiedName // Get the file name from what the user specified
                : Path.GetFileName(requestUri.LocalPath); // Otherwise attempt to get a file name from the URI

            return !string.IsNullOrWhiteSpace(filename);
        }

#if !NET
        /// <summary>
        /// Represents a wrapper around the <see cref="HttpRequestException"/> that also contains the <see cref="HttpStatusCode"/>.
        /// DEPRECATED as of net5.0, which included the StatusCode in the HttpRequestException class.
        /// </summary>
        private sealed class CustomHttpRequestException : HttpRequestException
        {
            public CustomHttpRequestException(string message, Exception inner, HttpStatusCode statusCode)
                : base(message, inner)
            {
                StatusCode = statusCode;
            }

            public HttpStatusCode StatusCode { get; }
        }
#endif

        private sealed class ProgressReportingStream : Stream
        {
            private readonly Stream _inner;
            private readonly ITaskProgressReporter? _progress;
            private readonly long? _total;
            private long _completed;

            public ProgressReportingStream(Stream inner, ITaskProgressReporter? progress, long? total)
            {
                _inner = inner;
                _progress = progress;
                _total = total;
            }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }

            public override void Flush() => _inner.Flush();

            public override int Read(byte[] buffer, int offset, int count)
                => _inner.Read(buffer, offset, count);

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                _inner.ReadAsync(buffer, offset, count, cancellationToken);

            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count)
            {
                _inner.Write(buffer, offset, count);
                Report(count);
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
#pragma warning disable CA1835 // Use the compatible overload for the .NET Framework target.
                await _inner.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA1835
                Report(count);
            }

            private void Report(int bytesRead)
            {
                _completed += bytesRead;
                _progress?.Report(new TaskProgressUpdate(_completed, _total, "Downloading"));
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
            }
        }

        private bool ShouldSkip(HttpResponseMessage response, FileInfo destinationFile)
        {
            return SkipUnchangedFiles
                   && destinationFile.Exists
                   && destinationFile.Length == response.Content.Headers.ContentLength
                   && response.Content.Headers.LastModified.HasValue
                   && destinationFile.LastWriteTimeUtc > response.Content.Headers.LastModified.Value.UtcDateTime;
        }
    }
}