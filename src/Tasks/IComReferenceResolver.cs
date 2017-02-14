// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

// TYPELIBATTR clashes with the one in InteropServices.
using TYPELIBATTR = System.Runtime.InteropServices.ComTypes.TYPELIBATTR;

namespace Microsoft.Build.Tasks
{
    /*
     * Interface:   IComReferenceResolver
     * 
     * Callback interface for COM references to resolve their dependencies
     */
    internal interface IComReferenceResolver
    {
        /*
         * Method:  ResolveComClassicReference
         * 
         * Resolves a COM classic reference given the type library attributes and the type of wrapper to use.
         * If wrapper type is not specified, this method will first look for an existing reference in the project,
         * fall back to looking for a PIA and finally try to generate a regular tlbimp wrapper.
         *
         * This method is available for references to call back to resolve their dependencies
         */
        bool ResolveComClassicReference(TYPELIBATTR typeLibAttr, string outputDirectory, string wrapperType, string refName, out ComReferenceWrapperInfo wrapperInfo);

        /*
         * Method:  ResolveNetAssemblyReference
         * 
         * Resolves a .NET assembly reference using the list of resolved managed references supplied to the task.
         *
         * This method is available for references to call back to resolve their dependencies
         */
        bool ResolveNetAssemblyReference(string assemblyName, out string assemblyPath);

        /*
         * Method:  ResolveComAssemblyReference
         * 
         * Resolves a COM wrapper assembly reference based on the COM references resolved so far. This method is necessary
         * for Ax wrappers only, so all necessary references will be resolved by then (since we resolve them in 
         * the following order: pia, tlbimp, aximp)
         *
         * This method is available for references to call back to resolve their dependencies
         */
        bool ResolveComAssemblyReference(string assemblyName, out string assemblyPath);
    }
}
