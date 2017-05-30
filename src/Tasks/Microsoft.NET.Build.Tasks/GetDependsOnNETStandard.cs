// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Security;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Determines the assembly version to use for a given semantic version.
    /// </summary>
    public partial class GetDependsOnNETStandard : TaskBase
    {
        private const string NetStandardAssemblyName = "netstandard";
        /// <summary>
        /// Set of reference items to analyze.
        /// </summary>
        [Required]
        public ITaskItem[] References { get; set; }

        /// <summary>
        /// True if any of the references depend on netstandard.dll
        /// </summary>
        [Output]
        public bool DependsOnNETStandard { get; set; }

        protected override void ExecuteCore()
        {
            foreach (var reference in References)
            {
                var referenceSourcePath = ItemUtilities.GetSourcePath(reference);

                if (referenceSourcePath != null && File.Exists(referenceSourcePath))
                {
                    try
                    {
                        if (DependsOnNETStandard = GetFileDependsOnNETStandard(referenceSourcePath))
                        {
                            break;
                        }
                    }
                    catch (Exception e) when (IsReferenceException(e))
                    {
                        // ResolveAssemblyReference treats all of these exceptions as warnings
                        Log.LogWarning(Strings.GetDependsOnNETStandardFailedWithException, e.Message);
                    }
                }
            }
        }

        // ported from MSBuild's ReferenceTable.SetPrimaryAssemblyReferenceItem
        private static bool IsReferenceException(Exception e)
        {
            // These all derive from IOException
            //     DirectoryNotFoundException
            //     DriveNotFoundException
            //     EndOfStreamException
            //     FileLoadException
            //     FileNotFoundException
            //     PathTooLongException
            //     PipeException
            return e is BadImageFormatException
                   || e is UnauthorizedAccessException
                   || e is NotSupportedException
                   || (e is ArgumentException && !(e is ArgumentNullException))
                   || e is SecurityException
                   || e is IOException;
        }

    }
}
