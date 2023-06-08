// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends <see cref="IBuildEngine6" /> to allow tasks to set whether they want to
    /// log an error when a task returns without logging an error.
    /// </summary>
    public interface IBuildEngine7 : IBuildEngine6
    {
        public bool AllowFailureWithoutError { get; set; }
    }
}
