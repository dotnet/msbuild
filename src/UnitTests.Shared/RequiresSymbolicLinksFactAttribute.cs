// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

using Microsoft.Build.Shared;
using Xunit;


namespace Microsoft.Build.UnitTests
{
    public sealed class RequiresSymbolicLinksFactAttribute : FactAttribute
    {
        public RequiresSymbolicLinksFactAttribute()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return;
            }

            string sourceFile = FileUtilities.GetTemporaryFile();
            string destinationFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Create(sourceFile).Dispose();

                string? errorMessage = null;
                if (!NativeMethodsShared.MakeSymbolicLink(destinationFile, sourceFile, ref errorMessage))
                {
                    Skip = "This test requires symbolic link support to run.";
                }
            }
            finally
            {
                if (File.Exists(sourceFile))
                {
                    File.Delete(sourceFile);
                }
                if (File.Exists(destinationFile))
                {
                    File.Delete(destinationFile);
                }
            }
        }
    }
}
