// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
