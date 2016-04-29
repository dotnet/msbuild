// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ProjectModel.Server.Tests
{
    public class DthMessage
    {
        public string HostId { get; set; }

        public string MessageType { get; set; }

        public int ContextId { get; set; }

        public int Version { get; set; }

        public JToken Payload { get; set; }

        // for ProjectContexts message only
        public Dictionary<string, int> Projects { get; set; }
    }
}
