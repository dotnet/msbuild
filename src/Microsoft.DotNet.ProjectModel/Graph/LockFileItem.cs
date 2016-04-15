// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public class LockFileItem
    {
        public LockFileItem()
        {
            Properties = new Dictionary<string, string>();;
        }

        public LockFileItem(string path) : this()
        {
            Path = path;
        }

        public LockFileItem(string path, IDictionary<string, string> properties) : this(path)
        {
            Properties = new Dictionary<string, string>(properties);
        }

        public string Path { get; set; }

        public IDictionary<string, string> Properties { get; }

        public static implicit operator string (LockFileItem item) => item.Path;

        public static implicit operator LockFileItem(string path) => new LockFileItem { Path = path };

        public override string ToString() => Path;
    }
}
