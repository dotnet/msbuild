// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using System.Security;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Determines if any Reference depends on netstandard.dll.
    /// </summary>
    public partial class GetDependsOnNETStandard : TaskBase
    {
        private const string NetStandardAssemblyName = "netstandard";

        // System.Runtime from netstandard1.5
        // We also treat this as depending on netstandard so that we can provide netstandard1.5 and netstandard1.6 compatible 
        // facades since net461 was previously only compatible with netstandard1.4 and thus packages only provided netstandard1.4
        // compatible facades.
        private const string SystemRuntimeAssemblyName = "System.Runtime";
        private static readonly Version SystemRuntimeMinVersion = new Version(4, 1, 0, 0);

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
            DependsOnNETStandard = AnyReferenceDependsOnNETStandard();
        }

        private bool AnyReferenceDependsOnNETStandard()
        {
            foreach (var reference in References)
            {
                var referenceSourcePath = ItemUtilities.GetSourcePath(reference);

                if (referenceSourcePath != null && File.Exists(referenceSourcePath))
                {
                    try
                    {
                        if (GetFileDependsOnNETStandard(referenceSourcePath))
                        {
                            return true;
                        }
                    }
                    catch (Exception e) when (IsReferenceException(e))
                    {
                        // ResolveAssemblyReference treats all of these exceptions as warnings so we'll do the same
                        Log.LogWarning(Strings.GetDependsOnNETStandardFailedWithException, e.Message, referenceSourcePath);
                    }
                }
            }

            return false;
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
