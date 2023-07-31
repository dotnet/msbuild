// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    //  This task is used for projects targeting .NET 5 and higher and generates errors if there are any
    //  unsupported WinMD references.
    public class CheckForUnsupportedWinMDReferences : TaskBase
    {
        public string TargetFrameworkMoniker { get; set; }

        public ITaskItem[] ReferencePaths { get; set; } = Array.Empty<ITaskItem>();

        protected override void ExecuteCore()
        {
            //  Check if there are referenced WinMD files.  If so, we will generate an error
            List<ITaskItem> winMDReferences = new List<ITaskItem>();
            foreach (var referencePath in ReferencePaths)
            {
                if (Path.GetExtension(referencePath.ItemSpec).Equals(".winmd", StringComparison.OrdinalIgnoreCase))
                {
                    winMDReferences.Add(referencePath);
                }
            }

            bool shouldShowWinMDReferenceErrors = true;

            if (winMDReferences.Any())
            {
                //  Check to see if there are any managed references that have windowsruntime metadata references.  If so,
                //  then likely those components need to be updated to support .NET 5.  So we generate an error about them,
                //  instead of listing all of the WinMD references, which are likely to be all of the WinMDs in the
                //  Microsoft.Windows.Sdk.Contracts NuGet package
                //
                //  Note that we don't check for the case where there is a reference ta a managed component that uses WinRT
                //  support not available in .NET 5, but there are no WinMD references.  In that case we would not generate
                //  a build error but would get a runtime error.  However, it seems that in most cases there are WinMD references
                //  that flow transitively, so the error will be triggered at build time.
                //
                //  The reason we do it this way is to avoid the perf impact of examining all references for windowsruntime metadata
                //  references in all builds.  In this case, we already know that we are going to generate an error, so the perf hit
                //  to figure out which one to generate shouldn't matter.
                foreach (var referencePath in ReferencePaths)
                {
                    if (!Path.GetExtension(referencePath.ItemSpec).Equals(".winmd", StringComparison.OrdinalIgnoreCase) &&
                        AssemblyHasWindowsRuntimeReference(referencePath.ItemSpec))
                    {
                        //  Ignore System.Runtime.WindowsRuntime.dll, as it has windowsruntime metadata references, but is a dependency of
                        //  the Microsoft.Windows.Sdk.Contracts package, so generating an error about it isn't helpful
                        if (Path.GetFileName(referencePath.ItemSpec).Equals("System.Runtime.WindowsRuntime.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        shouldShowWinMDReferenceErrors = false;

                        Log.LogError(Strings.WinMDTransitiveReferenceNotSupported, Path.GetFileName(referencePath.ItemSpec));
                    }
                }
            }

            if (shouldShowWinMDReferenceErrors)
            {
                //  There weren't any managed references which need to be updated to support .NET 5, so warn about the individual WinMD references
                foreach (var winMDReference in winMDReferences)
                {
                    Log.LogError(Strings.WinMDReferenceNotSupportedOnTargetFramework, TargetFrameworkMoniker, Path.GetFileName(winMDReference.ItemSpec));
                }
            }

        }

        private static bool AssemblyHasWindowsRuntimeReference(string sourcePath)
        {
            using (var assemblyStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read))
            {
                try
                {
                    using (PEReader peReader = new PEReader(assemblyStream, PEStreamOptions.LeaveOpen))
                    {
                        if (peReader.HasMetadata)
                        {
                            MetadataReader reader = peReader.GetMetadataReader();
                            if (reader.IsAssembly)
                            {
                                foreach (var assemblyReferenceHandle in reader.AssemblyReferences)
                                {
                                    if ((reader.GetAssemblyReference(assemblyReferenceHandle).Flags & System.Reflection.AssemblyFlags.WindowsRuntime) == System.Reflection.AssemblyFlags.WindowsRuntime)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    // not a PE
                    return false;
                }

                return false;
            }
        }
    }
}
