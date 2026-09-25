// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Microsoft.Build.Framework;

/// <summary>
/// Appends records to a node lifecycle journal file that is shared by several processes.
/// </summary>
/// <remarks>
/// <para>
/// File format: a fixed-width header line <c>#MSBuildNodeJournal v1 next=NNNNNNNNNNNNNNNNNNN</c> that holds the next
/// sequence number, followed by one flat JSON object per line (see <see cref="NodeJournalRecord"/>).
/// </para>
/// <para>
/// A writer takes a named mutex derived from the full path, reads and bumps the sequence number in the header,
/// appends its line, and flushes before it releases the mutex. Sequence numbers are therefore dense, start at 1 and
/// match the order of the lines in the file. A named mutex is used instead of <see cref="FileStream.Lock"/> because
/// byte-range locks are not supported on every platform (macOS).
/// </para>
/// </remarks>
internal sealed class NodeLifecycleJournalWriter : IDisposable
{
    internal const string HeaderPrefix = "#MSBuildNodeJournal v1 next=";
    internal const int SequenceDigits = 19;
    internal const int HeaderLength = 28 + SequenceDigits + 1; // prefix + digits + '\n'

    internal const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private readonly FileStream _stream;
    private readonly Mutex _mutex;
    private readonly byte[] _headerBuffer = new byte[HeaderLength];
    private readonly StringBuilder _line = new(256);
    private bool _disposed;

    public NodeLifecycleJournalWriter(string path)
    {
        string fullPath = Path.GetFullPath(path);
        _mutex = new Mutex(initiallyOwned: false, GetMutexName(fullPath));
        _stream = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1);
    }

    /// <summary>
    /// The name of the cross-process mutex that serializes writers of the journal at <paramref name="fullPath"/>.
    /// </summary>
    internal static string GetMutexName(string fullPath)
    {
        if (NativeMethods.IsWindows)
        {
            fullPath = fullPath.ToUpperInvariant();
        }

        ulong hash = FnvOffsetBasis;
        foreach (char c in fullPath)
        {
            hash = Fnv(hash, c);
        }

        return "MSBuildNodeJournal." + hash.ToString("X16", CultureInfo.InvariantCulture);
    }

    internal static ulong Fnv(ulong hash, uint value)
    {
        for (int i = 0; i < 4; i++)
        {
            hash ^= (byte)(value >> (i * 8));
            hash *= FnvPrime;
        }

        return hash;
    }

    /// <summary>
    /// Appends one record and returns its sequence number.
    /// </summary>
    public long Append(int processId, NodeJournalKind role, NodeJournalEvent evt, NodeJournalKind kind, int nodeId, int subjectProcessId, string? detail)
    {
        lock (_line)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NodeLifecycleJournalWriter));
            }

            bool acquired = false;
            try
            {
                try
                {
                    acquired = _mutex.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                    // A writer died while holding the lock (for example because a fault crashed it). Its record is
                    // either complete or it is an unterminated line that readers skip, so continue.
                    acquired = true;
                }

                long sequence = ReadNextSequence();
                WriteHeader(sequence + 1);

                _line.Clear();
                FormatRecord(_line, sequence, DateTime.UtcNow.Ticks, processId, role, evt, kind, nodeId, subjectProcessId, detail);
                byte[] bytes = Encoding.UTF8.GetBytes(_line.ToString());

                long end = _stream.Seek(0, SeekOrigin.End);
                if (end > HeaderLength && !EndsWithNewLine(end))
                {
                    // Terminate a line a crashed writer left behind so that this record starts on its own line.
                    _stream.WriteByte((byte)'\n');
                }

                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush();
                return sequence;
            }
            finally
            {
                if (acquired)
                {
                    _mutex.ReleaseMutex();
                }
            }
        }
    }

    internal static void FormatRecord(StringBuilder sb, long sequence, long ticks, int processId, NodeJournalKind role, NodeJournalEvent evt, NodeJournalKind kind, int nodeId, int subjectProcessId, string? detail)
    {
        sb.Append("{\"seq\":").Append(sequence.ToString(CultureInfo.InvariantCulture))
          .Append(",\"ticks\":").Append(ticks.ToString(CultureInfo.InvariantCulture))
          .Append(",\"pid\":").Append(processId.ToString(CultureInfo.InvariantCulture))
          .Append(",\"role\":\"").Append(role.ToString())
          .Append("\",\"event\":\"").Append(evt.ToString())
          .Append("\",\"kind\":\"").Append(kind.ToString())
          .Append("\",\"node\":").Append(nodeId.ToString(CultureInfo.InvariantCulture))
          .Append(",\"subject\":").Append(subjectProcessId.ToString(CultureInfo.InvariantCulture));

        if (detail is not null)
        {
            sb.Append(",\"detail\":\"");
            AppendEscaped(sb, detail);
            sb.Append('"');
        }

        sb.Append("}\n");
    }

    private static void AppendEscaped(StringBuilder sb, string value)
    {
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }
    }

    private long ReadNextSequence()
    {
        _stream.Seek(0, SeekOrigin.Begin);
        int read = 0;
        while (read < HeaderLength)
        {
            int n = _stream.Read(_headerBuffer, read, HeaderLength - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        if (read < HeaderLength)
        {
            // New (or truncated) file. Truncating is only correct when the header was never written completely.
            _stream.SetLength(0);
            return 1;
        }

        long value = 0;
        for (int i = HeaderPrefix.Length; i < HeaderPrefix.Length + SequenceDigits; i++)
        {
            int digit = _headerBuffer[i] - '0';
            if ((uint)digit > 9)
            {
                throw new InvalidDataException("Corrupt node journal header.");
            }

            value = (value * 10) + digit;
        }

        return value;
    }

    private void WriteHeader(long nextSequence)
    {
        string header = HeaderPrefix + nextSequence.ToString("D19", CultureInfo.InvariantCulture) + "\n";
        byte[] bytes = Encoding.ASCII.GetBytes(header);
        _stream.Seek(0, SeekOrigin.Begin);
        _stream.Write(bytes, 0, bytes.Length);
    }

    private bool EndsWithNewLine(long end)
    {
        _stream.Seek(end - 1, SeekOrigin.Begin);
        int last = _stream.ReadByte();
        _stream.Seek(end, SeekOrigin.Begin);
        return last == '\n';
    }

    public void Dispose()
    {
        lock (_line)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stream.Dispose();
            _mutex.Dispose();
        }
    }
}
