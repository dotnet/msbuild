// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Processor architecture utilities
    /// </summary>
    public static class ProcessorArchitecture
    {
        /// <summary>
        /// Represents the 32-bit x86 processor architecture.
        /// </summary>
        public const string X86 = "x86";
        /// <summary>
        /// Represents the 64-bit IA64 processor architecture.
        /// </summary>
        public const string IA64 = nameof(IA64);

        /// <summary>
        /// Represents the 64-bit AMD64 processor architecture.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "AMD", Justification = "This is the correct casing for ProcessorArchitecture")]
        public const string AMD64 = nameof(AMD64);

        /// <summary>
        /// Represents the Microsoft Intermediate Language processor architecture.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "MSIL", Justification = "This is the correct casing for ProcessorArchitecture")]
        public const string MSIL = nameof(MSIL);

        /// <summary>
        /// Represents the ARM processor architecture.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "ARM", Justification = "This is the correct casing for ProcessorArchitecture")]
        public const string ARM = nameof(ARM);

        /// <summary>
        /// Represents the ARM64 processor architecture.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "ARM64", Justification = "This is the correct casing for ProcessorArchitecture")]
        public const string ARM64 = nameof(ARM64);

        /// <summary>
        /// Lazy-initted property for getting the architecture of the currently running process
        /// </summary>
        public static string CurrentProcessArchitecture => GetCurrentProcessArchitecture();

        /// <summary>
        /// Gets the processor architecture of the currently running process
        /// </summary>
        /// <returns>null if unknown architecture or error, one of the known architectures otherwise</returns>
        private static string GetCurrentProcessArchitecture()
        {
            string architecture;

            switch (NativeMethodsShared.ProcessorArchitecture)
            {
                case NativeMethodsShared.ProcessorArchitectures.X86:
                    architecture = X86;
                    break;

                case NativeMethodsShared.ProcessorArchitectures.X64:
                    architecture = AMD64;
                    break;

                case NativeMethodsShared.ProcessorArchitectures.IA64:
                    architecture = IA64;
                    break;

                case NativeMethodsShared.ProcessorArchitectures.ARM:
                    architecture = ARM;
                    break;

                case NativeMethodsShared.ProcessorArchitectures.ARM64:
                    architecture = ARM64;
                    break;

                // unknown architecture? return null
                default:
                    architecture = null;
                    break;
            }

            return architecture;
        }
    }
}
