// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.Shared
{
    internal sealed partial class LoadedType
    {
        /// <summary>
        /// Gets whether the exact task type is marked with MSBuildDeclaredIOTaskAttribute.
        /// </summary>
        internal bool HasMSBuildDeclaredIOTaskAttribute { get; }

        private bool ReadMSBuildDeclaredIOTaskAttribute()
        {
            const string taskAttributeFullName =
                "Microsoft.Build.Framework.MSBuildDeclaredIOTaskAttribute";

            foreach (CustomAttributeData attribute in
                CustomAttributeData.GetCustomAttributes(Type))
            {
                string? attributeTypeName;
                try
                {
                    attributeTypeName = attribute.AttributeType?.FullName;
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    continue;
                }

                if (attributeTypeName == taskAttributeFullName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
