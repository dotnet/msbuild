// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_ASSEMBLYLOADCONTEXT

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using System.Runtime.Loader;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class ValidateAssemblyLoadContext : Task
    {
        public override bool Execute()
        {
            var thisLoadContext = AssemblyLoadContext.GetLoadContext(typeof(ValidateAssemblyLoadContext).Assembly);

            // The straightforward implementation of this check:
            //   if (thisLoadContext is MSBuildLoadContext context)
            // fails here because MSBuildLoadContext (in this test assembly) is from MSBuild.exe via
            // IVT, but the one that actually gets used for task isolation is in Microsoft.Build.dll.
            // This probably doesn't need to be how it is forever: https://github.com/dotnet/msbuild/issues/5041
            if (thisLoadContext.GetType().FullName == typeof(MSBuildLoadContext).FullName)
            {
                if (!thisLoadContext.Name.EndsWith(typeof(ValidateAssemblyLoadContext).Assembly.GetName().Name + ".dll"))
                {
                    Log.LogError($"Unexpected AssemblyLoadContext name: \"{thisLoadContext.Name}\", but the current executing assembly was {typeof(ValidateAssemblyLoadContext).Assembly.GetName().Name}");
                }
                else
                {
                    Log.LogMessage(MessageImportance.High, $"Task {nameof(ValidateAssemblyLoadContext)} loaded in AssemblyLoadContext named {thisLoadContext.Name}");
                }
            }
            else
            {
                Log.LogError($"Load context was a {thisLoadContext.GetType().AssemblyQualifiedName} instead of an {typeof(MSBuildLoadContext).AssemblyQualifiedName}");
            }

            return !Log.HasLoggedErrors;
        }
    }
}
#endif
