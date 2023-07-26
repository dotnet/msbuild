// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Logging.Tests
{
    internal class EmptyTestSuppressionEngine : SuppressionEngine
    {
        protected override Stream GetReadableStream(string suppressionFile) => new MemoryStream();

        protected override Stream GetWritableStream(string suppressionFile) => new MemoryStream();
    }
}
