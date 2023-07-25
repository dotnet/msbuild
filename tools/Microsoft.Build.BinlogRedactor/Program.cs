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
    internal class Program
    {
        private static void Main(string[] args)
        {
            string binlogPath = "msbuild.binlog"; // args[0];

            Replay(binlogPath, CancellationToken.None);
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

    // internal static class BinaryReaderExtensions
    // {
    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public static string ReadOptionalString(this BinaryReader reader)
    //    {
    //        return reader.ReadByte() == 0 ? null : reader.ReadString();
    //    }

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public static int Read7BitEncodedInt(this BinaryReader reader)
    //    {
    //        // Read out an Int32 7 bits at a time.  The high bit
    //        // of the byte when on means to continue reading more bytes.
    //        int count = 0;
    //        int shift = 0;
    //        byte b;
    //        do
    //        {
    //            // Check for a corrupted stream.  Read a max of 5 bytes.
    //            // In a future version, add a DataFormatException.
    //            if (shift == 5 * 7) // 5 bytes max per Int32, shift += 7
    //            {
    //                throw new FormatException();
    //            }

    //            // ReadByte handles end of stream cases for us.
    //            b = reader.ReadByte();
    //            count |= (b & 0x7F) << shift;
    //            shift += 7;
    //        } while ((b & 0x80) != 0);

    //        return count;
    //    }
    // }
}
