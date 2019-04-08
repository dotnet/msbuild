// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class CreateComHost : TaskBase
    {
        [Required]
        public string ComHostSourcePath { get; set; }

        [Required]
        public string ComHostDestinationPath { get; set; }

        [Required]
        public string ClsidMapPath { get; set; }

        protected override void ExecuteCore()
        {
            ComHost.Create(
                ComHostSourcePath,
                ComHostDestinationPath,
                ClsidMapPath);
        }
    }
}
