using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Cli.Build.Framework
{
	public class BuildTarget
	{
		public string Name { get; }
        public string Source { get; }
		public IEnumerable<string> Dependencies { get; }
		public Func<BuildTargetContext, BuildTargetResult> Body { get; }

        public BuildTarget(string name, string source) : this(name, source, Enumerable.Empty<string>(), null) { }
        public BuildTarget(string name, string source, IEnumerable<string> dependencies) : this(name, source, dependencies, null) { }
		public BuildTarget(string name, string source, IEnumerable<string> dependencies, Func<BuildTargetContext, BuildTargetResult> body)
		{
			Name = name;
            Source = source;
			Dependencies = dependencies;
			Body = body;
		}
	}
}