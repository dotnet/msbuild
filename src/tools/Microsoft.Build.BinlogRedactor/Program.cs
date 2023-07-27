using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.Build.BinlogRedactor
{
    internal sealed class Program
    {
        // https://github.com/dotnet/templating/blob/7a437b5e79899092000d1fcf72376ed658bb8366/src/Microsoft.TemplateEngine.Core/Util/StreamProxy.cs#L13
        private class MyStreamProxy : Stream
        {
            public static MyStreamProxy Instance;

            private readonly Stream _sourceStream;
            private readonly Stream _targetStream;

            private bool IsPwd(string text)
            {
                const string pwd = "restore";
                return text.Contains(pwd, StringComparison.CurrentCulture);
            }

            public static string ReplacePwd(string text)
            {
                const string pwd = "restore";
                return text.Replace(pwd, "*******", StringComparison.CurrentCulture);
            }

            public MyStreamProxy(Stream sourceStream)
            {
                _sourceStream = sourceStream;
                Instance = this;
            }

            private long _stringStartPosition = -1;
            public void HandleStringStart()
            {
                _stringStartPosition = this.Position;
            }

            private bool _isRewritingOutput = false;

            private void StartRewritingOutput()
            {
                if (_isRewritingOutput)
                {
                    return;
                }

                // todo: catchup writing to output.
                // use the current position to determine how much to write to the output stream.

                _isRewritingOutput = true;
            }

            public void HandleStringRead(string text)
            {
                if (IsPwd(text))
                {
                    if (!_isRewritingOutput)
                    {
                        StartRewritingOutput();
                    }
                }

                _stringStartPosition = -1;
            }

            public override bool CanRead => _sourceStream.CanRead;

            public override bool CanSeek => _sourceStream.CanSeek;

            public override bool CanWrite => _sourceStream.CanWrite;

            public override long Length => _sourceStream.Length;

            public override long Position
            {
                get => _sourceStream.Position;

                set
                {
                    UnexpectedCall();
                    _sourceStream.Position = value;
                }
            }

            public override void Flush() => _sourceStream.Flush();

            public override long Seek(long offset, SeekOrigin origin) => _sourceStream.Seek(offset, origin);

            public override void SetLength(long value)
            {
                UnexpectedCall();
                _sourceStream.SetLength(value);
            }

            public override int Read(byte[] buffer, int offset, int count) => _sourceStream.Read(buffer, offset, count);

            public override void Write(byte[] buffer, int offset, int count)
            {
                UnexpectedCall();
                _sourceStream.Write(buffer, offset, count);
            }

            private void UnexpectedCall([CallerMemberName] string caller = null)
            {
                throw new InvalidOperationException($"Unexpected call to {caller}");
            }
        }

        private static void Main(string[] args)
        {
            string binlogPath = "msbuild.binlog"; // args[0];

            //BinaryLogReplayEventSource eventSource = new BinaryLogReplayEventSource();
            //eventSource.OnStringRead += MyStreamProxy.Instance.HandleStringRead;
            //eventSource.OnStringEncountered += MyStreamProxy.Instance.HandleStringStart;
            //eventSource.Replay(binlogPath, CancellationToken.None, stream => new MyStreamProxy(stream));

            ////Replay(binlogPath, CancellationToken.None);


            // Quick way:
            //
            ////BinaryLogReplayEventSource originalEventsSource = new BinaryLogReplayEventSource();
            ////BinaryLogger bl = new BinaryLogger()
            ////{
            ////    Parameters = $"LogFile=_rewritenMsbuild.binlog",
            ////};
            ////bl.Initialize(originalEventsSource);
            ////originalEventsSource.CurrateReadString += OriginalEventsSource_OnStringRead;
            ////originalEventsSource.Replay(binlogPath, CancellationToken.None);
            ////bl.Shutdown();
        }

        private static string OriginalEventsSource_OnStringRead(string str)
        {
            return MyStreamProxy.ReplacePwd(str);
        }

        const int FileFormatVersion = 16;

        public enum BinaryLogRecordKind
        {
            String = 24,
        }

        public static void Replay(string sourceFilePath, CancellationToken cancellationToken)
        {
            using (var stream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);

                // wrapping the GZipStream in a buffered stream significantly improves performance
                // and the max throughput is reached with a 32K buffer. See details here:
                // https://github.com/dotnet/runtime/issues/39233#issuecomment-745598847
                var bufferedStream = new BufferedStream(gzipStream, 32768);
                var binaryReader = new BinaryReader(bufferedStream);

                int fileFormatVersion = binaryReader.ReadInt32();

                // the log file is written using a newer version of file format
                // that we don't know how to read
                if (fileFormatVersion > FileFormatVersion)
                {
                    throw new NotSupportedException(string.Format("Unsupported log version: {0}", fileFormatVersion));
                }

                using var reader = new BuildEventArgsReader(binaryReader, fileFormatVersion);
                // internals visible to - should build fine
                // reader.OnBlobRead


                // TODO: inject a custom BinaryReader that will split everything read, to an output stream
                // plus it needs to allow to replace the strings without splitting them (so split needs to be delayed somehow)
                //
                // The output stream should be backed by file stream - but only flushed if we are beyond sane in-memory limit
                // or if we already performed some replacemet
                // The backing size might be guessable by the file size. After single replacement is done - than no backing, and immediate flushing makes sence
                // If the file size is too big - then the delayed flushing might not make sence either
                // other thing is embedded files - but we might possibly disregard them initialy


                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    BuildEventArgs instance = reader.Read();
                    if (instance == null)
                    {
                        break;
                    }

                    // Dispatch(instance);
                }

                // BinaryLogRecordKind recordKind;
                // while ((recordKind = (BinaryLogRecordKind)binaryReader.Read7BitEncodedInt()) == BinaryLogRecordKind.String)
                // {
                //    string s = binaryReader.ReadString();
                //    Console.WriteLine(s);
                // }

                // todo: if there was no replacement - shortcut escape here
                //  if there was replacement - write the recordKind to the output stream
                //  and then copy the rest of binaryReader to the output stream
            }
        }
    }

    //// internal static class BinaryReaderExtensions
    //// {
    ////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ////    public static string ReadOptionalString(this BinaryReader reader)
    ////    {
    ////        return reader.ReadByte() == 0 ? null : reader.ReadString();
    ////    }

    ////    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ////    public static int Read7BitEncodedInt(this BinaryReader reader)
    ////    {
    ////        // Read out an Int32 7 bits at a time.  The high bit
    ////        // of the byte when on means to continue reading more bytes.
    ////        int count = 0;
    ////        int shift = 0;
    ////        byte b;
    ////        do
    ////        {
    ////            // Check for a corrupted stream.  Read a max of 5 bytes.
    ////            // In a future version, add a DataFormatException.
    ////            if (shift == 5 * 7) // 5 bytes max per Int32, shift += 7
    ////            {
    ////                throw new FormatException();
    ////            }

    ////            // ReadByte handles end of stream cases for us.
    ////            b = reader.ReadByte();
    ////            count |= (b & 0x7F) << shift;
    ////            shift += 7;
    ////        } while ((b & 0x80) != 0);

    ////        return count;
    ////    }
    //// }
}
