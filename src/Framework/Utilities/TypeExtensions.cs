// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Shared;

internal static class TypeExtensions
{
    public static string GetAssemblyPath(this Type type)
        => Path.GetFullPath(AssemblyUtilities.GetAssemblyLocation(type.Assembly));
}
