using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.Cli.Add.P2P.Tests
{
    internal class TestSetup
    {
        public const string TestGroup = "NonRestoredTestProjects";
        public const string ProjectName = "DotnetAddP2PProjects";

        public string TestRoot { get; private set; }


        private const string ValidRef = "ValidRef";
        public string ValidRefCsprojName => $"{ValidRef}.csproj";
        public string ValidRefCsprojPath => Path.Combine(TestRoot, ValidRef, ValidRefCsprojName);
        public string ValidRefCsprojRelPath => Path.Combine("..", ValidRef, ValidRefCsprojName);


        private const string Lib = "Lib";
        public string LibCsprojName => $"{Lib}.csproj";
        public string LibCsprojPath => Path.Combine(TestRoot, Lib, LibCsprojName);
        public string LibCsprojRelPath => Path.Combine("..", Lib, LibCsprojName);

        

        public TestSetup(string testRoot)
        {
            TestRoot = testRoot;
        }
    }
}
