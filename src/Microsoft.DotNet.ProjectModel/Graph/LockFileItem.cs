// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.Extensions.ProjectModel.Graph
{
    public class LockFileItem
    {
        public string Path { get; set; }

        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public static implicit operator string (LockFileItem item) => item.Path;

        public static implicit operator LockFileItem(string path) => new LockFileItem { Path = path };

        public override string ToString() => Path;
    }
}
