// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class AddBoolPropertyTransform : AddPropertyTransform<bool> 
    {
        public AddBoolPropertyTransform(string propertyName)
            : base(propertyName, b => b.ToString(), b => b) { }

        public AddBoolPropertyTransform(string propertyName, Func<bool, bool> condition)
            : base(propertyName, b => b.ToString(), condition) { }
    }
}
