// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Shared.Debugging
{
    internal class CommonWriter
    {
        // Action<string id, string callsite, IEnumerable<string> args>
        // id - something that identifies a group of messages. Process name and node type (central, worker) is a good id. The writer could choose different files depending on the ID. Or just print it differently.
        // callsite - class and method that logged the message
        // args - payload
        public static Action<string, string, IEnumerable<string>> Writer { get; set; }
    }
}
