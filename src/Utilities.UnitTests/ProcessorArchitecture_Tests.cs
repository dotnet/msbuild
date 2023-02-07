// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class ProcessorArchitectureTests
    {
        internal static string ProcessorArchitectureIntToString()
        {
            return NativeMethodsShared.ProcessorArchitecture switch
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

        [Fact]
        public void ValidateProcessorArchitectureStrings()
        {
            // Make sure changes to BuildUtilities.ProcessorArchitecture.cs source don't accidentally get mangle ProcessorArchitecture
            ProcessorArchitecture.X86.ShouldBe("x86"); // "x86 ProcessorArchitecture isn't correct"
            ProcessorArchitecture.IA64.ShouldBe("IA64"); // "IA64 ProcessorArchitecture isn't correct"
            ProcessorArchitecture.AMD64.ShouldBe("AMD64"); // "AMD64 ProcessorArchitecture isn't correct"
            ProcessorArchitecture.MSIL.ShouldBe("MSIL"); // "MSIL ProcessorArchitecture isn't correct"
            ProcessorArchitecture.ARM.ShouldBe("ARM"); // "ARM ProcessorArchitecture isn't correct"
            ProcessorArchitecture.ARM64.ShouldBe("ARM64"); // "ARM ProcessorArchitecture isn't correct"
            ProcessorArchitecture.WASM.ShouldBe("WASM"); // "WASM ProcessorArchitecture isn't correct"
            ProcessorArchitecture.S390X.ShouldBe("S390X"); // "S390X ProcessorArchitecture isn't correct"
            ProcessorArchitecture.LOONGARCH64.ShouldBe("LOONGARCH64"); // "LOONGARCH64 ProcessorArchitecture isn't correct"
            ProcessorArchitecture.ARMV6.ShouldBe("ARMV6"); // "ARMV6 ProcessorArchitecture isn't correct"
            ProcessorArchitecture.PPC64LE.ShouldBe("PPC64LE"); // "PPC64LE ProcessorArchitecture isn't correct"
        }

        [Fact]
        public void ValidateCurrentProcessorArchitectureCall()
        {
            ProcessorArchitecture.CurrentProcessArchitecture.ShouldBe(ProcessorArchitectureIntToString()); // "BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture returned an invalid match"
        }

        [Fact]
        public void ValidateConvertDotNetFrameworkArchitectureToProcessorArchitecture()
        {
            Console.WriteLine("BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture is: {0}", ProcessorArchitecture.CurrentProcessArchitecture);
            string procArchitecture;
            switch (ProcessorArchitecture.CurrentProcessArchitecture)
            {
                case ProcessorArchitecture.ARM:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    procArchitecture.ShouldBe(ProcessorArchitecture.ARM);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);
                    procArchitecture.ShouldBeNull();
                    break;

                case ProcessorArchitecture.ARM64:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);
                    procArchitecture.ShouldBe(ProcessorArchitecture.ARM64);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    procArchitecture.ShouldBe(ProcessorArchitecture.ARM);
                    break;

                case ProcessorArchitecture.X86:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    procArchitecture.ShouldBe(ProcessorArchitecture.X86);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);

                    // We should also allow NULL if the machine is true x86 only.
                    bool isValidResult = procArchitecture?.Equals(ProcessorArchitecture.AMD64) != false || procArchitecture.Equals(ProcessorArchitecture.IA64);

                    isValidResult.ShouldBeTrue();
                    break;

                case ProcessorArchitecture.AMD64:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);
                    procArchitecture.ShouldBe(ProcessorArchitecture.AMD64);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    procArchitecture.ShouldBe(ProcessorArchitecture.X86);
                    break;

                case ProcessorArchitecture.IA64:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);
                    procArchitecture.ShouldBe(ProcessorArchitecture.IA64);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    procArchitecture.ShouldBe(ProcessorArchitecture.X86);
                    break;

                case ProcessorArchitecture.MSIL:
                    throw new InvalidOperationException("We should never hit ProcessorArchitecture.MSIL");

                default:
                    throw new InvalidOperationException("Untested or new ProcessorArchitecture type");
            }
        }
    }
}
