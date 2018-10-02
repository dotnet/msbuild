// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using BuildUtilities = Microsoft.Build.Utilities;

namespace Microsoft.Build.UnitTests
{
    public class ProcessorArchitectureTests
    {
        internal static string ProcessorArchitectureIntToString()
        {
            switch (NativeMethodsShared.ProcessorArchitecture)
            {
                case NativeMethodsShared.ProcessorArchitectures.X86:
                    return ProcessorArchitecture.X86;

                case NativeMethodsShared.ProcessorArchitectures.X64:
                    return ProcessorArchitecture.AMD64;

                case NativeMethodsShared.ProcessorArchitectures.IA64:
                    return ProcessorArchitecture.IA64;

                case NativeMethodsShared.ProcessorArchitectures.ARM:
                    return ProcessorArchitecture.ARM;

                // unknown architecture? return null
                default:
                    return null;
            }
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
                    procArchitecture.ShouldBeNull(); // "We should not have any Bitness64 Processor architecture returned in arm"
                    break;

                case ProcessorArchitecture.X86:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    procArchitecture.ShouldBe(ProcessorArchitecture.X86);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);

                    //We should also allow NULL if the machine is true x86 only.
                    bool isValidResult = procArchitecture == null || procArchitecture.Equals(ProcessorArchitecture.AMD64) || procArchitecture.Equals(ProcessorArchitecture.IA64);

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
