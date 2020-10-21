// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
