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
	public class MacCppCompileStep : IPlatformNativeStep
	{
        public MacCppCompileStep(Config config)
        {
            throw new NotImplementedException("Mac Cpp Not Supported Yet");
        }

        public int Invoke(Config config)
		{
			throw new NotImplementedException("mac cpp Not supported yet.");
		}
		
		public bool CheckPreReqs()
		{
			throw new NotImplementedException("mac cpp Not supported yet.");
		}
		
		public string DetermineOutputFile(Config config)
		{
			throw new NotImplementedException("Mac cpp Not supported yet.");
		}

        public bool RequiresLinkStep()
        {
            return false;
        }		
	}
}