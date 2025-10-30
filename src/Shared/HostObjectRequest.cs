// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    internal class HostObjectRequest : INodePacket
    {
        private int _callId;
        private string _methodName = string.Empty;

        public HostObjectRequest()
        {
            MethodName = string.Empty;
        }

        public HostObjectRequest(int callId, string methodName)
        {
            CallId = callId;
            MethodName = methodName;
        }

        public int CallId { get; set; }

        public string MethodName { get; set; }

        public NodePacketType Type => NodePacketType.HostObjectRequest;

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _callId);
            translator.Translate(ref _methodName);

            CallId = _callId;
            MethodName = _methodName;
        }

        public static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            HostObjectRequest packet = new HostObjectRequest();
            packet.Translate(translator);

            return packet;
        }
    }
}
