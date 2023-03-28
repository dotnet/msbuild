// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.IO;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal sealed class ShutdownServerResponse : ServerResponse
    {
        public readonly int ServerProcessId;

        public ShutdownServerResponse(int serverProcessId)
        {
            ServerProcessId = serverProcessId;
        }

        public override ResponseType Type => ResponseType.Shutdown;

        protected override void AddResponseBody(BinaryWriter writer)
        {
            writer.Write(ServerProcessId);
        }

        public static ShutdownServerResponse Create(BinaryReader reader)
        {
            var serverProcessId = reader.ReadInt32();
            return new ShutdownServerResponse(serverProcessId);
        }
    }
}
