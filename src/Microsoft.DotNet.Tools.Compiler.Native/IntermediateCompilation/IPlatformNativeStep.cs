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
	public interface IPlatformNativeStep
	{
		int Invoke(Config config);
		string DetermineOutputFile(Config config);
		bool CheckPreReqs();
	}
}