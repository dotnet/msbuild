// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Framework;

/// <summary>
/// Describes why a reference lost a conflict with another reference of the same simple name.
/// </summary>
public enum AssemblyConflictLossReason
{
    /// <summary>
    /// The reference did not lose a conflict.
    /// This value represents the internal "no conflict" state and does not occur in logged conflict events.
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
    /// This reference was a dependency. The other reference was a primary reference that the project specified directly.
    /// </summary>
    WasNotPrimary,

    /// <summary>
    /// The two references were equivalent according to fusion and also have the same version.
    /// </summary>
    FusionEquivalentWithSameVersion,
}

/// <summary>
/// Describes a reference that required the conflicting assembly.
/// It also identifies the project items that caused MSBuild to resolve the reference.
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
/// Describes the victor or victim of an assembly conflict.
/// It includes the references and project items that caused assembly resolution.
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
    /// Gets a value that indicates whether the rendered header uses the "unified" text.
    /// The victim uses this text when MSBuild unifies it to the victor.
    /// </summary>
    public bool UseUnifiedHeader { get; }

    /// <summary>
    /// Gets a value that indicates whether the project directly specified this primary reference.
    /// </summary>
    public bool IsPrimary { get; }

    /// <summary>
    /// Gets a value that indicates whether MSBuild resolved this reference to a file.
    /// </summary>
    public bool IsResolved { get; }

    /// <summary>
    /// Gets the escaped include of the unresolved primary source item.
    /// This value matches the text from the item's <c>ToString()</c> method.
    /// The value is available when <see cref="IsPrimary"/> is <see langword="true"/> and <see cref="IsResolved"/> is <see langword="false"/>.
    /// Otherwise, the value is <see langword="null"/>.
    /// </summary>
    public string? UnresolvedPrimaryItemSpec { get; }

    /// <summary>
    /// Gets the dependee references that required this reference.
    /// Each entry also contains the project items that caused MSBuild to resolve the dependee.
    /// If this reference is primary and resolved, the first entry contains this reference's project items.
    /// This first entry preserves the legacy text.
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
/// Contains invariant templates that reconstruct assembly conflict messages only when a reader requests the messages.
/// The templates produce stable text when a reader replays a binary log with a different culture.
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
/// Formats conflict reference details and complete conflict messages.
/// Both structured conflict event types use this class to produce identical text.
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
    /// Formats the conflict header and dependency details without the outer MSB3277 wrapper.
    /// This text matches the legacy warning body.
    /// <c>UnresolvedAssemblyConflicts</c> items use this text for the <c>logMessage</c> metadata.
    /// The warning <see cref="BuildEventArgs.Message"/> adds the outer wrapper.
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
    /// Formats the conflict header without dependency details.
    /// The result matches the standalone message for a conflict that does not produce a warning.
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
                // A primary victim produces a separate warning, so this header must not contain more text.
                if (!victimIsPrimary)
                {
                    log.AppendLine().Append(Format(formats.ConflictUnsolvable, victorFusionName, victimFusionName));
                }

                break;

            case AssemblyConflictLossReason.FusionEquivalentWithSameVersion:
                // Legacy messages do not contain more text for this reason.
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

    private static string Format(string format, object? arg0)
        => MessageFormatter.Format(format, arg0);

    private static string Format(string format, object? arg0, object? arg1)
        => MessageFormatter.Format(format, arg0, arg1);
}

/// <summary>
/// Reports the references and project items that caused an assembly conflict.
/// RAR logs this low-importance event when conflict resolution does not produce a warning.
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

    internal bool IsMessageMaterialized => _formattedMessage is not null;

    internal override void WriteToStream(BinaryWriter writer)
    {
        // The receiving node reconstructs the message from the structured fields.
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
/// Reports an unresolved assembly version conflict (MSB3277).
/// The structured details identify the victor, the victim, and their dependency chains.
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

    internal bool IsMessageMaterialized => _formattedMessage is not null;

    internal override void WriteToStream(BinaryWriter writer)
    {
        // The receiving node reconstructs the message from the structured fields.
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
