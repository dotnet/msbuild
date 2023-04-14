// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
