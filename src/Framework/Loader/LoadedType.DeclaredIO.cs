// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.Shared
{
    internal sealed partial class LoadedType
    {
        private readonly DeclaredIOMetadata _declaredIOMetadata;

        /// <summary>
        /// Gets whether the exact task type is marked with MSBuildDeclaredIOTaskAttribute.
        /// </summary>
        internal bool HasMSBuildDeclaredIOTaskAttribute =>
            _declaredIOMetadata.HasTaskAttribute;

        /// <summary>
        /// Gets whether every recognized declared-I/O annotation has the expected shape.
        /// </summary>
        internal bool HasValidMSBuildDeclaredIOAttributes =>
            _declaredIOMetadata.HasValidAttributes;

        /// <summary>
        /// Gets task parameters that must be unset for the declared-I/O contract to apply.
        /// </summary>
        internal IReadOnlyList<string> DeclaredIORequiredUnsetParameters =>
            _declaredIOMetadata.RequiredUnsetParameters;

        private DeclaredIOMetadata ReadMSBuildDeclaredIOAttributes()
        {
            const string taskAttributeFullName =
                "Microsoft.Build.Framework.MSBuildDeclaredIOTaskAttribute";
            const string requiresUnsetAttributeFullName =
                "Microsoft.Build.Framework.MSBuildDeclaredIORequiresUnsetAttribute";

            bool hasTaskAttribute = false;
            bool hasValidAttributes = true;
            List<string>? unsetParameters = null;

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
                    hasTaskAttribute = true;
                    continue;
                }

                if (attributeTypeName == requiresUnsetAttributeFullName)
                {
                    if (!TryReadDeclaredIOParameterName(
                            attribute,
                            out string? unsetParameter))
                    {
                        hasValidAttributes = false;
                        continue;
                    }

                    unsetParameters ??= [];
                    unsetParameters.Add(unsetParameter);
                }
            }

            return new DeclaredIOMetadata(
                hasTaskAttribute,
                hasValidAttributes,
                unsetParameters ?? []);
        }

        private static bool TryReadDeclaredIOParameterName(
            CustomAttributeData attribute,
            [NotNullWhen(true)] out string? parameterName)
        {
            parameterName = null;
            if (attribute.ConstructorArguments.Count != 1 ||
                attribute.ConstructorArguments[0].Value is not string candidate ||
                string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            parameterName = candidate;
            return true;
        }

        private readonly record struct DeclaredIOMetadata(
            bool HasTaskAttribute,
            bool HasValidAttributes,
            IReadOnlyList<string> RequiredUnsetParameters);
    }
}
