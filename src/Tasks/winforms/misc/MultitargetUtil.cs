// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Resources {
    using System;

    /// <devdoc>
    ///     Helper class supporting Multitarget type assembly qualified name resolution for ResX API.
    ///     Note: this file is compiled into different assemblies (runtime and VSIP assemblies ...)
    /// </devdoc>
    internal static class MultitargetUtil
    {
        /// <devdoc>
        ///     This method gets assembly info for the corresponding type. If the delegate
        ///     is provided it is used to get this information.
        /// </devdoc>
        public static string GetAssemblyQualifiedName(Type type, Func<Type, string> typeNameConverter)
        {
            string assemblyQualifiedName = null;

            if (type != null)
            {
                if (typeNameConverter != null)
                {
                    try
                    {
                        assemblyQualifiedName = typeNameConverter(type);
                    }
                    catch (Exception e)
                    {
                        if (IsSecurityOrCriticalException(e))
                        {
                            throw;
                        }
                    }
                }

                if (string.IsNullOrEmpty(assemblyQualifiedName))
                {
                    assemblyQualifiedName = type.AssemblyQualifiedName;
                }
            }

            return assemblyQualifiedName;
        }

        // ExecutionEngineException is obsolete and shouldn't be used (to catch, throw or reference) anymore.
        // Pragma added to prevent converting the "type is obsolete" warning into build error.
        #pragma warning disable 618        
        private static bool IsSecurityOrCriticalException(Exception ex)
        {
            return ex is NullReferenceException
                    || ex is StackOverflowException
                    || ex is OutOfMemoryException
                    || ex is System.Threading.ThreadAbortException
                    || ex is ExecutionEngineException
                    || ex is IndexOutOfRangeException
                    || ex is AccessViolationException
                    || ex is System.Security.SecurityException;
        }
        #pragma warning restore 618
    }
}
