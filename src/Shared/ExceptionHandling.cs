// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.Debugging;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Utility methods for classifying and handling exceptions.
    /// </summary>
    internal static class ExceptionHandling
    {
        /// <inheritdoc cref="DebugUtils.ResetDebugDumpPathInRunningTests"/>
        internal static bool ResetDebugDumpPathInRunningTests
        {
            get => DebugUtils.ResetDebugDumpPathInRunningTests;
            set => DebugUtils.ResetDebugDumpPathInRunningTests = value;
        }

        /// <inheritdoc cref="DebugUtils.DebugDumpPath"/>
        internal static string DebugDumpPath => DebugUtils.DebugDumpPath;

        /// <inheritdoc cref="DebugUtils.DumpFilePath"/>
        internal static string DumpFilePath => DebugUtils.DumpFilePath;

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

        /// <inheritdoc cref="DebugUtils.UnhandledExceptionHandler"/>
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "It is called by the CLR")]
        internal static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
            => DebugUtils.UnhandledExceptionHandler(sender, e);

        /// <inheritdoc cref="DebugUtils.DumpExceptionToFile"/>
        internal static void DumpExceptionToFile(Exception ex)
            => DebugUtils.DumpExceptionToFile(ex);

        /// <inheritdoc cref="DebugUtils.ReadAnyExceptionFromFile"/>
        internal static string ReadAnyExceptionFromFile(DateTime fromTimeUtc)
            => DebugUtils.ReadAnyExceptionFromFile(fromTimeUtc);
    }
}
