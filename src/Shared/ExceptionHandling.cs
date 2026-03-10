// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Utility methods for classifying and handling exceptions.
    /// </summary>
    internal static class ExceptionHandling
    {
        /// <inheritdoc cref="FrameworkExceptionHandling.IsCriticalException(Exception)"/>
        internal static bool IsCriticalException(Exception e)
            => FrameworkExceptionHandling.IsCriticalException(e);

        /// <inheritdoc cref="FrameworkExceptionHandling.NotExpectedException(Exception)"/>
        internal static bool NotExpectedException(Exception e)
            => FrameworkExceptionHandling.NotExpectedException(e);

        /// <inheritdoc cref="FrameworkExceptionHandling.IsIoRelatedException(Exception)"/>
        internal static bool IsIoRelatedException(Exception e)
            => FrameworkExceptionHandling.IsIoRelatedException(e);

        /// <inheritdoc cref="FrameworkExceptionHandling.IsXmlException(Exception)"/>
        internal static bool IsXmlException(Exception e)
            => FrameworkExceptionHandling.IsXmlException(e);

        /// <inheritdoc cref="FrameworkExceptionHandling.NotExpectedIoOrXmlException(Exception)"/>
        internal static bool NotExpectedIoOrXmlException(Exception e)
            => FrameworkExceptionHandling.NotExpectedIoOrXmlException(e);

        /// <inheritdoc cref="FrameworkExceptionHandling.NotExpectedReflectionException(Exception)"/>
        internal static bool NotExpectedReflectionException(Exception e)
            => FrameworkExceptionHandling.NotExpectedReflectionException(e);

        /// <inheritdoc cref="FrameworkExceptionHandling.NotExpectedSerializationException(Exception)"/>
        internal static bool NotExpectedSerializationException(Exception e)
            => FrameworkExceptionHandling.NotExpectedSerializationException(e);

        /// <inheritdoc cref="FrameworkExceptionHandling.NotExpectedRegistryException(Exception)"/>
        internal static bool NotExpectedRegistryException(Exception e)
            => FrameworkExceptionHandling.NotExpectedRegistryException(e);

        /// <inheritdoc cref="FrameworkExceptionHandling.NotExpectedFunctionException(Exception)"/>
        internal static bool NotExpectedFunctionException(Exception e)
            => FrameworkExceptionHandling.NotExpectedFunctionException(e);
    }
}
