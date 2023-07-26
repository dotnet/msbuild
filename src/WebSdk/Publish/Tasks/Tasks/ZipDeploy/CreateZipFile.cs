// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    public class CreateZipFile : Task
    {
        [Required]
        public string FolderToZip { get; set; }

        [Required]
        public string ProjectName { get; set; }

        [Required]
        public string PublishIntermediateTempPath { get; set; }

        [Output]
        public string CreatedZipPath { get; private set; }

        public override bool Execute()
        {
            string zipFileName = ProjectName + "-" + DateTime.Now.ToString("yyyyMMddHHmmssFFF") + ".zip";
            CreatedZipPath = Path.Combine(PublishIntermediateTempPath, zipFileName);
            ZipFile.CreateFromDirectory(FolderToZip, CreatedZipPath);
            return true;
        }
    }
}
