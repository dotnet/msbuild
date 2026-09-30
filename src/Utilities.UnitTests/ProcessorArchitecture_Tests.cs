// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class ProcessorArchitectureTests
{
    [Fact]
    public void ValidateConvertDotNetFrameworkArchitectureToProcessorArchitecture()
    {
        string procArchitecture;
        switch (ProcessorArchitecture.CurrentProcessArchitecture)
        {
            case ProcessorArchitecture.ARM:
                procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture.Bitness32);
                procArchitecture.ShouldBe(ProcessorArchitecture.ARM);

                procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture.Bitness64);
                procArchitecture.ShouldBeNull();
                break;

            case ProcessorArchitecture.ARM64:
                procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture.Bitness64);
                procArchitecture.ShouldBe(ProcessorArchitecture.ARM64);

                procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture.Bitness32);
                procArchitecture.ShouldBe(ProcessorArchitecture.ARM);
                break;

            case ProcessorArchitecture.X86:
                procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture.Bitness32);
                procArchitecture.ShouldBe(ProcessorArchitecture.X86);

                procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture.Bitness64);

                // We should also allow NULL if the machine is true x86 only.
                bool isValidResult = procArchitecture?.Equals(ProcessorArchitecture.AMD64) != false || procArchitecture.Equals(ProcessorArchitecture.IA64);

                isValidResult.ShouldBeTrue();
                break;

            case ProcessorArchitecture.AMD64:
                procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture.Bitness64);
                procArchitecture.ShouldBe(ProcessorArchitecture.AMD64);

                procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture.Bitness32);
                procArchitecture.ShouldBe(ProcessorArchitecture.X86);
                break;

            case ProcessorArchitecture.IA64:
                procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture.Bitness64);
                procArchitecture.ShouldBe(ProcessorArchitecture.IA64);

                procArchitecture = ToolLocationHelper.ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture.Bitness32);
                procArchitecture.ShouldBe(ProcessorArchitecture.X86);
                break;

            case ProcessorArchitecture.MSIL:
                throw new InvalidOperationException("We should never hit ProcessorArchitecture.MSIL");

            default:
                throw new InvalidOperationException("Untested or new ProcessorArchitecture type");
        }
    }
}
