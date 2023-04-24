// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    // The compiler leverages .editorconfig files to transfer information between
    // msbuild and the compiler. However, the transfer of data from MSBuild to the
    // .editorconfig file to the source generator, causes a lot of issues as strings
    // are transferred from one type to another. For example, the editorconfig file
    // interprets "#" as comments and omits them from the produced AnalyzerConfigOptions.
    // Characters like {} in the filename cause issues with resolving the type. To work
    // around this, we encode everything before writing it to the editorconfig then decode
    // inside the Razor source generator.
    public class EncodeRazorInputItem : Task
    {
        [Required]
        public ITaskItem[] RazorInputItems { get; set; }

        [Output]
        public ITaskItem[] EncodedRazorInputItems { get; set; }

        public override bool Execute()
        {
            EncodedRazorInputItems = new ITaskItem[RazorInputItems.Length];

            for (var i = 0; i < RazorInputItems.Length; i++)
            {
                var input = RazorInputItems[i];
                var targetPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(input.GetMetadata("TargetPath")));

                var outputItem = new TaskItem(input);
                outputItem.SetMetadata("TargetPath", targetPath);

                EncodedRazorInputItems[i] = outputItem;
            }

            return !Log.HasLoggedErrors;
        }
    }
}
