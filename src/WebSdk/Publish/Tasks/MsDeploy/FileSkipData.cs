// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    public class FileSkipData
    {
        public string sourceProvider { get; set; }
        public string sourceFilePath { get; set; }
        public string destinationProvider { get; set; }
        public string destinationFilePath { get; set; }

    }
}
