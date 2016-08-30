using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public class ConstantPackageNames
    {
        public const string CSdkPackageName = "Microsoft.DotNet.Core.Sdk";
    }
}