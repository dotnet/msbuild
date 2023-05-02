// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
