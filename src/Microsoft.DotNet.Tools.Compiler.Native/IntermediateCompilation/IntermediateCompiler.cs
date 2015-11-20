using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
	public class IntermediateCompiler
	{
		public static IntermediateCompiler Create(NativeCompileSettings config)
		{
            var platformStepList = CreatePlatformNativeSteps(config);
			
			return new IntermediateCompiler(platformStepList);
		}
		
		private static List<IPlatformNativeStep> CreatePlatformNativeSteps(NativeCompileSettings config)
		{
			if (config.NativeMode == NativeIntermediateMode.cpp)
			{
				return CreateCppSteps(config);
			}
			else if (config.NativeMode == NativeIntermediateMode.ryujit)
			{
				return CreateJitSteps(config);
			}
            else
            {
                throw new Exception("Unrecognized Mode");
            }
		}
		
		private static List<IPlatformNativeStep> CreateCppSteps(NativeCompileSettings config)
		{
            var stepList = new List<IPlatformNativeStep>();

			if (config.OS == OSMode.Windows)
			{
                stepList.Add(new WindowsCppCompileStep(config));
                stepList.Add(new WindowsLinkStep(config));
			}
			else if (config.OS == OSMode.Linux)
			{
                stepList.Add(new LinuxCppCompileStep(config));
			}
			else if (config.OS == OSMode.Mac)
			{
				stepList.Add(new MacCppCompileStep(config));
			}
			else
			{
				throw new Exception("Unrecognized Operating System. Unable to create Intermediate Compiler.");	
			}

            return stepList;
		}
		
		private static List<IPlatformNativeStep> CreateJitSteps(NativeCompileSettings config)
		{
            var stepList = new List<IPlatformNativeStep>();

            if (config.OS == OSMode.Windows)
			{
                stepList.Add(new WindowsLinkStep(config));
			}
			else if (config.OS == OSMode.Linux)
			{
                stepList.Add(new LinuxRyuJitCompileStep(config));
			}
			else if (config.OS == OSMode.Mac)
			{
				throw new NotImplementedException("Mac RyuJit not supported");
			}
			else
			{
				throw new Exception("Unrecognized Operating System. Unable to create Intermediate Compiler.");	
			}

            return stepList;
		}
		
        private List<IPlatformNativeStep> StepList { get; set; }
		
		private IntermediateCompiler(List<IPlatformNativeStep> stepList)
		{
            if (stepList == null || stepList.Count < 1)
            {
                throw new Exception("Intermediate step list must not be empty.");
            }

            this.StepList = stepList;
		}
		
		public int Invoke()
        {
            foreach(var step in StepList)
            {
                int result = step.Invoke();

                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }
		
		public string DetermineOutputFile(NativeCompileSettings config)
		{
			return config.DetermineFinalOutputPath();
		}
		
		public bool CheckPreReqs()
		{
            var check = true;

            foreach(var step in StepList)
            {
                check = check && step.CheckPreReqs();
            }

            return check;
		}
	}
}