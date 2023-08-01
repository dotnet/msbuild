// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BinlogRedactor.Reporting;
using Microsoft.Build.Logging;

namespace Microsoft.Build.BinlogRedactor.BinaryLog
{
    internal interface ISensitiveDataProcessor
    {
        string ReplaceSensitiveData(string text);
        bool IsSensitiveData(string text);
    }

    internal sealed class SimpleSensitiveDataProcessor : ISensitiveDataProcessor
    {
        private readonly string[] _passwordsToRedact;

        public SimpleSensitiveDataProcessor(string[] passwordsToRedact)
        {
            _passwordsToRedact = passwordsToRedact;
        }

        public string ReplaceSensitiveData(string text)
        {
            foreach (string pwd in _passwordsToRedact)
            {
                text = text.Replace(pwd, "*******", StringComparison.CurrentCulture);
            }

            return text;
        }

        public bool IsSensitiveData(string text)
        {
            return _passwordsToRedact.Any(pwd => text.Contains(pwd, StringComparison.CurrentCulture));
        }
    }

    internal interface IBinlogProcessor
    {
        Task<BinlogRedactorErrorCode> ProcessBinlog(
            string inputFileName,
            string outputFileName,
            ISensitiveDataProcessor sensitiveDataProcessor,
            CancellationToken cancellationToken);
    }

    internal sealed class SimpleBinlogProcessor : IBinlogProcessor
    {
        public Task<BinlogRedactorErrorCode> ProcessBinlog(
            string inputFileName,
            string outputFileName,
            ISensitiveDataProcessor sensitiveDataProcessor,
            CancellationToken cancellationToken)
        {
            // Quick way:
            //
            BinaryLogReplayEventSource originalEventsSource = new BinaryLogReplayEventSource();
            BinaryLogger bl = new BinaryLogger()
            {
                Parameters = $"LogFile={outputFileName}",
            };
            bl.Initialize(originalEventsSource);
            originalEventsSource.CurrateReadString += sensitiveDataProcessor.ReplaceSensitiveData;
            originalEventsSource.Replay(inputFileName, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            bl.Shutdown();

            // TODO: error handling

            return Task.FromResult(BinlogRedactorErrorCode.Success);
        }
    }

    // do not start write output until any sensitive data is found
    ////internal class DefferedBinlogProcessor : IBinlogProcessor
    ////{
    ////    // https://github.com/dotnet/templating/blob/7a437b5e79899092000d1fcf72376ed658bb8366/src/Microsoft.TemplateEngine.Core/Util/StreamProxy.cs#L13
    ////    private class MyStreamProxy : Stream
    ////    {
    ////        public static MyStreamProxy? Instance;

    ////        private readonly Stream? _sourceStream;
    ////        // private readonly Stream? _targetStream;

    ////        private bool IsPwd(string text)
    ////        {
    ////            const string pwd = "restore";
    ////            return text.Contains(pwd, StringComparison.CurrentCulture);
    ////        }

    ////        public static string ReplacePwd(string text)
    ////        {
    ////            const string pwd = "restore";
    ////            return text.Replace(pwd, "*******", StringComparison.CurrentCulture);
    ////        }

    ////        public MyStreamProxy(Stream sourceStream)
    ////        {
    ////            _sourceStream = sourceStream;
    ////            Instance = this;
    ////        }

    ////        private long _stringStartPosition = -1;
    ////        public void HandleStringStart()
    ////        {
    ////            _stringStartPosition = Position;
    ////        }

    ////        private bool _isRewritingOutput = false;

    ////        private void StartRewritingOutput()
    ////        {
    ////            if (_isRewritingOutput)
    ////            {
    ////                return;
    ////            }

    ////            // todo: catchup writing to output.
    ////            // use the current position to determine how much to write to the output stream.

    ////            _isRewritingOutput = true;
    ////        }

    ////        public void HandleStringRead(string text)
    ////        {
    ////            if (IsPwd(text))
    ////            {
    ////                if (!_isRewritingOutput)
    ////                {
    ////                    StartRewritingOutput();
    ////                }
    ////            }

    ////            _stringStartPosition = -1;
    ////        }

    ////        public override bool CanRead => _sourceStream.CanRead;

    ////        public override bool CanSeek => _sourceStream.CanSeek;

    ////        public override bool CanWrite => _sourceStream.CanWrite;

    ////        public override long Length => _sourceStream.Length;

    ////        public override long Position
    ////        {
    ////            get => _sourceStream.Position;

    ////            set
    ////            {
    ////                UnexpectedCall();
    ////                _sourceStream.Position = value;
    ////            }
    ////        }

    ////        public override void Flush() => _sourceStream.Flush();

    ////        public override long Seek(long offset, SeekOrigin origin) => _sourceStream.Seek(offset, origin);

    ////        public override void SetLength(long value)
    ////        {
    ////            UnexpectedCall();
    ////            _sourceStream.SetLength(value);
    ////        }

    ////        public override int Read(byte[] buffer, int offset, int count) => _sourceStream.Read(buffer, offset, count);

    ////        public override void Write(byte[] buffer, int offset, int count)
    ////        {
    ////            UnexpectedCall();
    ////            _sourceStream.Write(buffer, offset, count);
    ////        }

    ////        private void UnexpectedCall([CallerMemberName] string? caller = null)
    ////        {
    ////            throw new InvalidOperationException($"Unexpected call to {caller}");
    ////        }
    ////    }


    ////    // TODO: inject a custom BinaryReader that will split everything read, to an output stream
    ////    // plus it needs to allow to replace the strings without splitting them (so split needs to be delayed somehow)
    ////    //
    ////    // The output stream should be backed by file stream - but only flushed if we are beyond sane in-memory limit
    ////    // or if we already performed some replacemet
    ////    // The backing size might be guessable by the file size. After single replacement is done - than no backing, and immediate flushing makes sence
    ////    // If the file size is too big - then the delayed flushing might not make sence either
    ////    //
    ////    // Or we might just read through and once secret is hit, then reopen the file and copy portion to output and then continue shadowed writing
    ////    //
    ////    // other thing is embedded files - but we might possibly disregard them initialy

    ////    public Task<BinlogRedactorErrorCode> ProcessBinlog(
    ////        string inputFileName,
    ////        string outputFileName,
    ////        ISensitiveDataProcessor sensitiveDataProcessor,
    ////        CancellationToken token)
    ////    {
    ////        BinaryLogReplayEventSource eventSource = new BinaryLogReplayEventSource();
    ////        eventSource.OnStringRead += MyStreamProxy.Instance.HandleStringRead;
    ////        eventSource.OnStringEncountered += MyStreamProxy.Instance.HandleStringStart;
    ////        eventSource.Replay(inputFileName, CancellationToken.None, stream => new MyStreamProxy(stream));

    ////        // TODO: error handling

    ////        return Task.FromResult(BinlogRedactorErrorCode.Success);
    ////    }
    ////}
}
