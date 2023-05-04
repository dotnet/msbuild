// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;

using Microsoft.Build.Framework;

namespace Microsoft.Build.Engine.UnitTests;

/// <summary>
/// Task that emulates .NET 3.5 tasks.
/// </summary>
public sealed class TaskThatReturnsMinimalItem : ITask
{
    public IBuildEngine? BuildEngine { get; set; }
    public ITaskHost? HostObject { get; set; }

    [Output]
    public ITaskItem MinimalTaskItemOutput { get => new MinimalTaskItem(); }

    public bool Execute() => true;

    /// <summary>
    /// Minimal implementation of <see cref="ITaskItem"/> that uses a <see cref="Hashtable"/> for metadata,
    /// like MSBuild 3 did.
    /// </summary>
    internal sealed class MinimalTaskItem : ITaskItem
    {
        public string ItemSpec { get => $"{nameof(MinimalTaskItem)}spec"; set => throw new NotImplementedException(); }

        public ICollection MetadataNames => throw new NotImplementedException();

        public int MetadataCount => throw new NotImplementedException();

        public IDictionary CloneCustomMetadata()
        {
            Hashtable t = new();
            t["key"] = "value";

            return t;
        }
        public void CopyMetadataTo(ITaskItem destinationItem) => throw new NotImplementedException();
        public string GetMetadata(string metadataName) => "value";
        public void RemoveMetadata(string metadataName) => throw new NotImplementedException();
        public void SetMetadata(string metadataName, string metadataValue) => throw new NotImplementedException();
    }
}
