// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ProjectJsonMigration.Models
{
    public class DefaultProjectItemInfo
    {
        public string ItemType {get; set;}
        public string Include {get; set;}
        public string Exclude {get; set;}
        public string Remove {get; set;}
        public string Condition {get; set;}
        public string ParentCondition {get; set;}
    }
}