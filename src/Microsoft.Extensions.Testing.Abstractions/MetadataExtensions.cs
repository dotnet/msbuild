// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Extensions.Testing.Abstractions
{
    internal static class MetadataExtensions
    {
        internal static int GetMethodToken(this MethodInfo methodInfo)
        {
            var methodToken = methodInfo.MetadataToken;

            return methodToken;
        }

        internal static MethodDebugInformationHandle GetMethodDebugInformationHandle(this MethodInfo methodInfo)
        {
            var methodToken = methodInfo.GetMethodToken();
            var handle = ((MethodDefinitionHandle)MetadataTokens.Handle(methodToken)).ToDebugInformationHandle();
            return handle;
        }
    }
}
