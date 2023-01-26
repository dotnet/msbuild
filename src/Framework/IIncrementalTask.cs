// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for tasks which is incremental
    /// </summary>
    public interface IIncrementalTask
    {
        void SetQuestion(bool question);
    }
}
