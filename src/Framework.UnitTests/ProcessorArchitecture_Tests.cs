// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ProcessorArchitectureTests
{
    [Fact]
    public void ValidateProcessorArchitectureStrings()
    {
        // Make sure changes to BuildUtilities.ProcessorArchitecture.cs source don't accidentally get mangle ProcessorArchitecture
        ProcessorArchitecture.X86.ShouldBe("x86"); // "x86 ProcessorArchitecture isn't correct"
        ProcessorArchitecture.IA64.ShouldBe("IA64"); // "IA64 ProcessorArchitecture isn't correct"
        ProcessorArchitecture.AMD64.ShouldBe("AMD64"); // "AMD64 ProcessorArchitecture isn't correct"
        ProcessorArchitecture.MSIL.ShouldBe("MSIL"); // "MSIL ProcessorArchitecture isn't correct"
        ProcessorArchitecture.ARM.ShouldBe("ARM"); // "ARM ProcessorArchitecture isn't correct"
        ProcessorArchitecture.ARM64.ShouldBe("ARM64"); // "ARM64 ProcessorArchitecture isn't correct"
        ProcessorArchitecture.WASM.ShouldBe("WASM"); // "WASM ProcessorArchitecture isn't correct"
        ProcessorArchitecture.S390X.ShouldBe("S390X"); // "S390X ProcessorArchitecture isn't correct"
        ProcessorArchitecture.LOONGARCH64.ShouldBe("LOONGARCH64"); // "LOONGARCH64 ProcessorArchitecture isn't correct"
        ProcessorArchitecture.ARMV6.ShouldBe("ARMV6"); // "ARMV6 ProcessorArchitecture isn't correct"
        ProcessorArchitecture.PPC64LE.ShouldBe("PPC64LE"); // "PPC64LE ProcessorArchitecture isn't correct"
    }

    [Fact]
    public void ValidateCurrentProcessorArchitectureCall()
        => ProcessorArchitecture.CurrentProcessArchitecture.ShouldBe(ProcessorArchitectureIntToString());

    private static string? ProcessorArchitectureIntToString()
        => NativeMethodsShared.ProcessorArchitecture switch
        {
            NativeMethodsShared.ProcessorArchitectures.X86 => ProcessorArchitecture.X86,
            NativeMethodsShared.ProcessorArchitectures.X64 => ProcessorArchitecture.AMD64,
            NativeMethodsShared.ProcessorArchitectures.IA64 => ProcessorArchitecture.IA64,
            NativeMethodsShared.ProcessorArchitectures.ARM => ProcessorArchitecture.ARM,
            NativeMethodsShared.ProcessorArchitectures.ARM64 => ProcessorArchitecture.ARM64,
            NativeMethodsShared.ProcessorArchitectures.WASM => ProcessorArchitecture.WASM,
            NativeMethodsShared.ProcessorArchitectures.S390X => ProcessorArchitecture.S390X,
            NativeMethodsShared.ProcessorArchitectures.LOONGARCH64 => ProcessorArchitecture.LOONGARCH64,
            NativeMethodsShared.ProcessorArchitectures.ARMV6 => ProcessorArchitecture.ARMV6,
            NativeMethodsShared.ProcessorArchitectures.PPC64LE => ProcessorArchitecture.PPC64LE,

            // unknown architecture? return null
            _ => null,
        };
}
