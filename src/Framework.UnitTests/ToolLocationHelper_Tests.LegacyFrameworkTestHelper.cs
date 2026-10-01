// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    /// <summary>
    /// This class will provide delegates and properties to allow differen combinations of ToolLocationHelper GetDotNetFrameworkPaths and GetReferenceAssemblyPaths to be simulated.
    /// </summary>
    internal sealed class LegacyFrameworkTestHelper
    {
        /// <summary>
        /// Paths which simulate the fact that the frameworks are installed including their reference assemblies
        /// </summary>
        internal const string DotNet40ReferenceAssemblyPath = "C:\\Program Files\\Reference Assemblies\\Framework\\V4.0";
        internal const string DotNet35ReferenceAssemblyPath = "C:\\Program Files\\Reference Assemblies\\Framework\\V3.5";
        internal const string DotNet30ReferenceAssemblyPath = "C:\\Program Files\\Reference Assemblies\\Framework\\V3.0";
        internal const string DotNet20FrameworkPath = "C:\\Microsoft\\.Net Framework\\V2.0.57027";
        internal const string DotNet30FrameworkPath = "C:\\Microsoft\\.Net Framework\\V3.0";
        internal const string DotNet35FrameworkPath = "C:\\Microsoft\\.Net Framework\\V3.5";
        internal const string DotNet40FrameworkPath = "C:\\Microsoft\\.Net Framework\\V4.0";

        /// <summary>
        /// Should the delegate respond with a path or null when asked for Version20 on the delegate which gets the DotNetFrameworkPath
        /// </summary>
        internal bool DotNet20Installed { get; set; }

        /// <summary>
        /// Should the delegate respond with a path or null when asked for Version30 on the delegate which gets the DotNetFrameworkPath
        /// </summary>
        internal bool DotNet30Installed { get; set; }

        /// <summary>
        /// Should the delegate respond with a path or null when asked for Version35 on the delegate which gets the DotNetFrameworkPath
        /// </summary>
        internal bool DotNet35Installed { get; set; }

        /// <summary>
        /// Should the delegate respond with a path or null when asked for Version40 on the delegate which gets the DotNetFrameworkPath
        /// </summary>
        internal bool DotNet40Installed { get; set; }

        /// <summary>
        /// Should the delegate respond with a path or null when asked for Version40 on the delegate which gets the DotNetReferenceAssembliesPath is called
        /// </summary>
        internal bool DotNetReferenceAssemblies40Installed { get; set; }

        /// <summary>
        /// Should the delegate respond with a path or null when asked for Version35 on the delegate which gets the DotNetReferenceAssembliesPath is called
        /// </summary>
        internal bool DotNetReferenceAssemblies35Installed { get; set; }

        /// <summary>
        /// Should the delegate respond with a path or null when asked for Version30 on the delegate which gets the DotNetReferenceAssembliesPath is called
        /// </summary>
        internal bool DotNetReferenceAssemblies30Installed { get; set; }

        /// <summary>
        /// Return a delegate which will return a path or null depending on whether or not frameworks and their reference assembly paths are being simulated as being installed
        /// </summary>
        internal ToolLocationHelper.VersionToPath GetDotNetVersionToPathDelegate => GetDotNetFramework;

        /// <summary>
        /// Return a delegate which will return a path or null depending on whether or not frameworks and their reference assembly paths are being simulated as being installed
        /// </summary>
        internal ToolLocationHelper.VersionToPath GetDotNetReferenceAssemblyDelegate => GetDotNetFrameworkReferenceAssemblies;

        /// <summary>
        /// Return a path to the .net framework reference assemblies if the boolean property said we should return one.
        /// Return null if we should not fake the fact that the framework reference assemblies are installed
        /// </summary>
        private string GetDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion version)
        {
            switch (version)
            {
                case TargetDotNetFrameworkVersion.Version40:
                    {
                        return DotNetReferenceAssemblies40Installed ? DotNet40ReferenceAssemblyPath : null;
                    }
                case TargetDotNetFrameworkVersion.Version35:
                    {
                        return DotNetReferenceAssemblies35Installed ? DotNet35ReferenceAssemblyPath : null;
                    }
                case TargetDotNetFrameworkVersion.Version30:
                    {
                        return DotNetReferenceAssemblies30Installed ? DotNet30ReferenceAssemblyPath : null;
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        /// <summary>
        /// Return a path to the .net framework if the boolean property said we should return one.
        /// Return null if we should not fake the fact that the framework is installed
        /// </summary>
        private string GetDotNetFramework(TargetDotNetFrameworkVersion version)
        {
            switch (version)
            {
                case TargetDotNetFrameworkVersion.Version20:
                    {
                        return DotNet20Installed ? DotNet20FrameworkPath : null;
                    }
                case TargetDotNetFrameworkVersion.Version30:
                    {
                        return DotNet30Installed ? DotNet30FrameworkPath : null;
                    }
                case TargetDotNetFrameworkVersion.Version35:
                    {
                        return DotNet35Installed ? DotNet35FrameworkPath : null;
                    }
                case TargetDotNetFrameworkVersion.Version40:
                    {
                        return DotNet40Installed ? DotNet40FrameworkPath : null;
                    }
                default:
                    {
                        return null;
                    }
            }
        }
    }
}
