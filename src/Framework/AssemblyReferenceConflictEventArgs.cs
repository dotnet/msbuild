// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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
    DidNotLose = 0,

    /// <summary>
    /// The reference matched another assembly that had a higher version number.
    /// </summary>
    HadLowerVersion = 1,

    /// <summary>
    /// The two assemblies cannot be reconciled.
    /// </summary>
    InsolubleConflict = 2,

    /// <summary>
    /// This reference was a dependency. The other reference was a primary reference that the project specified directly.
    /// </summary>
    WasNotPrimary = 3,

    /// <summary>
    /// The two references were equivalent according to fusion and also have the same version.
    /// </summary>
    FusionEquivalentWithSameVersion = 4,
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
        bool isPrimary,
        bool isResolved,
        string? unresolvedPrimaryItemSpec,
        IReadOnlyList<string> primarySourceItemSpecs,
        IReadOnlyList<AssemblyConflictDependee> dependees)
    {
        FusionName = fusionName;
        FullPath = fullPath;
        IsPrimary = isPrimary;
        IsResolved = isResolved;
        UnresolvedPrimaryItemSpec = unresolvedPrimaryItemSpec;
        PrimarySourceItemSpecs = primarySourceItemSpecs;
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
    /// Gets the project items that caused MSBuild to resolve this primary reference.
    /// </summary>
    public IReadOnlyList<string> PrimarySourceItemSpecs { get; }

    /// <summary>
    /// Gets the dependee references that required this reference.
    /// Each entry also contains the project items that caused MSBuild to resolve the dependee.
    /// </summary>
    public IReadOnlyList<AssemblyConflictDependee> Dependees { get; }

    internal void WriteToStream(BinaryWriter writer)
    {
        writer.WriteOptionalString(FusionName);
        writer.WriteOptionalString(FullPath);
        writer.Write(IsPrimary);
        writer.Write(IsResolved);
        writer.WriteOptionalString(UnresolvedPrimaryItemSpec);
        writer.Write7BitEncodedInt(PrimarySourceItemSpecs.Count);
        for (int i = 0; i < PrimarySourceItemSpecs.Count; i++)
        {
            writer.WriteOptionalString(PrimarySourceItemSpecs[i]);
        }

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
        bool isPrimary = reader.ReadBoolean();
        bool isResolved = reader.ReadBoolean();
        string? unresolvedPrimaryItemSpec = reader.ReadOptionalString();

        int primarySourceItemSpecCount = reader.Read7BitEncodedInt();
        var primarySourceItemSpecs = new string[primarySourceItemSpecCount];
        for (int i = 0; i < primarySourceItemSpecCount; i++)
        {
            primarySourceItemSpecs[i] = reader.ReadOptionalString() ?? string.Empty;
        }

        int dependeeCount = reader.Read7BitEncodedInt();
        var dependees = new AssemblyConflictDependee[dependeeCount];
        for (int i = 0; i < dependeeCount; i++)
        {
            dependees[i] = AssemblyConflictDependee.CreateFromStream(reader);
        }

        return new AssemblyConflictReferenceDetails(
            fusionName ?? string.Empty,
            fullPath,
            isPrimary,
            isResolved,
            unresolvedPrimaryItemSpec,
            primarySourceItemSpecs,
            dependees);
    }
}

/// <summary>
/// Formats conflict reference details and complete conflict messages.
/// Both structured conflict event types use this class to produce identical text.
/// </summary>
internal static class AssemblyConflictMessageFormatter
{
    private static readonly ConditionalWeakTable<CultureInfo, MessageFormats> s_formatsByCulture = new();
    private const string FourSpaces = "    ";
    private const string EightSpaces = "        ";
    private const string TenSpaces = "          ";
    private const string TwelveSpaces = "            ";

    internal static string FormatDependencyDetails(
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim) =>
        FormatDependencyDetails(victor, victim, CultureInfo.InvariantCulture);

    internal static string FormatDependencyDetails(
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        CultureInfo culture)
    {
        MessageFormats formats = GetMessageFormats(culture);
        var log = new StringBuilder();
        AppendDependencyDetails(log, victor, victim, formats, culture);
        return log.ToString();
    }

    internal static string FormatWarningMessage(
        string simpleAssemblyName,
        string body) =>
        FormatWarningMessage(simpleAssemblyName, body, CultureInfo.InvariantCulture);

    internal static string FormatWarningMessage(
        string simpleAssemblyName,
        string body,
        CultureInfo culture)
        => Format(culture, GetMessageFormats(culture).FoundConflicts, simpleAssemblyName, body);

    /// <summary>
    /// Formats the conflict header and dependency details without the outer MSB3277 wrapper.
    /// This text matches the legacy warning body.
    /// <c>UnresolvedAssemblyConflicts</c> items use this text for the <c>logMessage</c> metadata.
    /// The warning <see cref="BuildEventArgs.Message"/> adds the outer wrapper.
    /// </summary>
    internal static string FormatWarningBody(
        AssemblyConflictLossReason lossReason,
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim) =>
        FormatWarningBody(lossReason, victor, victim, CultureInfo.InvariantCulture);

    internal static string FormatWarningBody(
        AssemblyConflictLossReason lossReason,
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        CultureInfo culture)
    {
        MessageFormats formats = GetMessageFormats(culture);
        var log = new StringBuilder();
        log.Append(FormatHeaderOnly(victor.FusionName, victim.FusionName, lossReason, victim.IsPrimary, formats, culture));
        log.AppendLine();
        AppendDependencyDetails(log, victor, victim, formats, culture);
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
        bool victimIsPrimary) =>
        FormatHeaderOnly(victorFusionName, victimFusionName, lossReason, victimIsPrimary, CultureInfo.InvariantCulture);

    internal static string FormatHeaderOnly(
        string victorFusionName,
        string victimFusionName,
        AssemblyConflictLossReason lossReason,
        bool victimIsPrimary,
        CultureInfo culture)
        => FormatHeaderOnly(
            victorFusionName,
            victimFusionName,
            lossReason,
            victimIsPrimary,
            GetMessageFormats(culture),
            culture);

    private static string FormatHeaderOnly(
        string victorFusionName,
        string victimFusionName,
        AssemblyConflictLossReason lossReason,
        bool victimIsPrimary,
        MessageFormats formats,
        CultureInfo culture)
    {
        string header = Format(culture, formats.ConflictFound, victorFusionName, victimFusionName);
        return lossReason switch
        {
            AssemblyConflictLossReason.HadLowerVersion
                => string.Concat(header, Environment.NewLine, FourSpaces, Format(culture, formats.ConflictHigherVersionChosen, victorFusionName)),
            AssemblyConflictLossReason.WasNotPrimary
                => string.Concat(header, Environment.NewLine, FourSpaces, Format(culture, formats.ConflictPrimaryChosen, victorFusionName, victimFusionName)),
            AssemblyConflictLossReason.InsolubleConflict when !victimIsPrimary
                => string.Concat(header, Environment.NewLine, Format(culture, formats.ConflictUnsolvable, victorFusionName, victimFusionName)),
            _ => header,
        };
    }

    private static void AppendDependencyDetails(
        StringBuilder log,
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        MessageFormats formats,
        CultureInfo culture)
    {
        AppendReferenceDetails(log, victor, formats.ReferenceDependsOn, formats, culture);
        log.AppendLine();
        AppendReferenceDetails(log, victim, formats.UnifiedReferenceDependsOn, formats, culture);
    }

    private static void AppendReferenceDetails(
        StringBuilder log,
        AssemblyConflictReferenceDetails details,
        string headerFormat,
        MessageFormats formats,
        CultureInfo culture)
    {
        log.Append(FourSpaces);
        log.Append(Format(culture, headerFormat, details.FusionName, details.FullPath));

        if (details.IsPrimary && !details.IsResolved)
        {
            log.AppendLine().Append(EightSpaces).Append(Format(culture, formats.UnresolvedPrimaryItemSpec, details.UnresolvedPrimaryItemSpec));
        }
        else if (details.IsPrimary && details.IsResolved)
        {
            log.AppendLine().Append(EightSpaces).AppendLine(details.FullPath);
            log.Append(TenSpaces).Append(Format(culture, formats.PrimarySourceItemsForReference, details.FullPath));
            for (int i = 0; i < details.PrimarySourceItemSpecs.Count; i++)
            {
                log.AppendLine().Append(TwelveSpaces).Append(details.PrimarySourceItemSpecs[i]);
            }
        }

        for (int i = 0; i < details.Dependees.Count; i++)
        {
            AssemblyConflictDependee dependee = details.Dependees[i];
            log.AppendLine().Append(EightSpaces).AppendLine(dependee.DependeeFullPath);
            log.Append(TenSpaces).Append(Format(culture, formats.PrimarySourceItemsForReference, dependee.DependeeFullPath));
            for (int j = 0; j < dependee.SourceItemSpecs.Count; j++)
            {
                log.AppendLine().Append(TwelveSpaces).Append(dependee.SourceItemSpecs[j]);
            }
        }
    }

    private static MessageFormats GetMessageFormats(CultureInfo culture)
        => s_formatsByCulture.GetValue(culture, static culture => new MessageFormats(culture));

    private static string Format(CultureInfo culture, string format, object? arg0)
        => MessageFormatter.Format(culture, format, arg0);

    private static string Format(CultureInfo culture, string format, object? arg0, object? arg1)
        => MessageFormatter.Format(culture, format, arg0, arg1);

    internal sealed class LocalizedMessage(CultureInfo culture, string message)
    {
        internal CultureInfo Culture { get; } = culture;
        internal string Message { get; } = message;
    }

    private sealed class MessageFormats
    {
        internal MessageFormats(CultureInfo culture)
        {
            ConflictFound = GetResource("AssemblyConflict_ConflictFound", culture);
            ConflictHigherVersionChosen = GetResource("AssemblyConflict_ConflictHigherVersionChosen", culture);
            ConflictPrimaryChosen = GetResource("AssemblyConflict_ConflictPrimaryChosen", culture);
            ConflictUnsolvable = GetResource("AssemblyConflict_ConflictUnsolvable", culture);
            ReferenceDependsOn = GetResource("AssemblyConflict_ReferenceDependsOn", culture);
            UnifiedReferenceDependsOn = GetResource("AssemblyConflict_UnifiedReferenceDependsOn", culture);
            UnresolvedPrimaryItemSpec = GetResource("AssemblyConflict_UnResolvedPrimaryItemSpec", culture);
            PrimarySourceItemsForReference = GetResource("AssemblyConflict_PrimarySourceItemsForReference", culture);

            string foundConflicts = GetResource("AssemblyConflict_FoundConflicts", culture);
            FoundConflicts = MessageParser.TryStripAnyCode(foundConflicts, out string? strippedMessage)
                ? strippedMessage
                : foundConflicts;
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

        private static string GetResource(string resourceName, CultureInfo culture)
            => Resources.SR.ResourceManager.GetString(resourceName, culture)
                ?? throw new InvalidOperationException($"The resource string '{resourceName}' was not found for culture '{culture.Name}'.");
    }
}

/// <summary>
/// Reports the references and project items that caused an assembly conflict.
/// RAR logs this low-importance event when conflict resolution does not produce a warning.
/// The <see cref="BuildEventArgs.Message"/> is rendered in invariant English from the structured details.
/// </summary>
[Serializable]
public sealed class AssemblyConflictDependencyDetailsMessageEventArgs : BuildMessageEventArgs
{
    private string? _formattedMessage;
    [NonSerialized]
    private AssemblyConflictMessageFormatter.LocalizedMessage? _localizedMessage;

    internal AssemblyConflictDependencyDetailsMessageEventArgs()
    {
    }

    internal AssemblyConflictDependencyDetailsMessageEventArgs(
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        string senderName,
        MessageImportance importance,
        DateTime eventTimestamp)
        : base(message: null, helpKeyword: null, senderName, importance, eventTimestamp)
    {
        Victor = victor;
        Victim = victim;
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
            if (_formattedMessage is null && Victor is not null && Victim is not null)
            {
                _formattedMessage = AssemblyConflictMessageFormatter.FormatDependencyDetails(Victor, Victim);
            }

            return _formattedMessage ?? base.Message;
        }
    }

    /// <summary>
    /// Formats the message using resources and argument formatting for the specified culture.
    /// </summary>
    /// <param name="culture">The culture to use when formatting the message.</param>
    /// <returns>The formatted message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    public string FormatMessage(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (culture.Equals(CultureInfo.InvariantCulture))
        {
            return Message ?? string.Empty;
        }

        AssemblyConflictMessageFormatter.LocalizedMessage? localizedMessage = Volatile.Read(ref _localizedMessage);
        if (localizedMessage is not null && localizedMessage.Culture.Equals(culture))
        {
            return localizedMessage.Message;
        }

        string message = AssemblyConflictMessageFormatter.FormatDependencyDetails(Victor, Victim, culture);
        Volatile.Write(ref _localizedMessage, new(culture, message));
        return message;
    }

    internal bool IsMessageMaterialized => _formattedMessage is not null;

    internal override void WriteToStream(BinaryWriter writer)
    {
        // The receiving node reconstructs the message from the structured fields.
        base.WriteToStream(writer);
        Victor.WriteToStream(writer);
        Victim.WriteToStream(writer);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        Victor = AssemblyConflictReferenceDetails.CreateFromStream(reader);
        Victim = AssemblyConflictReferenceDetails.CreateFromStream(reader);
    }
}

/// <summary>
/// Reports an unresolved assembly version conflict (MSB3277).
/// The structured details identify the victor, the victim, and their dependency chains.
/// The <see cref="BuildEventArgs.Message"/> is rendered in invariant English from the structured details.
/// </summary>
[Serializable]
public sealed class AssemblyConflictWarningEventArgs : BuildWarningEventArgs
{
    private string? _formattedBody;
    private string? _formattedMessage;
    [NonSerialized]
    private AssemblyConflictMessageFormatter.LocalizedMessage? _localizedMessage;

    internal AssemblyConflictWarningEventArgs()
    {
    }

    internal AssemblyConflictWarningEventArgs(
        string simpleAssemblyName,
        AssemblyConflictLossReason lossReason,
        AssemblyConflictReferenceDetails victor,
        AssemblyConflictReferenceDetails victim,
        string code,
        string? file,
        int lineNumber,
        int columnNumber,
        string? helpKeyword,
        string senderName,
        DateTime eventTimestamp,
        string? formattedBody = null)
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
        LossReason = lossReason;
        Victor = victor;
        Victim = victim;
        _formattedBody = formattedBody;
    }

    /// <summary>
    /// Gets the simple (short) name of the assembly for which conflicting versions were found.
    /// </summary>
    public string SimpleAssemblyName { get; private set; } = string.Empty;

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
            if (_formattedMessage is null && Victor is not null && Victim is not null)
            {
                string body = _formattedBody ?? AssemblyConflictMessageFormatter.FormatWarningBody(LossReason, Victor, Victim);
                _formattedMessage = AssemblyConflictMessageFormatter.FormatWarningMessage(SimpleAssemblyName, body);
                _formattedBody = null;
            }

            return _formattedMessage ?? base.Message;
        }
    }

    /// <summary>
    /// Formats the message using resources and argument formatting for the specified culture.
    /// </summary>
    /// <param name="culture">The culture to use when formatting the message.</param>
    /// <returns>The formatted message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    public string FormatMessage(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (culture.Equals(CultureInfo.InvariantCulture))
        {
            return Message ?? string.Empty;
        }

        AssemblyConflictMessageFormatter.LocalizedMessage? localizedMessage = Volatile.Read(ref _localizedMessage);
        if (localizedMessage is not null && localizedMessage.Culture.Equals(culture))
        {
            return localizedMessage.Message;
        }

        string body = AssemblyConflictMessageFormatter.FormatWarningBody(LossReason, Victor, Victim, culture);
        string message = AssemblyConflictMessageFormatter.FormatWarningMessage(SimpleAssemblyName, body, culture);
        Volatile.Write(ref _localizedMessage, new(culture, message));
        return message;
    }

    internal bool IsMessageMaterialized => _formattedMessage is not null;

    internal override void WriteToStream(BinaryWriter writer)
    {
        // The receiving node reconstructs the message from the structured fields.
        base.WriteToStream(writer);
        writer.Write(SimpleAssemblyName);
        writer.Write7BitEncodedInt((int)LossReason);
        Victor.WriteToStream(writer);
        Victim.WriteToStream(writer);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        SimpleAssemblyName = reader.ReadString();
        LossReason = (AssemblyConflictLossReason)reader.Read7BitEncodedInt();
        Victor = AssemblyConflictReferenceDetails.CreateFromStream(reader);
        Victim = AssemblyConflictReferenceDetails.CreateFromStream(reader);
    }
}
