// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
