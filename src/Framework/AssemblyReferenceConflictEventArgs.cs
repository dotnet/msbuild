// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Microsoft.Build.Framework;

/// <summary>
/// Describes why a reference lost a conflict with another reference of the same simple name.
/// </summary>
public enum AssemblyConflictLossReason
{
    /// <summary>
    /// The reference did not lose a conflict. This value is never expected to appear on a
    /// logged conflict event; it exists only to mirror the internal "no conflict" state.
    /// </summary>
    DidNotLose,

    /// <summary>
    /// The reference matched another assembly that had a higher version number.
    /// </summary>
    HadLowerVersion,

    /// <summary>
    /// The two assemblies cannot be reconciled.
    /// </summary>
    InsolubleConflict,

    /// <summary>
    /// This reference was a dependency and the other reference was primary (specified directly in the project file).
    /// </summary>
    WasNotPrimary,

    /// <summary>
    /// The two references were equivalent according to fusion and also have the same version.
    /// </summary>
    FusionEquivalentWithSameVersion,
}

/// <summary>
/// Describes one reference (a project item, or a file on disk) that required a dependee reference, along with the
/// project items that pulled the dependee in.
/// </summary>
[Serializable]
public sealed class AssemblyConflictDependee
{
    internal AssemblyConflictDependee(string dependeeFullPath, IReadOnlyList<string> sourceItemSpecs)
    {
        DependeeFullPath = dependeeFullPath;
        SourceItemSpecs = sourceItemSpecs;
    }

    /// <summary>
    /// Gets the full path of the reference that depended on the conflicting assembly.
    /// </summary>
    public string DependeeFullPath { get; }

    /// <summary>
    /// Gets the item specs of the project items that caused <see cref="DependeeFullPath"/> to be resolved.
    /// </summary>
    public IReadOnlyList<string> SourceItemSpecs { get; }

    internal void WriteToStream(BinaryWriter writer)
    {
        writer.WriteOptionalString(DependeeFullPath);
        writer.Write7BitEncodedInt(SourceItemSpecs.Count);
        for (int i = 0; i < SourceItemSpecs.Count; i++)
        {
            writer.WriteOptionalString(SourceItemSpecs[i]);
        }
    }

    internal static AssemblyConflictDependee CreateFromStream(BinaryReader reader)
    {
        string? dependeeFullPath = reader.ReadOptionalString();
        int count = reader.Read7BitEncodedInt();
        var sourceItemSpecs = new string[count];
        for (int i = 0; i < count; i++)
        {
            sourceItemSpecs[i] = reader.ReadOptionalString() ?? string.Empty;
        }

        return new AssemblyConflictDependee(dependeeFullPath ?? string.Empty, sourceItemSpecs);
    }
}

/// <summary>
/// Describes one side (the victor or the victim) of an assembly conflict, including the references and project
/// items that led to it being resolved.
/// </summary>
[Serializable]
public sealed class AssemblyConflictReferenceDetails
{
    internal AssemblyConflictReferenceDetails(
        string fusionName,
        string? fullPath,
        bool useUnifiedHeader,
        bool isPrimary,
        bool isResolved,
        string? unresolvedPrimaryItemSpec,
        IReadOnlyList<AssemblyConflictDependee> dependees)
    {
        FusionName = fusionName;
        FullPath = fullPath;
        UseUnifiedHeader = useUnifiedHeader;
        IsPrimary = isPrimary;
        IsResolved = isResolved;
        UnresolvedPrimaryItemSpec = unresolvedPrimaryItemSpec;
        Dependees = dependees;
    }

    /// <summary>
    /// Gets the display identity (fusion name) of this reference.
    /// </summary>
    public string FusionName { get; }

    /// <summary>
    /// Gets the resolved full path of this reference, when known.
    /// </summary>
    public string? FullPath { get; }

    /// <summary>
    /// Gets whether the "unified" wording should be used when rendering the header for this reference
    /// (used for the reference that lost the conflict, which may have been unified to the victor).
    /// </summary>
    public bool UseUnifiedHeader { get; }

    /// <summary>
    /// Gets whether this reference is a primary reference (directly specified in the project file).
    /// </summary>
    public bool IsPrimary { get; }

    /// <summary>
    /// Gets whether this reference was resolved to a file on disk.
    /// </summary>
    public bool IsResolved { get; }

    /// <summary>
    /// Gets the item spec of the unresolved primary source item, when <see cref="IsPrimary"/> is <see langword="true"/>
    /// and <see cref="IsResolved"/> is <see langword="false"/>. Otherwise <see langword="null"/>.
    /// </summary>
    public string? UnresolvedPrimaryItemSpec { get; }

    /// <summary>
    /// Gets the dependee references (and the project items that pulled each one in) that required this reference.
    /// When <see cref="IsPrimary"/> and <see cref="IsResolved"/> are both <see langword="true"/>, the first entry
    /// describes this reference's own project items, matching legacy rendering.
    /// </summary>
    public IReadOnlyList<AssemblyConflictDependee> Dependees { get; }

    internal void WriteToStream(BinaryWriter writer)
    {
        writer.WriteOptionalString(FusionName);
        writer.WriteOptionalString(FullPath);
        writer.Write(UseUnifiedHeader);
        writer.Write(IsPrimary);
        writer.Write(IsResolved);
        writer.WriteOptionalString(UnresolvedPrimaryItemSpec);
        writer.Write7BitEncodedInt(Dependees.Count);
        for (int i = 0; i < Dependees.Count; i++)
        {
            Dependees[i].WriteToStream(writer);
        }
    }

    internal static AssemblyConflictReferenceDetails CreateFromStream(BinaryReader reader)
    {
        string? fusionName = reader.ReadOptionalString();
        string? fullPath = reader.ReadOptionalString();
        bool useUnifiedHeader = reader.ReadBoolean();
        bool isPrimary = reader.ReadBoolean();
        bool isResolved = reader.ReadBoolean();
        string? unresolvedPrimaryItemSpec = reader.ReadOptionalString();

        int count = reader.Read7BitEncodedInt();
        var dependees = new AssemblyConflictDependee[count];
        for (int i = 0; i < count; i++)
        {
            dependees[i] = AssemblyConflictDependee.CreateFromStream(reader);
        }

        return new AssemblyConflictReferenceDetails(
            fusionName ?? string.Empty,
            fullPath,
            useUnifiedHeader,
            isPrimary,
            isResolved,
            unresolvedPrimaryItemSpec,
            dependees);
    }
}

/// <summary>
/// Invariant message templates used to reconstruct assembly conflict messages lazily, regardless of the
/// culture active when the event is later read back (for example, from a binary log).
/// </summary>
[Serializable]
internal sealed class AssemblyConflictMessageFormats
{
    internal AssemblyConflictMessageFormats(
        string conflictFound,
        string conflictHigherVersionChosen,
        string conflictPrimaryChosen,
        string conflictUnsolvable,
        string referenceDependsOn,
        string unifiedReferenceDependsOn,
        string unresolvedPrimaryItemSpec,
        string primarySourceItemsForReference,
        string foundConflicts)
    {
        ConflictFound = conflictFound;
        ConflictHigherVersionChosen = conflictHigherVersionChosen;
        ConflictPrimaryChosen = conflictPrimaryChosen;
        ConflictUnsolvable = conflictUnsolvable;
        ReferenceDependsOn = referenceDependsOn;
        UnifiedReferenceDependsOn = unifiedReferenceDependsOn;
        UnresolvedPrimaryItemSpec = unresolvedPrimaryItemSpec;
        PrimarySourceItemsForReference = primarySourceItemsForReference;
        FoundConflicts = foundConflicts;
    }

    internal string ConflictFound { get; }
    internal string ConflictHigherVersionChosen { get; }
    internal string ConflictPrimaryChosen { get; }
    internal string ConflictUnsolvable { get; }
    internal string ReferenceDependsOn { get; }
    internal string UnifiedReferenceDependsOn { get; }
    internal string UnresolvedPrimaryItemSpec { get; }
    internal string PrimarySourceItemsForReference { get; }
    internal string FoundConflicts { get; }

    internal void WriteToStream(BinaryWriter writer)
    {
        writer.Write(ConflictFound);
        writer.Write(ConflictHigherVersionChosen);
        writer.Write(ConflictPrimaryChosen);
        writer.Write(ConflictUnsolvable);
        writer.Write(ReferenceDependsOn);
        writer.Write(UnifiedReferenceDependsOn);
        writer.Write(UnresolvedPrimaryItemSpec);
        writer.Write(PrimarySourceItemsForReference);
        writer.Write(FoundConflicts);
    }

    internal static AssemblyConflictMessageFormats CreateFromStream(BinaryReader reader)
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

/// <summary>
/// Shared formatting logic for rendering <see cref="AssemblyConflictReferenceDetails"/> and the overall conflict
/// message, used by both <see cref="AssemblyConflictDependencyDetailsMessageEventArgs"/> and
/// <see cref="AssemblyConflictWarningEventArgs"/> so that their rendered text matches exactly.
/// </summary>
internal static class AssemblyConflictMessageFormatter
{
    private const string FourSpaces = "    ";
    private const string EightSpaces = "        ";
    private const string TenSpaces = "          ";
    private const string TwelveSpaces = "            ";

    internal static string FormatDependencyDetails(
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        AssemblyConflictMessageFormats formats)
    {
        var log = new StringBuilder();
        AppendReferenceDetails(log, victor, formats);
        log.AppendLine();
        AppendReferenceDetails(log, victim, formats);
        return log.ToString();
    }

    internal static string FormatWarningMessage(
        string simpleAssemblyName,
        string victorFusionName,
        string victimFusionName,
        AssemblyConflictLossReason lossReason,
        bool victimIsPrimary,
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        AssemblyConflictMessageFormats formats)
        => Format(
            formats.FoundConflicts,
            simpleAssemblyName,
            FormatWarningBody(victorFusionName, victimFusionName, lossReason, victimIsPrimary, victor, victim, formats));

    /// <summary>
    /// Formats the conflict header and dependency details combined (matching the legacy "output" StringBuilder
    /// contents for the warning path), without the outer "Found conflicts..." (MSB3277) wrapper. This is the value
    /// historically exposed via the <c>logMessage</c> metadata on <c>UnresolvedAssemblyConflicts</c> items; the
    /// wrapper text is only added when rendering the actual warning <see cref="BuildEventArgs.Message"/>.
    /// </summary>
    internal static string FormatWarningBody(
        string victorFusionName,
        string victimFusionName,
        AssemblyConflictLossReason lossReason,
        bool victimIsPrimary,
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        AssemblyConflictMessageFormats formats)
    {
        var log = new StringBuilder();
        AppendHeader(log, victorFusionName, victimFusionName, lossReason, victimIsPrimary, formats);
        log.AppendLine();
        AppendReferenceDetails(log, victor, formats);
        log.AppendLine();
        AppendReferenceDetails(log, victim, formats);
        return log.ToString();
    }

    /// <summary>
    /// Formats just the conflict header (no dependency details), matching the text logged as a standalone
    /// message when the conflict does not also produce a warning.
    /// </summary>
    internal static string FormatHeaderOnly(
        string victorFusionName,
        string victimFusionName,
        AssemblyConflictLossReason lossReason,
        bool victimIsPrimary,
        AssemblyConflictMessageFormats formats)
    {
        var log = new StringBuilder();
        AppendHeader(log, victorFusionName, victimFusionName, lossReason, victimIsPrimary, formats);
        return log.ToString();
    }

    private static void AppendHeader(
        StringBuilder log,
        string victorFusionName,
        string victimFusionName,
        AssemblyConflictLossReason lossReason,
        bool victimIsPrimary,
        AssemblyConflictMessageFormats formats)
    {
        log.Append(Format(formats.ConflictFound, victorFusionName, victimFusionName));
        switch (lossReason)
        {
            case AssemblyConflictLossReason.HadLowerVersion:
                log.AppendLine().Append(FourSpaces).Append(Format(formats.ConflictHigherVersionChosen, victorFusionName));
                break;

            case AssemblyConflictLossReason.WasNotPrimary:
                log.AppendLine().Append(FourSpaces).Append(Format(formats.ConflictPrimaryChosen, victorFusionName, victimFusionName));
                break;

            case AssemblyConflictLossReason.InsolubleConflict:
                // When the victim is primary, a separate immediate warning is logged instead; nothing is appended here,
                // matching legacy behavior.
                if (!victimIsPrimary)
                {
                    log.AppendLine().Append(Format(formats.ConflictUnsolvable, victorFusionName, victimFusionName));
                }

                break;

            case AssemblyConflictLossReason.FusionEquivalentWithSameVersion:
                // No additional text, matching legacy behavior.
                break;

            default:
                break;
        }
    }

    private static void AppendReferenceDetails(StringBuilder log, AssemblyConflictReferenceDetails details, AssemblyConflictMessageFormats formats)
    {
        log.Append(FourSpaces);

        string resource = details.UseUnifiedHeader ? formats.UnifiedReferenceDependsOn : formats.ReferenceDependsOn;
        log.Append(Format(resource, details.FusionName, details.FullPath));

        if (details.IsPrimary && !details.IsResolved)
        {
            log.AppendLine().Append(EightSpaces).Append(Format(formats.UnresolvedPrimaryItemSpec, details.UnresolvedPrimaryItemSpec));
        }

        for (int i = 0; i < details.Dependees.Count; i++)
        {
            AssemblyConflictDependee dependee = details.Dependees[i];
            log.AppendLine().Append(EightSpaces).AppendLine(dependee.DependeeFullPath);
            log.Append(TenSpaces).Append(Format(formats.PrimarySourceItemsForReference, dependee.DependeeFullPath));
            for (int j = 0; j < dependee.SourceItemSpecs.Count; j++)
            {
                log.AppendLine().Append(TwelveSpaces).Append(dependee.SourceItemSpecs[j]);
            }
        }
    }

    private static string Format(string format, params object?[] arguments)
        => string.Format(CultureInfo.CurrentCulture, format, arguments);
}

/// <summary>
/// Reports the dependency chain (the references and project items) that led to an assembly conflict, without any
/// warning-level severity. This corresponds to the "extra information" that RAR logs at low importance whenever a
/// conflict was resolved without producing a warning.
/// </summary>
[Serializable]
public sealed class AssemblyConflictDependencyDetailsMessageEventArgs : BuildMessageEventArgs
{
    private AssemblyConflictMessageFormats? _messageFormats;
    private string? _formattedMessage;

    internal AssemblyConflictDependencyDetailsMessageEventArgs()
    {
    }

    internal AssemblyConflictDependencyDetailsMessageEventArgs(
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        AssemblyConflictMessageFormats messageFormats,
        string senderName,
        MessageImportance importance,
        DateTime eventTimestamp)
        : base(message: null, helpKeyword: null, senderName, importance, eventTimestamp)
    {
        Victor = victor;
        Victim = victim;
        _messageFormats = messageFormats;
    }

    /// <summary>
    /// Gets the details of the reference that won the conflict.
    /// </summary>
    public AssemblyConflictReferenceDetails Victor { get; private set; } = null!;

    /// <summary>
    /// Gets the details of the reference that lost the conflict.
    /// </summary>
    public AssemblyConflictReferenceDetails Victim { get; private set; } = null!;

    /// <inheritdoc />
    public override string? Message
    {
        get
        {
            if (_formattedMessage is null && _messageFormats is not null)
            {
                _formattedMessage = AssemblyConflictMessageFormatter.FormatDependencyDetails(Victor, Victim, _messageFormats);
            }

            return _formattedMessage ?? base.Message;
        }
    }

    internal AssemblyConflictMessageFormats? MessageFormats => _messageFormats;

    internal override void WriteToStream(BinaryWriter writer)
    {
        // Message is reconstructed from the structured fields on the receiving node.
        base.WriteToStream(writer);
        Victor.WriteToStream(writer);
        Victim.WriteToStream(writer);
        _messageFormats!.WriteToStream(writer);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        Victor = AssemblyConflictReferenceDetails.CreateFromStream(reader);
        Victim = AssemblyConflictReferenceDetails.CreateFromStream(reader);
        _messageFormats = AssemblyConflictMessageFormats.CreateFromStream(reader);
    }
}

/// <summary>
/// Reports an unresolved assembly version conflict (MSB3277) with structured details describing the victor, the
/// victim, and the dependency chains that led to each.
/// </summary>
[Serializable]
public sealed class AssemblyConflictWarningEventArgs : BuildWarningEventArgs
{
    private AssemblyConflictMessageFormats? _messageFormats;
    private string? _formattedMessage;

    internal AssemblyConflictWarningEventArgs()
    {
    }

    internal AssemblyConflictWarningEventArgs(
        string simpleAssemblyName,
        string victorFusionName,
        string victimFusionName,
        AssemblyConflictLossReason lossReason,
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        AssemblyConflictMessageFormats messageFormats,
        string code,
        string? file,
        int lineNumber,
        int columnNumber,
        string? helpKeyword,
        string senderName,
        DateTime eventTimestamp)
        : base(
            subcategory: null,
            code: code,
            file: file,
            lineNumber: lineNumber,
            columnNumber: columnNumber,
            endLineNumber: 0,
            endColumnNumber: 0,
            message: null,
            helpKeyword: helpKeyword,
            senderName: senderName,
            eventTimestamp: eventTimestamp)
    {
        SimpleAssemblyName = simpleAssemblyName;
        VictorFusionName = victorFusionName;
        VictimFusionName = victimFusionName;
        LossReason = lossReason;
        Victor = victor;
        Victim = victim;
        _messageFormats = messageFormats;
    }

    /// <summary>
    /// Gets the simple (short) name of the assembly for which conflicting versions were found.
    /// </summary>
    public string SimpleAssemblyName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the fusion name of the reference that won the conflict.
    /// </summary>
    public string VictorFusionName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the fusion name of the reference that lost the conflict.
    /// </summary>
    public string VictimFusionName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the reason the victim lost the conflict.
    /// </summary>
    public AssemblyConflictLossReason LossReason { get; private set; }

    /// <summary>
    /// Gets the details of the reference that won the conflict.
    /// </summary>
    public AssemblyConflictReferenceDetails Victor { get; private set; } = null!;

    /// <summary>
    /// Gets the details of the reference that lost the conflict.
    /// </summary>
    public AssemblyConflictReferenceDetails Victim { get; private set; } = null!;

    /// <inheritdoc />
    public override string? Message
    {
        get
        {
            if (_formattedMessage is null && _messageFormats is not null)
            {
                _formattedMessage = AssemblyConflictMessageFormatter.FormatWarningMessage(
                    SimpleAssemblyName,
                    VictorFusionName,
                    VictimFusionName,
                    LossReason,
                    Victim.IsPrimary,
                    Victor,
                    Victim,
                    _messageFormats);
            }

            return _formattedMessage ?? base.Message;
        }
    }

    internal AssemblyConflictMessageFormats? MessageFormats => _messageFormats;

    internal override void WriteToStream(BinaryWriter writer)
    {
        // Message is reconstructed from the structured fields on the receiving node.
        base.WriteToStream(writer);
        writer.Write(SimpleAssemblyName);
        writer.Write(VictorFusionName);
        writer.Write(VictimFusionName);
        writer.Write7BitEncodedInt((int)LossReason);
        Victor.WriteToStream(writer);
        Victim.WriteToStream(writer);
        _messageFormats!.WriteToStream(writer);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        SimpleAssemblyName = reader.ReadString();
        VictorFusionName = reader.ReadString();
        VictimFusionName = reader.ReadString();
        LossReason = (AssemblyConflictLossReason)reader.Read7BitEncodedInt();
        Victor = AssemblyConflictReferenceDetails.CreateFromStream(reader);
        Victim = AssemblyConflictReferenceDetails.CreateFromStream(reader);
        _messageFormats = AssemblyConflictMessageFormats.CreateFromStream(reader);
    }
}
