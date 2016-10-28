// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectJsonMigration.Models
{
    internal class SerializableMigrationDefaultsInfo
    {
        public IEnumerable<DefaultProjectItemInfo> Items { get; set; }
        public IEnumerable<DefaultProjectPropertyInfo> Properties { get; set; }
    }
}