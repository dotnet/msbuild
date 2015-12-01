// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public class Message
    {
        public string MessageType { get; set; }

        public JToken Payload { get; set; }

        public override string ToString()
        {
            return "(" + MessageType + ") -> " + (Payload == null ? "null" : Payload.ToString(Formatting.Indented));
        }
    }
}