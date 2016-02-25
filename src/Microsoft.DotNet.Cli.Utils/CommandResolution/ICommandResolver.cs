using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Utils
{
    public interface ICommandResolver
    {
        CommandSpec Resolve(CommandResolverArguments arguments);
    }
}
