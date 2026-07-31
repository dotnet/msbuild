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
    /// Gets the identity read from the candidate assembly, when available.
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

    internal void WriteToStream(BinaryWriter writer)
    {
        writer.WriteOptionalString(FileNameAttempted);
        writer.WriteOptionalString(SearchPath);
        writer.WriteOptionalString(ParentAssembly);
        writer.WriteOptionalString(AssemblyName);
        writer.Write7BitEncodedInt((int)Result);
        writer.WriteOptionalString(ProcessorArchitecture);
        writer.Write(IsAssemblyFoldersExSearch);
    }

    internal static AssemblyResolutionSearchAttempt CreateFromStream(BinaryReader reader)
        => new(
            reader.ReadOptionalString(),
            reader.ReadOptionalString(),
            reader.ReadOptionalString(),
            reader.ReadOptionalString(),
            (AssemblyResolutionSearchResult)reader.Read7BitEncodedInt(),
            reader.ReadOptionalString(),
            reader.ReadBoolean());
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
    {
        get
        {
            if (_formattedMessage is null && _messageFormats is not null)
            {
                _formattedMessage = FormatMessage();
            }

            return _formattedMessage ?? base.Message;
        }
    }

    internal AssemblyResolutionSearchTraceMessageFormats? MessageFormats => _messageFormats;

    internal override void WriteToStream(BinaryWriter writer)
    {
        // Message is reconstructed from the structured fields on the receiving node.
        base.WriteToStream(writer);
        writer.Write(RequestedAssemblyName);
        writer.WriteOptionalString(TargetProcessorArchitecture);
        _messageFormats!.WriteToStream(writer);
        writer.Write7BitEncodedInt(_searchAttempts.Count);
        for (int i = 0; i < _searchAttempts.Count; i++)
        {
            _searchAttempts[i].WriteToStream(writer);
        }
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        RequestedAssemblyName = reader.ReadString();
        TargetProcessorArchitecture = reader.ReadOptionalString();
        _messageFormats = AssemblyResolutionSearchTraceMessageFormats.CreateFromStream(reader);

        int count = reader.Read7BitEncodedInt();
        var attempts = new AssemblyResolutionSearchAttempt[count];
        for (int i = 0; i < count; i++)
        {
            attempts[i] = AssemblyResolutionSearchAttempt.CreateFromStream(reader);
        }

        _searchAttempts = attempts;
    }

    private string FormatMessage()
    {
        var builder = new StringBuilder();
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
                        ? Format(_messageFormats!.SearchPath, attempt.SearchPath)
                        : Format(_messageFormats!.SearchPathAddedByParentAssembly, attempt.SearchPath, attempt.ParentAssembly));

                if (attempt.IsAssemblyFoldersExSearch)
                {
                    AppendMessage(builder, _messageFormats.SearchedAssemblyFoldersEx);
                }
            }

            string? message = attempt.Result switch
            {
                AssemblyResolutionSearchResult.FileNotFound when !attempt.IsAssemblyFoldersExSearch
                    => Format(_messageFormats!.FileNotFound, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.FusionNamesDidNotMatch
                    => Format(_messageFormats!.FusionNamesDidNotMatch, attempt.FileNameAttempted, attempt.AssemblyName, RequestedAssemblyName),
                AssemblyResolutionSearchResult.TargetHadNoFusionName
                    => Format(_messageFormats!.TargetHadNoFusionName, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.NotInGac
                    => Format(_messageFormats!.NotInGac, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.NotAFileNameOnDisk when !attempt.IsAssemblyFoldersExSearch
                    => Format(_messageFormats!.NotAFileNameOnDisk, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.ProcessorArchitectureDoesNotMatch
                    => Format(_messageFormats!.ProcessorArchitectureDoesNotMatch, attempt.FileNameAttempted, attempt.ProcessorArchitecture, TargetProcessorArchitecture),
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

    internal void WriteToStream(BinaryWriter writer)
    {
        writer.Write(SearchPath);
        writer.Write(SearchPathAddedByParentAssembly);
        writer.Write(SearchedAssemblyFoldersEx);
        writer.Write(FileNotFound);
        writer.Write(FusionNamesDidNotMatch);
        writer.Write(TargetHadNoFusionName);
        writer.Write(NotInGac);
        writer.Write(NotAFileNameOnDisk);
        writer.Write(ProcessorArchitectureDoesNotMatch);
    }

    internal static AssemblyResolutionSearchTraceMessageFormats CreateFromStream(BinaryReader reader)
        => new(
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadString(),
            reader.ReadString());
}
