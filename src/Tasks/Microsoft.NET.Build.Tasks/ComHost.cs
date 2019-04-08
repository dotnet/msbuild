// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    internal static class ComHost
    {
        // These need to match RESOURCEID_CLISDMAP and RESOURCETYPE_CLSIDMAP in core-setup, defined in comhost.h.
        private const int ClsidmapResourceId = 64;
        private const int ClsidmapResourceType = 1024;

        /// <summary>
        /// Create an ComHost with an embedded CLSIDMap file to map CLSIDs to .NET Classes.
        /// </summary>
        /// <param name="comHostSourceFilePath">The path of Apphost template, which has the place holder</param>
        /// <param name="comHostDestinationFilePath">The destination path for desired location to place, including the file name</param>
        /// <param name="intermediateAssembly">Path to the intermediate assembly, used for copying resources to PE apphosts.</param>
        /// <param name="clsidmap">The path to the *.clsidmap file.</param>
        /// <param name="log">Specify the logger used to log warnings and messages. If null, no logging is done.</param>
        public static void Create(
            string comHostSourceFilePath,
            string comHostDestinationFilePath,
            string clsidmapFilePath)
        {
            var destinationDirectory = new FileInfo(comHostDestinationFilePath).Directory.FullName;
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Copy apphost to destination path so it inherits the same attributes/permissions.
            File.Copy(comHostSourceFilePath, comHostDestinationFilePath, overwrite: true);

            if (ResourceUpdater.IsSupportedOS())
            {
                string clsidMap = File.ReadAllText(clsidmapFilePath);
                byte[] clsidMapBytes = Encoding.UTF8.GetBytes(clsidMap);

                ResourceUpdater updater = new ResourceUpdater(comHostDestinationFilePath);
                updater.AddResource(clsidMapBytes, (IntPtr)ClsidmapResourceType, (IntPtr)ClsidmapResourceId);
                updater.Update();
            }
            else
            {
                throw new BuildErrorException(Strings.CannotEmbedClsidMapIntoComhost);
            }
        }
    }
}
