// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Represents a type used to log setup operations.
    /// </summary>
    internal interface ISetupLogger
    {
        string LogPath { get; }
        void LogMessage(string message);
    }
}
