// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using BuildUtilities = Microsoft.Build.Utilities;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class ProcessorArchitectureTests
    {
        internal static string ProcessorArchitectureIntToString()
        {
            switch (NativeMethodsShared.ProcessorArchitecture)
            {
                case NativeMethodsShared.ProcessorArchitectures.X86:
                    return BuildUtilities.ProcessorArchitecture.X86;

                case NativeMethodsShared.ProcessorArchitectures.X64:
                    return BuildUtilities.ProcessorArchitecture.AMD64;

                case NativeMethodsShared.ProcessorArchitectures.IA64:
                    return BuildUtilities.ProcessorArchitecture.IA64;

                case NativeMethodsShared.ProcessorArchitectures.ARM:
                    return BuildUtilities.ProcessorArchitecture.ARM;

                // unknown architecture? return null
                default:
                    return null;
            }
        }

        [Fact]
        public void ValidateProcessorArchitectureStrings()
        {
            // Make sure changes to BuildUtilities.ProcessorArchitecture.cs source don't accidentally get mangle ProcessorArchitecture
            Assert.Equal("x86", BuildUtilities.ProcessorArchitecture.X86); // "x86 ProcessorArchitecture isn't correct"
            Assert.Equal("IA64", BuildUtilities.ProcessorArchitecture.IA64); // "IA64 ProcessorArchitecture isn't correct"
            Assert.Equal("AMD64", BuildUtilities.ProcessorArchitecture.AMD64); // "AMD64 ProcessorArchitecture isn't correct"
            Assert.Equal("MSIL", BuildUtilities.ProcessorArchitecture.MSIL); // "MSIL ProcessorArchitecture isn't correct"
            Assert.Equal("ARM", BuildUtilities.ProcessorArchitecture.ARM); // "ARM ProcessorArchitecture isn't correct"
        }

        [Fact]
        public void ValidateCurrentProcessorArchitectureCall()
        {
            Assert.Equal(ProcessorArchitectureIntToString(), BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture); // "BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture returned an invalid match"
        }

        [Fact]
        public void ValidateConvertDotNetFrameworkArchitectureToProcessorArchitecture()
        {
            Console.WriteLine("BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture is: {0}", BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture);
            string procArchitecture;
            switch (BuildUtilities.ProcessorArchitecture.CurrentProcessArchitecture)
            {
                case BuildUtilities.ProcessorArchitecture.ARM:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    Assert.Equal(BuildUtilities.ProcessorArchitecture.ARM, procArchitecture);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);
                    Assert.Null(procArchitecture); // "We should not have any Bitness64 Processor architecture returned in arm"
                    break;

                case BuildUtilities.ProcessorArchitecture.X86:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    Assert.Equal(BuildUtilities.ProcessorArchitecture.X86, procArchitecture);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);

                    //We should also allow NULL if the machine is true x86 only.
                    bool isValidResult = procArchitecture == null ? true : procArchitecture.Equals(BuildUtilities.ProcessorArchitecture.AMD64) || procArchitecture.Equals(BuildUtilities.ProcessorArchitecture.IA64);

                    Assert.True(isValidResult);
                    break;

                case BuildUtilities.ProcessorArchitecture.AMD64:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);
                    Assert.Equal(BuildUtilities.ProcessorArchitecture.AMD64, procArchitecture);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    Assert.Equal(BuildUtilities.ProcessorArchitecture.X86, procArchitecture);
                    break;

                case BuildUtilities.ProcessorArchitecture.IA64:
                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness64);
                    Assert.Equal(BuildUtilities.ProcessorArchitecture.IA64, procArchitecture);

                    procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(Utilities.DotNetFrameworkArchitecture.Bitness32);
                    Assert.Equal(BuildUtilities.ProcessorArchitecture.X86, procArchitecture);
                    break;

                case BuildUtilities.ProcessorArchitecture.MSIL:
                    Assert.True(false, "We should never hit ProcessorArchitecture.MSIL");
                    break;

                default:
                    Assert.True(false, "Untested or new ProcessorArchitecture type");
                    break;
            }
        }
    }
}
