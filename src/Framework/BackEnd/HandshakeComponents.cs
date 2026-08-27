// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Internal;

/// <summary>
///  Represents the components of a handshake in a structured format with named fields.
/// </summary>
internal readonly struct HandshakeComponents
{
    private readonly int options;
    private readonly int salt;
    private readonly int fileVersionMajor;
    private readonly int fileVersionMinor;
    private readonly int fileVersionBuild;
    private readonly int fileVersionPrivate;
    private readonly int sessionId;

    public HandshakeComponents(int options, int salt, int fileVersionMajor, int fileVersionMinor, int fileVersionBuild, int fileVersionPrivate, int sessionId)
    {
        this.options = options;
        this.salt = salt;
        this.fileVersionMajor = fileVersionMajor;
        this.fileVersionMinor = fileVersionMinor;
        this.fileVersionBuild = fileVersionBuild;
        this.fileVersionPrivate = fileVersionPrivate;
        this.sessionId = sessionId;
    }

    public HandshakeComponents(int options, int salt, int fileVersionMajor, int fileVersionMinor, int fileVersionBuild, int fileVersionPrivate)
        : this(options, salt, fileVersionMajor, fileVersionMinor, fileVersionBuild, fileVersionPrivate, 0)
    {
    }

    public int Options => options;

    public int Salt => salt;

    public int FileVersionMajor => fileVersionMajor;

    public int FileVersionMinor => fileVersionMinor;

    public int FileVersionBuild => fileVersionBuild;

    public int FileVersionPrivate => fileVersionPrivate;

    public int SessionId => sessionId;

    public IEnumerable<KeyValuePair<string, int>> EnumerateComponents()
    {
        yield return new KeyValuePair<string, int>(nameof(Options), Options);
        yield return new KeyValuePair<string, int>(nameof(Salt), Salt);
        yield return new KeyValuePair<string, int>(nameof(FileVersionMajor), FileVersionMajor);
        yield return new KeyValuePair<string, int>(nameof(FileVersionMinor), FileVersionMinor);
        yield return new KeyValuePair<string, int>(nameof(FileVersionBuild), FileVersionBuild);
        yield return new KeyValuePair<string, int>(nameof(FileVersionPrivate), FileVersionPrivate);
        yield return new KeyValuePair<string, int>(nameof(SessionId), SessionId);
    }

    public override string ToString()
        => $"{options} {salt} {fileVersionMajor} {fileVersionMinor} {fileVersionBuild} {fileVersionPrivate} {sessionId}";
}
