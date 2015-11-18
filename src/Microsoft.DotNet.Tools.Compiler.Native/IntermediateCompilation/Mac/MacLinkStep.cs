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
	public class MacLinkStep : IPlatformNativeStep
	{
		public int Invoke()
		{
			throw new NotImplementedException("Mac linker Not supported yet.");
		}
		
		public bool CheckPreReqs()
		{
			throw new NotImplementedException("Mac linker Not supported yet.");
		}
		
		public string DetermineOutputFile(NativeCompileSettings config)
		{
			throw new NotImplementedException("Mac linker Not supported yet.");
		}
		
	}
}