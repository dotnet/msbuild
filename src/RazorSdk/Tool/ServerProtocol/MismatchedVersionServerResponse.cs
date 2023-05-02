// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal sealed class MismatchedVersionServerResponse : ServerResponse
    {
        public override ResponseType Type => ResponseType.MismatchedVersion;

        /// <summary>
        /// MismatchedVersion has no body.
        /// </summary>
        protected override void AddResponseBody(BinaryWriter writer) { }
    }
}
