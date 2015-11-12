// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet
{
    public interface IPackageFile
    {
        /// <summary>
        /// Gets the full path of the file inside the package.
        /// </summary>
        string Path
        {
            get;
        }

        Stream GetStream();
    }
}