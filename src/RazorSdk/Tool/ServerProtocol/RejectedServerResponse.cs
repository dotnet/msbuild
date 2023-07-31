// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal sealed class RejectedServerResponse : ServerResponse
    {
        public override ResponseType Type => ResponseType.Rejected;

        /// <summary>
        /// RejectedResponse has no body.
        /// </summary>
        protected override void AddResponseBody(BinaryWriter writer) { }
    }
}
