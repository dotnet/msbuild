// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watcher.Tools
{
    internal enum HotReloadProfile
    {
        Default,

        /// <summary>
        /// Blazor WebAssembly app
        /// </summary>
        BlazorWebAssembly,

        /// <summary>
        /// Blazor WebAssembly app hosted by an ASP.NET Core app.
        /// </summary>
        BlazorHosted,
    }
}
