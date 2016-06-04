// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Configurer
{
    public class NuGetPackagesArchiver : INuGetPackagesArchiver
    {
        public string ExtractArchive()
        {
            // -- ExtractArchive
            // find archive
            // extract archive to temporary folder
            //      Path.GetTempPath();
            //      Path.GetRandomFileName();
            //      Consider putting this inside an abstraction that will delete the folder automatically once it is done.

            return @"C:\Users\licavalc\git\temp\feed";
        }
    }
}
