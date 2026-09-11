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
/// <remarks>
/// The <see cref="BuildEventArgs.Message"/> is rendered in invariant English.
/// </remarks>
[Serializable]
public sealed class AssemblyResolutionSearchTraceEventArgs : BuildMessageEventArgs
{
    private const string SearchPathFormat = "        For SearchPath \"{0}\".";
    private const string SearchPathAddedByParentAssemblyFormat = "        For SearchPath \"{0}\" (added by referencing assembly \"{1}\").";
    private const string SearchedAssemblyFoldersExMessage = "        Considered AssemblyFoldersEx locations.";
    private const string FileNotFoundFormat = "        Considered \"{0}\", but it didn't exist.";
    private const string TargetHadNoFusionNameFormat = "        Considered \"{0}\", which existed but did not appear to be a valid .NET assembly.";
    private const string NotInGacFormat = "        Considered \"{0}\", which was not found in the GAC.";
    private const string NotAFileNameOnDiskFormat = "        Considered treating \"{0}\" as a file name, but it didn't exist.";
    private const string ProcessorArchitectureDoesNotMatchFormat = "        Considered \"{0}\", which existed but had a processor architecture \"{1}\" which does not match the targeted processor architecture \"{2}\".";

    private static readonly string s_fusionNamesDidNotMatchFormat = $"        Considered \"{{0}}\",{Environment.NewLine}\t\t\tbut its name \"{{1}}\"{Environment.NewLine}\t\t\tdidn't match the expected name \"{{2}}\".";

    private IReadOnlyList<AssemblyResolutionSearchAttempt> _searchAttempts = [];
    private string? _formattedMessage;

    internal AssemblyResolutionSearchTraceEventArgs()
    {
    }

    internal AssemblyResolutionSearchTraceEventArgs(
        string requestedAssemblyName,
        string? targetProcessorArchitecture,
        IReadOnlyList<AssemblyResolutionSearchAttempt> searchAttempts,
        string senderName,
        MessageImportance importance,
        DateTime eventTimestamp)
        : base(message: null, helpKeyword: null, senderName, importance, eventTimestamp)
    {
        RequestedAssemblyName = requestedAssemblyName;
        TargetProcessorArchitecture = targetProcessorArchitecture;
        _searchAttempts = searchAttempts;
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
        => _formattedMessage ??= FormatMessage();

    internal override void WriteToStream(BinaryWriter writer)
    {
        // Message is reconstructed from the structured fields on the receiving node.
        base.WriteToStream(writer);
        writer.Write(RequestedAssemblyName);
        writer.WriteOptionalString(TargetProcessorArchitecture);
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
                        ? Format(SearchPathFormat, attempt.SearchPath)
                        : Format(SearchPathAddedByParentAssemblyFormat, attempt.SearchPath, attempt.ParentAssembly));

                if (attempt.IsAssemblyFoldersExSearch)
                {
                    AppendMessage(builder, SearchedAssemblyFoldersExMessage);
                }
            }

            string? message = attempt.Result switch
            {
                AssemblyResolutionSearchResult.FileNotFound when !attempt.IsAssemblyFoldersExSearch
                    => Format(FileNotFoundFormat, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.FusionNamesDidNotMatch
                    => Format(s_fusionNamesDidNotMatchFormat, attempt.FileNameAttempted, attempt.AssemblyName, RequestedAssemblyName),
                AssemblyResolutionSearchResult.TargetHadNoFusionName
                    => Format(TargetHadNoFusionNameFormat, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.NotInGac
                    => Format(NotInGacFormat, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.NotAFileNameOnDisk when !attempt.IsAssemblyFoldersExSearch
                    => Format(NotAFileNameOnDiskFormat, attempt.FileNameAttempted),
                AssemblyResolutionSearchResult.ProcessorArchitectureDoesNotMatch
                    => Format(ProcessorArchitectureDoesNotMatchFormat, attempt.FileNameAttempted, attempt.ProcessorArchitecture, TargetProcessorArchitecture),
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
        => string.Format(CultureInfo.InvariantCulture, format, arguments);

    private static void AppendMessage(StringBuilder builder, string message)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(message);
    }
}
