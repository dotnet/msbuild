// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd;

internal readonly struct EvaluationDirectoryScope : IDisposable
{
    private readonly string? _previousDirectory;
    private readonly bool _enabled;

    internal EvaluationDirectoryScope(string? directory)
    {
        _enabled = directory is not null;
        _previousDirectory = _enabled ? FileUtilities.CurrentThreadWorkingDirectory : null;
        if (_enabled)
        {
            FileUtilities.CurrentThreadWorkingDirectory = directory;
        }
    }

    public void Dispose()
    {
        if (_enabled)
        {
            FileUtilities.CurrentThreadWorkingDirectory = _previousDirectory;
        }
    }
}
