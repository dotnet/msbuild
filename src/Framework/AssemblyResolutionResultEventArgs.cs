// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.Build.Framework;

/// <summary>
/// Experimental diagnostic report for a resolved reference without additional resolution details.
/// A non-CopyLocal result in this report always means that Private metadata was false.
/// </summary>
[Serializable]
internal sealed class AssemblyResolutionResultEventArgs : BuildMessageEventArgs
{
    private static readonly ConditionalWeakTable<CultureInfo, MessageFormats> s_formatsByCulture = new();
    private string? _formattedMessage;

    internal AssemblyResolutionResultEventArgs()
    {
    }

    internal AssemblyResolutionResultEventArgs(
        string assemblyName,
        string fullPath,
        string resolvedSearchPath,
        bool isPrimary,
        bool isCopyLocal,
        string senderName,
        MessageImportance importance,
        DateTime eventTimestamp)
        : base(message: null, helpKeyword: null, senderName, importance, eventTimestamp)
    {
        AssemblyName = assemblyName;
        FullPath = fullPath;
        ResolvedSearchPath = resolvedSearchPath;
        IsPrimary = isPrimary;
        IsCopyLocal = isCopyLocal;
    }

    internal string AssemblyName { get; private set; } = string.Empty;
    internal string FullPath { get; private set; } = string.Empty;
    internal string ResolvedSearchPath { get; private set; } = string.Empty;
    internal bool IsPrimary { get; private set; }
    internal bool IsCopyLocal { get; private set; }

    public override string Message => _formattedMessage ??= FormatMessageCore(CultureInfo.InvariantCulture);

    internal string FormatMessage(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.Equals(CultureInfo.InvariantCulture) ? Message : FormatMessageCore(culture);
    }

    private string FormatMessageCore(CultureInfo culture)
    {
        MessageFormats formats = s_formatsByCulture.GetValue(culture, static culture => new(culture));
        var builder = new StringBuilder();
        builder.AppendFormat(culture, IsPrimary ? formats.PrimaryReference : formats.Dependency, AssemblyName);
        builder.AppendLine().Append("    ").AppendFormat(culture, formats.Resolved, FullPath);
        builder.AppendLine().Append("    ").AppendFormat(culture, formats.ResolvedFrom, ResolvedSearchPath);
        if (!IsCopyLocal)
        {
            builder.AppendLine().Append("    ").Append(formats.PrivateIsFalse);
        }

        return builder.ToString();
    }

    internal override void WriteToStream(BinaryWriter writer)
    {
        base.WriteToStream(writer);
        writer.Write(AssemblyName);
        writer.Write(FullPath);
        writer.Write(ResolvedSearchPath);
        writer.Write(IsPrimary);
        writer.Write(IsCopyLocal);
    }

    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);
        AssemblyName = reader.ReadString();
        FullPath = reader.ReadString();
        ResolvedSearchPath = reader.ReadString();
        IsPrimary = reader.ReadBoolean();
        IsCopyLocal = reader.ReadBoolean();
    }

    private sealed class MessageFormats
    {
        internal MessageFormats(CultureInfo culture)
        {
            PrimaryReference = GetResource("PrimaryReference", culture);
            Dependency = GetResource("Dependency", culture);
            Resolved = GetResource("Resolved", culture);
            ResolvedFrom = GetResource("ResolvedFrom", culture);
            PrivateIsFalse = GetResource("NotCopyLocalBecauseIncomingItemAttributeOverrode", culture);
        }

        internal string PrimaryReference { get; }
        internal string Dependency { get; }
        internal string Resolved { get; }
        internal string ResolvedFrom { get; }
        internal string PrivateIsFalse { get; }

        private static string GetResource(string name, CultureInfo culture)
            => SR.ResourceManager.GetString("AssemblyResolutionResult_" + name, culture)
                ?? throw new InvalidOperationException($"Assembly resolution result resource '{name}' was not found for culture '{culture.Name}'.");
    }
}
