// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.BackEnd.Components.Caching
{
    internal sealed class TaskResultCacheEventCollector
    {
        private readonly List<TaskResultCacheEvent> _events = [];

        internal IReadOnlyList<TaskResultCacheEvent> Events => _events;

        internal bool IsSupported { get; private set; } = true;

        internal void Record(BuildMessageEventArgs buildEvent)
        {
            if (!TaskResultCacheEvent.TryCreate(buildEvent, out TaskResultCacheEvent? cacheEvent))
            {
                IsSupported = false;
                return;
            }

            _events.Add(cacheEvent);
        }

        internal void Record(BuildWarningEventArgs buildEvent)
        {
            if (!TaskResultCacheEvent.TryCreate(buildEvent, out TaskResultCacheEvent? cacheEvent))
            {
                IsSupported = false;
                return;
            }

            _events.Add(cacheEvent);
        }

        internal void MarkUnsupported()
        {
            IsSupported = false;
        }
    }

    internal sealed class TaskResultCacheEvent
    {
        private enum EventKind : byte
        {
            Message,
            Warning,
            TaskCommandLine,
        }

        private TaskResultCacheEvent(
            EventKind kind,
            string? subcategory,
            string? code,
            string? file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string? message,
            string? helpKeyword,
            string? senderName,
            string? helpLink,
            MessageImportance importance)
        {
            Kind = kind;
            Subcategory = subcategory;
            Code = code;
            File = file;
            LineNumber = lineNumber;
            ColumnNumber = columnNumber;
            EndLineNumber = endLineNumber;
            EndColumnNumber = endColumnNumber;
            Message = message;
            HelpKeyword = helpKeyword;
            SenderName = senderName;
            HelpLink = helpLink;
            Importance = importance;
        }

        private EventKind Kind { get; }

        private string? Subcategory { get; }

        private string? Code { get; }

        private string? File { get; }

        private int LineNumber { get; }

        private int ColumnNumber { get; }

        private int EndLineNumber { get; }

        private int EndColumnNumber { get; }

        private string? Message { get; }

        private string? HelpKeyword { get; }

        private string? SenderName { get; }

        private string? HelpLink { get; }

        private MessageImportance Importance { get; }

        internal static bool TryCreate(
            BuildMessageEventArgs buildEvent,
            [NotNullWhen(true)] out TaskResultCacheEvent? cacheEvent)
        {
            EventKind kind;
            if (buildEvent.GetType() == typeof(BuildMessageEventArgs))
            {
                kind = EventKind.Message;
            }
            else if (buildEvent.GetType() == typeof(TaskCommandLineEventArgs))
            {
                kind = EventKind.TaskCommandLine;
            }
            else
            {
                cacheEvent = null;
                return false;
            }

            cacheEvent = new TaskResultCacheEvent(
                kind,
                buildEvent.Subcategory,
                buildEvent.Code,
                buildEvent.File,
                buildEvent.LineNumber,
                buildEvent.ColumnNumber,
                buildEvent.EndLineNumber,
                buildEvent.EndColumnNumber,
                buildEvent.Message,
                buildEvent.HelpKeyword,
                buildEvent.SenderName,
                helpLink: null,
                buildEvent.Importance);
            return true;
        }

        internal static bool TryCreate(
            BuildWarningEventArgs buildEvent,
            [NotNullWhen(true)] out TaskResultCacheEvent? cacheEvent)
        {
            if (buildEvent.GetType() != typeof(BuildWarningEventArgs))
            {
                cacheEvent = null;
                return false;
            }

            cacheEvent = new TaskResultCacheEvent(
                EventKind.Warning,
                buildEvent.Subcategory,
                buildEvent.Code,
                buildEvent.File,
                buildEvent.LineNumber,
                buildEvent.ColumnNumber,
                buildEvent.EndLineNumber,
                buildEvent.EndColumnNumber,
                buildEvent.Message,
                buildEvent.HelpKeyword,
                buildEvent.SenderName,
                buildEvent.HelpLink,
                MessageImportance.Normal);
            return true;
        }

        internal void Replay(TaskHost taskHost)
        {
            switch (Kind)
            {
                case EventKind.Message:
                    taskHost.LogMessageEvent(
                        new BuildMessageEventArgs(
                            Subcategory,
                            Code,
                            File,
                            LineNumber,
                            ColumnNumber,
                            EndLineNumber,
                            EndColumnNumber,
                            Message,
                            HelpKeyword,
                            SenderName,
                            Importance));
                    break;

                case EventKind.Warning:
                    taskHost.LogWarningEvent(
                        new BuildWarningEventArgs(
                            Subcategory,
                            Code,
                            File,
                            LineNumber,
                            ColumnNumber,
                            EndLineNumber,
                            EndColumnNumber,
                            Message,
                            HelpKeyword,
                            SenderName,
                            HelpLink,
                            DateTime.UtcNow));
                    break;

                case EventKind.TaskCommandLine:
                    taskHost.LogMessageEvent(
                        new TaskCommandLineEventArgs(
                            Message,
                            SenderName,
                            Importance));
                    break;

                default:
                    throw new InvalidDataException();
            }
        }

        internal void Write(BinaryWriter writer)
        {
            writer.Write((byte)Kind);
            WriteNullableString(writer, Subcategory);
            WriteNullableString(writer, Code);
            WriteNullableString(writer, File);
            writer.Write(LineNumber);
            writer.Write(ColumnNumber);
            writer.Write(EndLineNumber);
            writer.Write(EndColumnNumber);
            WriteNullableString(writer, Message);
            WriteNullableString(writer, HelpKeyword);
            WriteNullableString(writer, SenderName);
            WriteNullableString(writer, HelpLink);
            writer.Write((byte)Importance);
        }

        internal static TaskResultCacheEvent Read(BinaryReader reader)
        {
            EventKind kind = (EventKind)reader.ReadByte();
            if (kind is < EventKind.Message or > EventKind.TaskCommandLine)
            {
                throw new InvalidDataException();
            }

            string? subcategory = ReadNullableString(reader);
            string? code = ReadNullableString(reader);
            string? file = ReadNullableString(reader);
            int lineNumber = reader.ReadInt32();
            int columnNumber = reader.ReadInt32();
            int endLineNumber = reader.ReadInt32();
            int endColumnNumber = reader.ReadInt32();
            string? message = ReadNullableString(reader);
            string? helpKeyword = ReadNullableString(reader);
            string? senderName = ReadNullableString(reader);
            string? helpLink = ReadNullableString(reader);
            MessageImportance importance = (MessageImportance)reader.ReadByte();
            if (importance is < MessageImportance.High or > MessageImportance.Low)
            {
                throw new InvalidDataException();
            }

            return new TaskResultCacheEvent(
                kind,
                subcategory,
                code,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message,
                helpKeyword,
                senderName,
                helpLink,
                importance);
        }

        private static void WriteNullableString(BinaryWriter writer, string? value)
        {
            writer.Write(value is not null);
            if (value is not null)
            {
                writer.Write(value);
            }
        }

        private static string? ReadNullableString(BinaryReader reader)
        {
            return reader.ReadBoolean()
                ? reader.ReadString()
                : null;
        }
    }
}
