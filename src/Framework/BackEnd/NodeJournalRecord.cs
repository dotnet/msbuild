// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Build.Framework;

/// <summary>
/// One parsed line of a node lifecycle journal.
/// </summary>
internal sealed class NodeJournalRecord
{
    public long Sequence { get; init; }

    public DateTime Timestamp { get; init; }

    /// <summary>The process that wrote the record.</summary>
    public int ProcessId { get; init; }

    /// <summary>The role of the process that wrote the record.</summary>
    public NodeJournalKind Role { get; init; }

    public NodeJournalEvent Event { get; init; }

    /// <summary>The event name as written; differs from <see cref="Event"/> only for events unknown to this reader.</summary>
    public string EventName { get; init; } = string.Empty;

    /// <summary>The kind of node the event is about.</summary>
    public NodeJournalKind Kind { get; init; }

    public int NodeId { get; init; }

    /// <summary>The process the event is about (for example the launched process), or 0.</summary>
    public int SubjectProcessId { get; init; }

    public string? Detail { get; init; }

    /// <summary>A single timeline line for failure reports.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder(96);
        sb.Append('#').Append(Sequence.ToString(CultureInfo.InvariantCulture))
          .Append(' ').Append(Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
          .Append(" pid ").Append(ProcessId.ToString(CultureInfo.InvariantCulture))
          .Append(" (").Append(Role.ToString()).Append(") ")
          .Append(EventName);

        if (Kind != NodeJournalKind.None)
        {
            sb.Append(" kind=").Append(Kind.ToString());
        }

        if (NodeId != 0)
        {
            sb.Append(" node=").Append(NodeId.ToString(CultureInfo.InvariantCulture));
        }

        if (SubjectProcessId != 0)
        {
            sb.Append(" subject=").Append(SubjectProcessId.ToString(CultureInfo.InvariantCulture));
        }

        if (Detail is not null)
        {
            sb.Append(" detail=").Append(Detail);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses one journal line (without its line terminator). Returns <see langword="null"/> for lines that are not records.
    /// </summary>
    public static NodeJournalRecord? TryParse(string line)
    {
        if (line.Length < 2 || line[0] != '{')
        {
            return null;
        }

        long sequence = 0, ticks = 0;
        int pid = 0, node = 0, subject = 0;
        string? role = null, evt = null, kind = null, detail = null;

        int i = 1;
        bool closed = false;
        while (i < line.Length)
        {
            SkipWhitespace(line, ref i);
            if (i < line.Length && line[i] == '}')
            {
                closed = true;
                break;
            }

            string? name = ReadString(line, ref i);
            SkipWhitespace(line, ref i);
            if (name is null || i >= line.Length || line[i] != ':')
            {
                return null;
            }

            i++;
            SkipWhitespace(line, ref i);
            if (i >= line.Length)
            {
                return null;
            }

            if (line[i] == '"')
            {
                string? value = ReadString(line, ref i);
                if (value is null)
                {
                    return null;
                }

                switch (name)
                {
                    case "role": role = value; break;
                    case "event": evt = value; break;
                    case "kind": kind = value; break;
                    case "detail": detail = value; break;
                }
            }
            else
            {
                int start = i;
                while (i < line.Length && (line[i] == '-' || char.IsDigit(line[i])))
                {
                    i++;
                }

                if (!long.TryParse(line.Substring(start, i - start), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number))
                {
                    return null;
                }

                switch (name)
                {
                    case "seq": sequence = number; break;
                    case "ticks": ticks = number; break;
                    case "pid": pid = (int)number; break;
                    case "node": node = (int)number; break;
                    case "subject": subject = (int)number; break;
                }
            }

            SkipWhitespace(line, ref i);
            if (i < line.Length && line[i] == ',')
            {
                i++;
            }
            else if (i >= line.Length || line[i] != '}')
            {
                return null;
            }
        }

        if (!closed || sequence <= 0 || evt is null)
        {
            return null;
        }

        return new NodeJournalRecord
        {
            Sequence = sequence,
            Timestamp = new DateTime(ticks, DateTimeKind.Utc),
            ProcessId = pid,
            Role = ParseKind(role),
            Event = Enum.TryParse(evt, out NodeJournalEvent e) ? e : NodeJournalEvent.None,
            EventName = evt,
            Kind = ParseKind(kind),
            NodeId = node,
            SubjectProcessId = subject,
            Detail = detail,
        };
    }

    private static NodeJournalKind ParseKind(string? value)
        => value is not null && Enum.TryParse(value, out NodeJournalKind kind) ? kind : NodeJournalKind.None;

    private static void SkipWhitespace(string s, ref int i)
    {
        while (i < s.Length && s[i] == ' ')
        {
            i++;
        }
    }

    private static string? ReadString(string s, ref int i)
    {
        if (i >= s.Length || s[i] != '"')
        {
            return null;
        }

        i++;
        StringBuilder? sb = null;
        int start = i;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '"')
            {
                string result = sb is null ? s.Substring(start, i - start) : sb.ToString();
                i++;
                return result;
            }

            if (c == '\\')
            {
                sb ??= new StringBuilder().Append(s, start, i - start);
                if (i + 1 >= s.Length)
                {
                    return null;
                }

                char escaped = s[i + 1];
                i += 2;
                switch (escaped)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                        {
                            return null;
                        }

                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: sb.Append(escaped); break;
                }

                continue;
            }

            sb?.Append(c);
            i++;
        }

        return null;
    }
}

/// <summary>
/// Reads a node lifecycle journal, possibly while other processes are still writing it.
/// </summary>
internal static class NodeLifecycleJournalReader
{
    /// <summary>
    /// Reads all complete records of the journal at <paramref name="path"/>.
    /// </summary>
    public static List<NodeJournalRecord> ReadAll(string path)
    {
        var records = new List<NodeJournalRecord>();
        long offset = 0;
        ReadNew(path, ref offset, records);
        return records;
    }

    /// <summary>
    /// Appends to <paramref name="records"/> the complete records written after <paramref name="offset"/>, and advances
    /// <paramref name="offset"/> past them. A trailing line that is still being written is left for the next call.
    /// </summary>
    /// <returns>The number of records added.</returns>
    public static int ReadNew(string path, ref long offset, List<NodeJournalRecord> records)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        byte[] bytes;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            long start = Math.Max(offset, NodeLifecycleJournalWriter.HeaderLength);
            long length = stream.Length;
            if (length <= start)
            {
                return 0;
            }

            stream.Seek(start, SeekOrigin.Begin);
            bytes = new byte[length - start];
            int read = 0;
            while (read < bytes.Length)
            {
                int n = stream.Read(bytes, read, bytes.Length - read);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            int lastNewLine = read == 0 ? -1 : Array.LastIndexOf(bytes, (byte)'\n', read - 1);
            if (lastNewLine < 0)
            {
                offset = start;
                return 0;
            }

            offset = start + lastNewLine + 1;
            int added = 0;
            int lineStart = 0;
            for (int i = 0; i <= lastNewLine; i++)
            {
                if (bytes[i] == '\n')
                {
                    if (i > lineStart)
                    {
                        NodeJournalRecord? record = NodeJournalRecord.TryParse(Encoding.UTF8.GetString(bytes, lineStart, i - lineStart));
                        if (record is not null)
                        {
                            records.Add(record);
                            added++;
                        }
                    }

                    lineStart = i + 1;
                }
            }

            return added;
        }
    }
}

/// <summary>
/// What an injected fault does to the process.
/// </summary>
internal enum NodeFaultAction : byte
{
    /// <summary>Terminates the process immediately, like an external kill.</summary>
    Crash,

    /// <summary>Blocks the recording thread forever.</summary>
    Hang,
}

/// <summary>
/// A fault requested through <c>MSBUILDNODEFAULT</c>: <c>Event[@Role][#Occurrence]:Crash|Hang</c>, several separated by <c>;</c>.
/// </summary>
/// <example>
/// <c>DisposalBegin@TaskHost:Crash</c> crashes a TaskHost the first time it starts disposing build-scoped objects.
/// <c>Connected@Worker#2:Hang</c> hangs a worker the second time it connects.
/// </example>
internal readonly struct NodeFaultSpec
{
    public NodeFaultSpec(NodeJournalEvent point, NodeJournalKind role, int occurrence, NodeFaultAction action)
    {
        Point = point;
        Role = role;
        Occurrence = occurrence;
        Action = action;
    }

    public NodeJournalEvent Point { get; }

    /// <summary>The process role the fault applies to; <see cref="NodeJournalKind.None"/> for any.</summary>
    public NodeJournalKind Role { get; }

    /// <summary>Which pass (1-based) through <see cref="Point"/> in a matching process triggers the fault.</summary>
    public int Occurrence { get; }

    public NodeFaultAction Action { get; }

    public bool Matches(NodeJournalEvent evt, NodeJournalKind role, int occurrence)
        => evt == Point && occurrence == Occurrence && (Role == NodeJournalKind.None || Role == role);

    public override string ToString()
    {
        string role = Role == NodeJournalKind.None ? string.Empty : "@" + Role.ToString();
        return Point.ToString() + role + "#" + Occurrence.ToString(CultureInfo.InvariantCulture) + ":" + Action.ToString();
    }

    /// <summary>
    /// Parses a single fault specification.
    /// </summary>
    public static bool TryParse(string? text, out NodeFaultSpec spec, out string? error)
    {
        spec = default;
        error = null;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            error = "The fault specification is empty.";
            return false;
        }

        int colon = text!.LastIndexOf(':');
        if (colon <= 0 || colon == text.Length - 1)
        {
            error = $"'{text}' is not of the form Event[@Role][#Occurrence]:Crash|Hang.";
            return false;
        }

        string actionText = text.Substring(colon + 1).Trim();
        if (!TryParseName(actionText, out NodeFaultAction action))
        {
            error = $"'{actionText}' is not a fault action (Crash, Hang).";
            return false;
        }

        string point = text.Substring(0, colon);
        int occurrence = 1;
        int hash = point.IndexOf('#');
        if (hash >= 0)
        {
            string occurrenceText = point.Substring(hash + 1).Trim();
            if (!int.TryParse(occurrenceText, NumberStyles.None, CultureInfo.InvariantCulture, out occurrence) || occurrence < 1)
            {
                error = $"'{occurrenceText}' is not a positive occurrence number.";
                return false;
            }

            point = point.Substring(0, hash);
        }

        NodeJournalKind role = NodeJournalKind.None;
        int at = point.IndexOf('@');
        if (at >= 0)
        {
            string roleText = point.Substring(at + 1).Trim();
            if (!TryParseName(roleText, out role) || role == NodeJournalKind.None)
            {
                error = $"'{roleText}' is not a node role.";
                return false;
            }

            point = point.Substring(0, at);
        }

        point = point.Trim();
        if (!TryParseName(point, out NodeJournalEvent evt) || evt == NodeJournalEvent.None || evt == NodeJournalEvent.FaultInjected)
        {
            error = $"'{point}' is not a journal event that can be faulted.";
            return false;
        }

        spec = new NodeFaultSpec(evt, role, occurrence, action);
        return true;
    }

    /// <summary>
    /// Parses a <c>;</c>-separated list, ignoring invalid entries (a node must not fail because of a bad test setting).
    /// </summary>
    public static NodeFaultSpec[] ParseList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var result = new List<NodeFaultSpec>();
        foreach (string part in text!.Split([';'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParse(part, out NodeFaultSpec spec, out _))
            {
                result.Add(spec);
            }
        }

        return result.ToArray();
    }

    private static bool TryParseName<TEnum>(string text, out TEnum value)
        where TEnum : struct
    {
        // Enum.TryParse also accepts numbers; only accept names so that typos are not silently misread.
        if (text.Length == 0 || char.IsDigit(text[0]) || text[0] == '-')
        {
            value = default;
            return false;
        }

        return Enum.TryParse(text, ignoreCase: true, out value);
    }
}
