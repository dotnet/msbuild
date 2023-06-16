// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Extensions.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal sealed class NullReporter : IReporter
    {
        private NullReporter()
        { }

        public static IReporter Singleton { get; } = new NullReporter();

        public void Verbose(string message, string emoji = "⌚")
        { }

        public void Output(string message, string emoji = "⌚")
        { }

        public void Warn(string message, string emoji = "⌚")
        { }

        public void Error(string message, string emoji = "❌")
        { }
    }
}
