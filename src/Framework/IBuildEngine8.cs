// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends <see cref="IBuildEngine6" /> to allow tasks to know when the
    /// warnings they log were actually converted to errors.
    /// </summary>
    public interface IBuildEngine8 : IBuildEngine7
    {
        public event BuildWarningEventHandler WarningLoggedAsError;
    }
}
