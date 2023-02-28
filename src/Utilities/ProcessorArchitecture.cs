// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

#nullable disable

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
        /// Represents the WebAssembly platform.
        /// </summary>
        public const string WASM = nameof(WASM);

        /// <summary>
        /// Represents the S390x processor architecture.
        /// </summary>
        public const string S390X = nameof(S390X);

        /// <summary>
        /// Represents the LoongAarch64 processor architecture.
        /// </summary>
        public const string LOONGARCH64 = nameof(LOONGARCH64);

        /// <summary>
        /// Represents the 32-bit ARMv6 processor architecture.
        /// </summary>
        public const string ARMV6 = nameof(ARMV6);

        /// <summary>
        /// Represents the PowerPC 64-bit (little-endian) processor architecture.
        /// </summary>
        public const string PPC64LE = nameof(PPC64LE);

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
            string architecture = NativeMethodsShared.ProcessorArchitecture switch
            {
                NativeMethodsShared.ProcessorArchitectures.X86 => X86,
                NativeMethodsShared.ProcessorArchitectures.X64 => AMD64,
                NativeMethodsShared.ProcessorArchitectures.IA64 => IA64,
                NativeMethodsShared.ProcessorArchitectures.ARM => ARM,
                NativeMethodsShared.ProcessorArchitectures.ARM64 => ARM64,
                NativeMethodsShared.ProcessorArchitectures.WASM => WASM,
                NativeMethodsShared.ProcessorArchitectures.S390X => S390X,
                NativeMethodsShared.ProcessorArchitectures.LOONGARCH64 => LOONGARCH64,
                NativeMethodsShared.ProcessorArchitectures.ARMV6 => ARMV6,
                NativeMethodsShared.ProcessorArchitectures.PPC64LE => PPC64LE,
                // unknown architecture? return null
                _ => null,
            };
            return architecture;
        }
    }
}
