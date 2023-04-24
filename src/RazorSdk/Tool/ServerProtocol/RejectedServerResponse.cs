// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;

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
