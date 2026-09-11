// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Build.Framework;

/// <summary>
/// Describes why a candidate assembly did not resolve a reference.
/// </summary>
public enum AssemblyResolutionSearchResult
{
    /// <summary>
    /// The reason is unknown.
    /// </summary>
    Unknown,

    /// <summary>
    /// The candidate file did not exist.
    /// </summary>
    FileNotFound,

    /// <summary>
    /// The candidate assembly identity did not match the requested identity.
    /// </summary>
    FusionNamesDidNotMatch,

    /// <summary>
    /// The candidate file did not have an assembly identity.
    /// </summary>
    TargetHadNoFusionName,

    /// <summary>
    /// The candidate assembly was not in the global assembly cache.
    /// </summary>
    NotInGac,

    /// <summary>
    /// The candidate could not be treated as a file on disk.
    /// </summary>
    NotAFileNameOnDisk,

    /// <summary>
    /// The candidate processor architecture did not match the targeted architecture.
    /// </summary>
    ProcessorArchitectureDoesNotMatch,
}

[Flags]
internal enum AssemblyResolutionSearchTraceFormat
{
    None = 0,
    SearchPath = 1 << 0,
    SearchPathAddedByParentAssembly = 1 << 1,
    SearchedAssemblyFoldersEx = 1 << 2,
    FileNotFound = 1 << 3,
    FusionNamesDidNotMatch = 1 << 4,
    TargetHadNoFusionName = 1 << 5,
    NotInGac = 1 << 6,
    NotAFileNameOnDisk = 1 << 7,
    ProcessorArchitectureDoesNotMatch = 1 << 8,
}

[Flags]
internal enum AssemblyResolutionSearchAttemptContext
{
    None = 0,
    SearchPathUnchanged = 1 << 0,
    ParentAssemblyUnchanged = 1 << 1,
    AssemblyFoldersExUnchanged = 1 << 2,
}

/// <summary>
/// Describes one candidate considered while resolving an assembly reference.
/// </summary>
[Serializable]
public sealed class AssemblyResolutionSearchAttempt
{
    internal AssemblyResolutionSearchAttempt(
        string? fileNameAttempted,
        string? searchPath,
        string? parentAssembly,
        string? assemblyName,
        AssemblyResolutionSearchResult result,
        string? processorArchitecture,
        bool logAssemblyFoldersEx)
    {
        FileNameAttempted = fileNameAttempted;
        SearchPath = searchPath;
        ParentAssembly = parentAssembly;
        AssemblyName = assemblyName;
        Result = result;
        ProcessorArchitecture = processorArchitecture;
        IsAssemblyFoldersExSearch = logAssemblyFoldersEx;
    }

    /// <summary>
    /// Gets the candidate file name.
    /// </summary>
    public string? FileNameAttempted { get; }

    /// <summary>
    /// Gets the search path that produced the candidate.
    /// </summary>
    public string? SearchPath { get; }

    /// <summary>
    /// Gets the parent assembly that contributed the search path.
    /// </summary>
    public string? ParentAssembly { get; }

    /// <summary>
    /// Gets the identity read from the candidate assembly when its fusion name did not match.
    /// </summary>
    public string? AssemblyName { get; }

    /// <summary>
    /// Gets the result of considering the candidate.
    /// </summary>
    public AssemblyResolutionSearchResult Result { get; }

    /// <summary>
    /// Gets the candidate processor architecture, when relevant.
    /// </summary>
    public string? ProcessorArchitecture { get; }

    /// <summary>
    /// Gets whether this attempt represents a summarized AssemblyFoldersEx search.
    /// </summary>
    public bool IsAssemblyFoldersExSearch { get; }

    internal AssemblyResolutionSearchAttemptContext GetUnchangedContext(AssemblyResolutionSearchAttempt? previous)
    {
        AssemblyResolutionSearchAttemptContext unchangedContext = AssemblyResolutionSearchAttemptContext.None;
        if (string.Equals(SearchPath, previous?.SearchPath, StringComparison.Ordinal))
        {
            unchangedContext |= AssemblyResolutionSearchAttemptContext.SearchPathUnchanged;
        }

        if (string.Equals(ParentAssembly, previous?.ParentAssembly, StringComparison.Ordinal))
        {
            unchangedContext |= AssemblyResolutionSearchAttemptContext.ParentAssemblyUnchanged;
        }

        if (IsAssemblyFoldersExSearch == (previous?.IsAssemblyFoldersExSearch ?? false))
        {
            unchangedContext |= AssemblyResolutionSearchAttemptContext.AssemblyFoldersExUnchanged;
        }

        return unchangedContext;
    }

    internal void WriteToStream(BinaryWriter writer, AssemblyResolutionSearchAttempt? previous)
    {
        AssemblyResolutionSearchAttemptContext unchangedContext = GetUnchangedContext(previous);
        writer.Write((byte)unchangedContext);
        writer.WriteOptionalString(FileNameAttempted);
        if ((unchangedContext & AssemblyResolutionSearchAttemptContext.SearchPathUnchanged) == 0)
        {
            writer.WriteOptionalString(SearchPath);
        }

        if ((unchangedContext & AssemblyResolutionSearchAttemptContext.ParentAssemblyUnchanged) == 0)
        {
            writer.WriteOptionalString(ParentAssembly);
        }

        writer.WriteOptionalString(AssemblyName);
        writer.Write7BitEncodedInt((int)Result);
        writer.WriteOptionalString(ProcessorArchitecture);
        if ((unchangedContext & AssemblyResolutionSearchAttemptContext.AssemblyFoldersExUnchanged) == 0)
        {
            writer.Write(IsAssemblyFoldersExSearch);
        }
    }

    internal static AssemblyResolutionSearchAttempt CreateFromStream(BinaryReader reader, AssemblyResolutionSearchAttempt? previous)
    {
        var unchangedContext = (AssemblyResolutionSearchAttemptContext)reader.ReadByte();
        string? fileNameAttempted = reader.ReadOptionalString();
        string? searchPath = (unchangedContext & AssemblyResolutionSearchAttemptContext.SearchPathUnchanged) != 0
            ? previous?.SearchPath
            : reader.ReadOptionalString();
        string? parentAssembly = (unchangedContext & AssemblyResolutionSearchAttemptContext.ParentAssemblyUnchanged) != 0
            ? previous?.ParentAssembly
            : reader.ReadOptionalString();
        string? assemblyName = reader.ReadOptionalString();
        var result = (AssemblyResolutionSearchResult)reader.Read7BitEncodedInt();
        string? processorArchitecture = reader.ReadOptionalString();
        bool isAssemblyFoldersExSearch = (unchangedContext & AssemblyResolutionSearchAttemptContext.AssemblyFoldersExUnchanged) != 0
            ? previous?.IsAssemblyFoldersExSearch ?? false
            : reader.ReadBoolean();

        return new(
            fileNameAttempted,
            searchPath,
            parentAssembly,
            assemblyName,
            result,
            processorArchitecture,
            isAssemblyFoldersExSearch);
    }
}

/// <summary>
/// Describes all candidates considered while resolving one assembly reference.
/// </summary>
[Serializable]
public sealed class AssemblyResolutionSearchTraceEventArgs : BuildMessageEventArgs
{
    private IReadOnlyList<AssemblyResolutionSearchAttempt> _searchAttempts = [];
    private AssemblyResolutionSearchTraceMessageFormats? _messageFormats;
    private string? _formattedMessage;

    internal AssemblyResolutionSearchTraceEventArgs()
    {
    }

    internal AssemblyResolutionSearchTraceEventArgs(
        string requestedAssemblyName,
        string? targetProcessorArchitecture,
        IReadOnlyList<AssemblyResolutionSearchAttempt> searchAttempts,
        AssemblyResolutionSearchTraceMessageFormats messageFormats,
        string senderName,
        MessageImportance importance,
        DateTime eventTimestamp)
        : base(message: null, helpKeyword: null, senderName, importance, eventTimestamp)
    {
        RequestedAssemblyName = requestedAssemblyName;
        TargetProcessorArchitecture = targetProcessorArchitecture;
        _searchAttempts = searchAttempts;
        _messageFormats = messageFormats;
    }

    /// <summary>
    /// Gets the assembly identity being resolved.
    /// </summary>
    public string RequestedAssemblyName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the targeted processor architecture.
    /// </summary>
    public string? TargetProcessorArchitecture { get; private set; }

    /// <summary>
    /// Gets the candidates considered while resolving this reference.
    /// </summary>
    public IReadOnlyList<AssemblyResolutionSearchAttempt> SearchAttempts => _searchAttempts;

    /// <inheritdoc />
    public override string? Message
        => _messageFormats is null ? base.Message : (_formattedMessage ??= FormatMessage());

    internal AssemblyResolutionSearchTraceMessageFormats? MessageFormats => _messageFormats;

    internal override void WriteToStream(BinaryWriter writer)
    {
        // Message is reconstructed from the structured fields on the receiving node.
        base.WriteToStream(writer);
        writer.Write(RequestedAssemblyName);
        writer.WriteOptionalString(TargetProcessorArchitecture);
        AssemblyResolutionSearchTraceFormat usedFormats = GetUsedFormats();
        writer.Write7BitEncodedInt((int)usedFormats);
        _messageFormats!.WriteToStream(writer, usedFormats);
        writer.Write7BitEncodedInt(_searchAttempts.Count);
        AssemblyResolutionSearchAttempt? previous = null;
        for (int i = 0; i < _searchAttempts.Count; i++)
        {
            AssemblyResolutionSearchAttempt attempt = _searchAttempts[i];
            attempt.WriteToStream(writer, previous);
            previous = attempt;
        }
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        RequestedAssemblyName = reader.ReadString();
        TargetProcessorArchitecture = reader.ReadOptionalString();
        var usedFormats = (AssemblyResolutionSearchTraceFormat)reader.Read7BitEncodedInt();
        _messageFormats = AssemblyResolutionSearchTraceMessageFormats.CreateFromStream(reader, usedFormats);

        int count = reader.Read7BitEncodedInt();
        var attempts = new AssemblyResolutionSearchAttempt[count];
        AssemblyResolutionSearchAttempt? previous = null;
        for (int i = 0; i < count; i++)
        {
            AssemblyResolutionSearchAttempt attempt = AssemblyResolutionSearchAttempt.CreateFromStream(reader, previous);
            attempts[i] = attempt;
            previous = attempt;
        }

        _searchAttempts = attempts;
    }

    internal AssemblyResolutionSearchTraceFormat GetUsedFormats()
    {
        AssemblyResolutionSearchTraceFormat usedFormats = AssemblyResolutionSearchTraceFormat.None;
        string? lastSearchPath = null;

        for (int i = 0; i < _searchAttempts.Count; i++)
        {
            AssemblyResolutionSearchAttempt attempt = _searchAttempts[i];
            if (!string.Equals(lastSearchPath, attempt.SearchPath, StringComparison.Ordinal))
            {
                lastSearchPath = attempt.SearchPath;
                usedFormats |= attempt.ParentAssembly is null
                    ? AssemblyResolutionSearchTraceFormat.SearchPath
                    : AssemblyResolutionSearchTraceFormat.SearchPathAddedByParentAssembly;

                if (attempt.IsAssemblyFoldersExSearch)
                {
                    usedFormats |= AssemblyResolutionSearchTraceFormat.SearchedAssemblyFoldersEx;
                }
            }

            usedFormats |= attempt.Result switch
            {
                AssemblyResolutionSearchResult.FileNotFound when !attempt.IsAssemblyFoldersExSearch
                    => AssemblyResolutionSearchTraceFormat.FileNotFound,
                AssemblyResolutionSearchResult.FusionNamesDidNotMatch
                    => AssemblyResolutionSearchTraceFormat.FusionNamesDidNotMatch,
                AssemblyResolutionSearchResult.TargetHadNoFusionName
                    => AssemblyResolutionSearchTraceFormat.TargetHadNoFusionName,
                AssemblyResolutionSearchResult.NotInGac
                    => AssemblyResolutionSearchTraceFormat.NotInGac,
                AssemblyResolutionSearchResult.NotAFileNameOnDisk when !attempt.IsAssemblyFoldersExSearch
                    => AssemblyResolutionSearchTraceFormat.NotAFileNameOnDisk,
                AssemblyResolutionSearchResult.ProcessorArchitectureDoesNotMatch
                    => AssemblyResolutionSearchTraceFormat.ProcessorArchitectureDoesNotMatch,
                _ => AssemblyResolutionSearchTraceFormat.None,
            };
        }

        return usedFormats;
    }

    private string FormatMessage()
    {
        var builder = new StringBuilder();
        AssemblyResolutionSearchTraceMessageFormats formats = _messageFormats!;
        string? lastSearchPath = null;

        for (int i = 0; i < _searchAttempts.Count; i++)
        {
            AssemblyResolutionSearchAttempt attempt = _searchAttempts[i];
            if (!string.Equals(lastSearchPath, attempt.SearchPath, StringComparison.Ordinal))
            {
                lastSearchPath = attempt.SearchPath;
                AppendMessage(
                    builder,
                    attempt.ParentAssembly is null
                        ? Format(formats.SearchPath, attempt.SearchPath)
                        : Format(formats.SearchPathAddedByParentAssembly, attempt.SearchPath, attempt.ParentAssembly));

                if (attempt.IsAssemblyFoldersExSearch)
                {
                    AppendMessage(builder, formats.SearchedAssemblyFoldersEx);
                }
            }

            string? message = attempt.Result switch
            {
                AssemblyResolutionSearchResult.FileNotFound when !attempt.IsAssemblyFoldersExSearch
                    => Format(formats.FileNotFound, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.FusionNamesDidNotMatch
                    => Format(formats.FusionNamesDidNotMatch, attempt.FileNameAttempted, attempt.AssemblyName, RequestedAssemblyName),
                AssemblyResolutionSearchResult.TargetHadNoFusionName
                    => Format(formats.TargetHadNoFusionName, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.NotInGac
                    => Format(formats.NotInGac, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.NotAFileNameOnDisk when !attempt.IsAssemblyFoldersExSearch
                    => Format(formats.NotAFileNameOnDisk, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.ProcessorArchitectureDoesNotMatch
                    => Format(formats.ProcessorArchitectureDoesNotMatch, attempt.FileNameAttempted, attempt.ProcessorArchitecture, TargetProcessorArchitecture),
                _ => null,
            };

            if (message is not null)
            {
                AppendMessage(builder, message);
            }
        }

        return builder.ToString();
    }

    private static string Format(string format, params object?[] arguments)
        => string.Format(CultureInfo.CurrentCulture, format, arguments);

    private static void AppendMessage(StringBuilder builder, string message)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(message);
    }
}

[Serializable]
internal sealed class AssemblyResolutionSearchTraceMessageFormats
{
    internal AssemblyResolutionSearchTraceMessageFormats(
        string searchPath,
        string searchPathAddedByParentAssembly,
        string searchedAssemblyFoldersEx,
        string fileNotFound,
        string fusionNamesDidNotMatch,
        string targetHadNoFusionName,
        string notInGac,
        string notAFileNameOnDisk,
        string processorArchitectureDoesNotMatch)
    {
        SearchPath = searchPath;
        SearchPathAddedByParentAssembly = searchPathAddedByParentAssembly;
        SearchedAssemblyFoldersEx = searchedAssemblyFoldersEx;
        FileNotFound = fileNotFound;
        FusionNamesDidNotMatch = fusionNamesDidNotMatch;
        TargetHadNoFusionName = targetHadNoFusionName;
        NotInGac = notInGac;
        NotAFileNameOnDisk = notAFileNameOnDisk;
        ProcessorArchitectureDoesNotMatch = processorArchitectureDoesNotMatch;
    }

    internal string SearchPath { get; }
    internal string SearchPathAddedByParentAssembly { get; }
    internal string SearchedAssemblyFoldersEx { get; }
    internal string FileNotFound { get; }
    internal string FusionNamesDidNotMatch { get; }
    internal string TargetHadNoFusionName { get; }
    internal string NotInGac { get; }
    internal string NotAFileNameOnDisk { get; }
    internal string ProcessorArchitectureDoesNotMatch { get; }

    internal void WriteToStream(BinaryWriter writer, AssemblyResolutionSearchTraceFormat usedFormats)
    {
        WriteIfUsed(writer, usedFormats, AssemblyResolutionSearchTraceFormat.SearchPath, SearchPath);
        WriteIfUsed(writer, usedFormats, AssemblyResolutionSearchTraceFormat.SearchPathAddedByParentAssembly, SearchPathAddedByParentAssembly);
        WriteIfUsed(writer, usedFormats, AssemblyResolutionSearchTraceFormat.SearchedAssemblyFoldersEx, SearchedAssemblyFoldersEx);
        WriteIfUsed(writer, usedFormats, AssemblyResolutionSearchTraceFormat.FileNotFound, FileNotFound);
        WriteIfUsed(writer, usedFormats, AssemblyResolutionSearchTraceFormat.FusionNamesDidNotMatch, FusionNamesDidNotMatch);
        WriteIfUsed(writer, usedFormats, AssemblyResolutionSearchTraceFormat.TargetHadNoFusionName, TargetHadNoFusionName);
        WriteIfUsed(writer, usedFormats, AssemblyResolutionSearchTraceFormat.NotInGac, NotInGac);
        WriteIfUsed(writer, usedFormats, AssemblyResolutionSearchTraceFormat.NotAFileNameOnDisk, NotAFileNameOnDisk);
        WriteIfUsed(writer, usedFormats, AssemblyResolutionSearchTraceFormat.ProcessorArchitectureDoesNotMatch, ProcessorArchitectureDoesNotMatch);
    }

    internal static AssemblyResolutionSearchTraceMessageFormats CreateFromStream(BinaryReader reader, AssemblyResolutionSearchTraceFormat usedFormats)
        => new(
            ReadIfUsed(reader, usedFormats, AssemblyResolutionSearchTraceFormat.SearchPath),
            ReadIfUsed(reader, usedFormats, AssemblyResolutionSearchTraceFormat.SearchPathAddedByParentAssembly),
            ReadIfUsed(reader, usedFormats, AssemblyResolutionSearchTraceFormat.SearchedAssemblyFoldersEx),
            ReadIfUsed(reader, usedFormats, AssemblyResolutionSearchTraceFormat.FileNotFound),
            ReadIfUsed(reader, usedFormats, AssemblyResolutionSearchTraceFormat.FusionNamesDidNotMatch),
            ReadIfUsed(reader, usedFormats, AssemblyResolutionSearchTraceFormat.TargetHadNoFusionName),
            ReadIfUsed(reader, usedFormats, AssemblyResolutionSearchTraceFormat.NotInGac),
            ReadIfUsed(reader, usedFormats, AssemblyResolutionSearchTraceFormat.NotAFileNameOnDisk),
            ReadIfUsed(reader, usedFormats, AssemblyResolutionSearchTraceFormat.ProcessorArchitectureDoesNotMatch));

    private static void WriteIfUsed(
        BinaryWriter writer,
        AssemblyResolutionSearchTraceFormat usedFormats,
        AssemblyResolutionSearchTraceFormat format,
        string value)
    {
        if ((usedFormats & format) != 0)
        {
            writer.Write(value);
        }
    }

    private static string ReadIfUsed(
        BinaryReader reader,
        AssemblyResolutionSearchTraceFormat usedFormats,
        AssemblyResolutionSearchTraceFormat format)
        => (usedFormats & format) != 0 ? reader.ReadString() : string.Empty;
}
