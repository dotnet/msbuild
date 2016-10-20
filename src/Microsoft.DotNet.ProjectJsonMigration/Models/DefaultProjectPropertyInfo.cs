// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectJsonMigration.Models
{
    public class DefaultProjectPropertyInfo
    {
        public string Name {get; set;}
        public string Value {get; set;}
        public string Condition {get; set;}
        public string ParentCondition {get; set;}
    }
}

        