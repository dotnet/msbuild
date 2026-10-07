// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
///  Represents the location of an MSBuild XML element in a file.
/// </summary>
/// <remarks>
///  Instances are immutable. Editing project XML through the MSBuild APIs invalidates locations associated
///  with that XML until it is reloaded.
///  <para>
///   Keep this type and its implementations compact. A project can contain many thousands of locations, and
///   locations are transferred between build nodes.
///  </para>
/// </remarks>
[Serializable]
public abstract class ElementLocation : IElementLocation, IImmutable
{
    /// <summary>
    ///  Gets the singleton location with no file, line, or column information.
    /// </summary>
    /// <remarks>
    ///  Use <see langword="null"/> when a location is absent. Use this value when an object has a location
    ///  conceptually, but no specific location is available, such as an unnamed in-memory project.
    /// </remarks>
    public static ElementLocation Empty { get; } = new FileOnly(string.Empty);

    /// <summary>
    ///  Gets the singleton location with no file, line, or column information.
    /// </summary>
    /// <remarks>
    ///  This compatibility property returns <see cref="Empty"/>. Use <see cref="Empty"/> in new code.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static ElementLocation EmptyLocation => Empty;

    /// <summary>
    ///  Gets the file from which the element originated, or an empty string when the file is unknown.
    /// </summary>
    /// <remarks>
    ///  This path may differ from the project path when the element originated in an imported project or
    ///  targets file.
    /// </remarks>
    public abstract string File { get; }

    /// <summary>
    ///  Gets the one-based line number, or <c>0</c> when the line is unknown.
    /// </summary>
    public abstract int Line { get; }

    /// <summary>
    ///  Gets the one-based column number, or <c>0</c> when the column is unknown.
    /// </summary>
    public abstract int Column { get; }

    /// <summary>
    ///  Gets the location formatted for inclusion in a message.
    /// </summary>
    /// <remarks>
    ///  The returned value has the form <c>file</c>, <c>file (line)</c>, or
    ///  <c>file (line,column)</c>, depending on the available coordinates. Prefer placing location information
    ///  at the beginning of a message rather than embedding it within the message text.
    /// </remarks>
    public string LocationString => GetLocationString(File, Line, Column);

    /// <summary>
    ///  Returns a hash code for this location.
    /// </summary>
    /// <returns>
    ///  A hash code derived from the file, line, and column.
    /// </returns>
    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(File) ^ Line ^ Column;

    /// <summary>
    ///  Determines whether an object represents the same file, line, and column as this location.
    /// </summary>
    /// <param name="obj">The object to compare with this location.</param>
    /// <returns>
    ///  <see langword="true"/> when <paramref name="obj"/> is an <see cref="ElementLocation"/> with the same
    ///  file, line, and column; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  File names are compared using <see cref="StringComparison.OrdinalIgnoreCase"/> on all platforms.
    /// </remarks>
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj)
        || (obj is ElementLocation other
            && Line == other.Line
            && Column == other.Column
            && StringComparer.OrdinalIgnoreCase.Equals(File, other.File));

    /// <summary>
    ///  Returns the location formatted for inclusion in a message.
    /// </summary>
    /// <returns>
    ///  The value of <see cref="LocationString"/>.
    /// </returns>
    public override string ToString() => LocationString;

    /// <summary>
    ///  Writes this location to a node packet.
    /// </summary>
    /// <param name="translator">The translator receiving the location data.</param>
    /// <remarks>
    ///  Line and column are always serialized as integers so that the wire format does not depend on the
    ///  in-memory representation.
    /// </remarks>
    void ITranslatable.Translate(ITranslator translator)
    {
        Assumed.Equal(translator.Mode, TranslationDirection.WriteToStream, "write only");

        string file = File;
        int line = Line;
        int column = Column;
        translator.Translate(ref file);
        translator.Translate(ref line);
        translator.Translate(ref column);
    }

    /// <summary>
    ///  Reads a location from a node packet.
    /// </summary>
    /// <param name="translator">The translator providing the serialized location data.</param>
    /// <returns>
    ///  The deserialized location.
    /// </returns>
    /// <remarks>
    ///  A factory is required because <see cref="ElementLocation"/> is abstract and uses specialized
    ///  representations.
    /// </remarks>
    internal static ElementLocation FactoryForDeserialization(ITranslator translator)
    {
        string? file = null;
        int line = 0;
        int column = 0;
        translator.Translate(ref file);
        translator.Translate(ref line);
        translator.Translate(ref column);

        return Create(file, line, column);
    }

    /// <summary>
    ///  Creates a location for which only the file is known.
    /// </summary>
    /// <param name="file">The file associated with the location, or <see langword="null"/> if unknown.</param>
    /// <returns>
    ///  A file-only location, or <see cref="Empty"/> when <paramref name="file"/> is <see langword="null"/> or
    ///  empty.
    /// </returns>
    /// <remarks>
    ///  File-only locations are used for objects that were not evaluated from XML, such as newly created items.
    /// </remarks>
    internal static ElementLocation Create(string? file)
        => file.IsNullOrEmpty()
            ? Empty
            : new FileOnly(file);

    /// <summary>
    ///  Creates a location from a file and optional line and column coordinates.
    /// </summary>
    /// <param name="file">The file containing the element, or <see langword="null"/> if unknown.</param>
    /// <param name="line">The one-based line number, or <c>0</c> if unknown.</param>
    /// <param name="column">The one-based column number, or <c>0</c> if unknown.</param>
    /// <returns>
    ///  A location containing the supplied information. Returns <see cref="Empty"/> when
    ///  <paramref name="file"/> is <see langword="null"/> or empty and both coordinates are <c>0</c>.
    /// </returns>
    /// <remarks>
    ///  The returned object uses a compact representation based on the supplied information.
    /// </remarks>
    public static ElementLocation Create(string? file, int line, int column)
    {
        Assumed.PositiveOrZero(line, "Use zero for unknown");
        Assumed.PositiveOrZero(column, "Use zero for unknown");

        string normalizedFile = file ?? string.Empty;

        if (line == 0 && column == 0)
        {
            return normalizedFile.Length == 0
                ? Empty
                : new FileOnly(normalizedFile);
        }

        return line <= ushort.MaxValue && column <= ushort.MaxValue
            ? new Small(normalizedFile, (ushort)line, (ushort)column)
            : new Regular(normalizedFile, line, column);
    }

    /// <summary>
    ///  Formats a file and optional coordinates for inclusion in a message.
    /// </summary>
    /// <param name="file">The file associated with the location.</param>
    /// <param name="line">The one-based line number, or <c>0</c> if unknown.</param>
    /// <param name="column">The one-based column number, or <c>0</c> if unknown.</param>
    /// <returns>
    ///  The formatted location.
    /// </returns>
    private static string GetLocationString(string file, int line, int column)
    {
        if (line == 0)
        {
            return file;
        }

        return column == 0
            ? $"{file} ({line})"
            : $"{file} ({line},{column})";
    }

    /// <summary>
    ///  Stores a location with no line or column information.
    /// </summary>
    private sealed class FileOnly(string file) : ElementLocation
    {
        private readonly string _file = file;

        public override string File => _file;

        public override int Line => 0;

        public override int Column => 0;
    }

    /// <summary>
    ///  Stores a location when either coordinate does not fit in an unsigned 16-bit integer.
    /// </summary>
    private sealed class Regular(string file, int line, int column) : ElementLocation
    {
        private readonly string _file = file;
        private readonly int _line = line;
        private readonly int _column = column;

        public override string File => _file;

        public override int Line => _line;

        public override int Column => _column;
    }

    /// <summary>
    ///  Stores the common case in which both coordinates fit in unsigned 16-bit integers.
    /// </summary>
    /// <remarks>
    ///  Using compact coordinate fields reduces memory consumption in projects containing many thousands of
    ///  locations.
    /// </remarks>
    private sealed class Small(string file, ushort line, ushort column) : ElementLocation
    {
        private readonly string _file = file;
        private readonly ushort _line = line;
        private readonly ushort _column = column;

        public override string File => _file;

        public override int Line => _line;

        public override int Column => _column;
    }
}
