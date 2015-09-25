// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;
using Microsoft.Build.Shared;
using System.Collections.Generic;
using LogExclusionReason = Microsoft.Build.Tasks.ReferenceTable.LogExclusionReason;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
using SystemProcessorArchitecture = System.Reflection.ProcessorArchitecture;
using System.Linq;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    public class ResolveAssemblyReferenceTestFixture : IDisposable
    {
        // Create the mocks.
        internal static Microsoft.Build.Shared.FileExists fileExists = new Microsoft.Build.Shared.FileExists(FileExists);
        internal static Microsoft.Build.Shared.DirectoryExists directoryExists = new Microsoft.Build.Shared.DirectoryExists(DirectoryExists);
        internal static Microsoft.Build.Tasks.GetDirectories getDirectories = new Microsoft.Build.Tasks.GetDirectories(GetDirectories);
        internal static Microsoft.Build.Tasks.GetAssemblyName getAssemblyName = new Microsoft.Build.Tasks.GetAssemblyName(GetAssemblyName);
        internal static Microsoft.Build.Tasks.GetAssemblyMetadata getAssemblyMetadata = new Microsoft.Build.Tasks.GetAssemblyMetadata(GetAssemblyMetadata);
        internal static Microsoft.Build.Shared.GetRegistrySubKeyNames getRegistrySubKeyNames = new Microsoft.Build.Shared.GetRegistrySubKeyNames(GetRegistrySubKeyNames);
        internal static Microsoft.Build.Shared.GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue = new Microsoft.Build.Shared.GetRegistrySubKeyDefaultValue(GetRegistrySubKeyDefaultValue);
        internal static Microsoft.Build.Tasks.GetLastWriteTime getLastWriteTime = new Microsoft.Build.Tasks.GetLastWriteTime(GetLastWriteTime);
        internal static Microsoft.Build.Tasks.GetAssemblyRuntimeVersion getRuntimeVersion = new Microsoft.Build.Tasks.GetAssemblyRuntimeVersion(GetRuntimeVersion);
        internal static Microsoft.Build.Tasks.GetAssemblyPathInGac checkIfAssemblyIsInGac = new Microsoft.Build.Tasks.GetAssemblyPathInGac(GetPathForAssemblyInGac);
        internal static Microsoft.Build.Shared.OpenBaseKey openBaseKey = new Microsoft.Build.Shared.OpenBaseKey(GetBaseKey);
        internal Microsoft.Build.UnitTests.MockEngine.GetStringDelegate resourceDelegate = new Microsoft.Build.UnitTests.MockEngine.GetStringDelegate(AssemblyResources.GetString);
        internal static Microsoft.Build.Tasks.IsWinMDFile isWinMDFile = new Microsoft.Build.Tasks.IsWinMDFile(IsWinMDFile);
        internal static Microsoft.Build.Tasks.ReadMachineTypeFromPEHeader readMachineTypeFromPEHeader = new Microsoft.Build.Tasks.ReadMachineTypeFromPEHeader(ReadMachineTypeFromPEHeader);

        // Performance checks.
        internal static Hashtable uniqueFileExists = null;
        internal static Hashtable uniqueGetAssemblyName = null;
        internal static bool useFrameworkFileExists = false;
        internal const string REDISTLIST = @"<FileList  Redist=""Microsoft-Windows-CLRCoreComp.4.0"" Name="".NET Framework 4"" RuntimeVersion=""4.0"" ToolsVersion=""12.0"">
  <File AssemblyName=""Accessibility"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""CustomMarshalers"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""ISymWrapper"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build.Conversion.v4.0"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build.Engine"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build.Framework"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build.Tasks.v4.0"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build.Utilities.v4.0"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.CSharp"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.JScript"" Version=""10.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.VisualBasic"" Version=""10.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.VisualBasic.Compatibility"" Version=""10.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.VisualBasic.Compatibility.Data"" Version=""10.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.VisualC"" Version=""10.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.VisualC.STLCLR"" Version=""2.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""mscorlib"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationBuildTasks"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationCore"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationFramework.Aero"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationFramework.Classic"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationFramework"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationFramework.Luna"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationFramework.Royale"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""ReachFramework"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""sysglobl"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Activities"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Activities.Core.Presentation"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Activities.DurableInstancing"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Activities.Presentation"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.AddIn.Contract"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.AddIn"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ComponentModel.Composition"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ComponentModel.DataAnnotations"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Configuration"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Configuration.Install"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Core"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.DataSetExtensions"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Entity.Design"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Entity"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Linq"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.OracleClient"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Services.Client"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Services.Design"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Services"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.SqlXml"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Deployment"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Design"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Device"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.DirectoryServices.AccountManagement"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.DirectoryServices"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.DirectoryServices.Protocols"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Drawing.Design"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Drawing"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Dynamic"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.EnterpriseServices"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.IdentityModel"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.IdentityModel.Selectors"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" /> 
  <File AssemblyName=""System.IO.Log"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Management"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Management.Instrumentation"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Messaging"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Net"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Numerics"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Printing"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Runtime.DurableInstancing"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Runtime.Caching"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Runtime.Remoting"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Runtime.Serialization"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Runtime.Serialization.Formatters.Soap"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Security"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Activation"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Activities"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Channels"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Discovery"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Routing"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Web"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceProcess"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Speech"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" /> 
  <File AssemblyName=""System.Transactions"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Abstractions"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.ApplicationServices"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.DataVisualization.Design"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.DataVisualization"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.DynamicData.Design"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.DynamicData"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Entity.Design"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Entity"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Extensions.Design"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Extensions"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Mobile"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.RegularExpressions"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Routing"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Services"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Windows.Forms.DataVisualization.Design"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Windows.Forms.DataVisualization"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Windows.Forms"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Windows.Input.Manipulations"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Windows.Presentation"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Workflow.Activities"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Workflow.ComponentModel"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Workflow.Runtime"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.WorkflowServices"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Xaml"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Xml"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Xml.Linq"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""UIAutomationClient"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""UIAutomationClientsideProviders"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""UIAutomationProvider"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""UIAutomationTypes"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""WindowsBase"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""WindowsFormsIntegration"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""XamlBuildTask"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
</FileList>"
;

        public ResolveAssemblyReferenceTestFixture()
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", "1");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", null);
        }

        /// <summary>
        /// Search paths to use.
        /// </summary>
        private static readonly string[] s_defaultPaths = new string[]
        {
            "{RawFileName}",
            "{CandidateAssemblyFiles}",
            @"c:\MyProject",
            @"c:\MyComponents\misc\",
            @"c:\MyComponents\1.0",
            @"c:\MyComponents\2.0",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion",
            @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
            "{AssemblyFolders}",
            "{HintPathFromItem}"
        };

        /// <summary>
        /// Return the default search paths.
        /// </summary>
        /// <value></value>
        internal string[] DefaultPaths
        {
            get { return s_defaultPaths; }
        }

        /// <summary>
        /// Start monitoring IO calls.
        /// </summary>
        internal void StartIOMonitoring()
        {
            // If tables are present then the corresponding IO function will do some monitoring.
            uniqueFileExists = new Hashtable();
            uniqueGetAssemblyName = new Hashtable();
        }

        /// <summary>
        /// Stop monitoring IO calls and assert if any unnecessary IO was used.
        /// </summary>
        internal void StopIOMonitoringAndAssert_Minimal_IOUse()
        {
            // Check for minimal IO in File.Exists.
            foreach (DictionaryEntry entry in uniqueFileExists)
            {
                string path = (string)entry.Key;
                int count = (int)entry.Value;
                if (count > 1)
                {
                    string message = String.Format("File.Exists() was called {0} times with path {1}.", count, path);
                    Assert.True(false, message);
                }
            }


            uniqueFileExists = null;
            uniqueGetAssemblyName = null;
        }

        /// <summary>
        /// Stop monitoring IO calls and assert if any IO was used.
        /// </summary>
        internal void StopIOMonitoringAndAssert_Zero_IOUse()
        {
            // Check for minimal IO in File.Exists.
            foreach (DictionaryEntry entry in uniqueFileExists)
            {
                string path = (string)entry.Key;
                int count = (int)entry.Value;
                if (count > 0)
                {
                    string message = String.Format("File.Exists() was called {0} times with path {1}.", count, path);
                    Assert.True(false, message);
                }
            }


            // Check for zero IO in GetAssemblyName.
            foreach (DictionaryEntry entry in uniqueGetAssemblyName)
            {
                string path = (string)entry.Key;
                int count = (int)entry.Value;
                if (count > 0)
                {
                    string message = String.Format("GetAssemblyName() was called {0} times with path {1}.", count, path);
                    Assert.True(false, message);
                }
            }

            uniqueFileExists = null;
            uniqueGetAssemblyName = null;
        }

        private static List<string> s_existentFiles = new List<string>
            {
                @"c:\Frameworks\DependsOnFoo4Framework.dll",
                @"c:\Frameworks\DependsOnFoo45Framework.dll",
                @"c:\Frameworks\DependsOnFoo35Framework.dll",
                @"c:\Frameworks\IndirectDependsOnFoo45Framework.dll",
                @"c:\Frameworks\IndirectDependsOnFoo4Framework.dll",
                @"c:\Frameworks\IndirectDependsOnFoo35Framework.dll",
                Path.Combine(Path.GetTempPath(), @"RawFileNameRelative\System.Xml.dll"),
                Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles\System.Xml.dll"),
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Data.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.pdb",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.xml",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en\System.Xml.resources.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en\System.Xml.resources.pdb",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en\System.Xml.resources.config",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\xx\System.Xml.resources.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en-GB\System.Xml.resources.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en-GB\System.Xml.resources.pdb",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en-GB\System.Xml.resources.config",
                @"c:\MyProject\MyPrivateAssembly.exe",
                @"c:\MyProject\MyCopyLocalAssembly.dll",
                @"c:\MyProject\MyDontCopyLocalAssembly.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\BadImage.dll",            // An assembly that will give a BadImageFormatException from GetAssemblyName
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\BadImage.pdb",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\MyGacAssembly.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\MyGacAssembly.pdb",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\xx\MyGacAssembly.resources.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion\System.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v9.0.MyVersion\System.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\mscorlib.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll",
                @"C:\myassemblies\My.Assembly.dll",
                @"c:\MyProject\mscorlib.dll",                                            // This is an mscorlib.dll that has no metadata (i.e. GetAssemblyName returns null)
                @"c:\MyProject\System.Data.dll",                                         // This is a System.Data.dll that has the wrong pkt, it shouldn't be matched.
                @"C:\MyComponents\MyGrid.dll",                                           // A vendor component that we should find in the registry.
                @"C:\MyComponentsA\CustomComponent.dll",                                           // A vendor component that we should find in the registry.
                @"C:\MyComponentsB\CustomComponent.dll",                                           // A vendor component that we should find in the registry.
                @"C:\MyWinMDComponents7\MyGridWinMD.winmd",
                @"C:\MyWinMDComponents9\MyGridWinMD.winmd",
                @"C:\MyWinMDComponents\MyGridWinMD.winmd",
                @"C:\MyWinMDComponents2\MyGridWinMD.winmd",
                @"C:\MyWinMDComponentsA\CustomComponentWinMD.winmd",
                @"C:\MyWinMDComponentsB\CustomComponentWinMD.winmd",
                @"C:\MyWinMDComponentsVv1\MyGridWinMD2.winmd",
                @"C:\MyWinMDComponentsV1\MyGridWinMD3.winmd",
                @"C:\MyRawDropControls\MyRawDropControl.dll",                             // A control installed by VSREG under v2.0.x86chk
                @"C:\MyComponents\HKLM Components\MyHKLMControl.dll",                    // A vendor component that is installed under HKLM but not HKCU.
                @"C:\MyComponents\HKCU Components\MyHKLMandHKCUControl.dll",             // A vendor component that is installed under HKLM and HKCU.
                @"C:\MyComponents\HKLM Components\MyHKLMandHKCUControl.dll",             // A vendor component that is installed under HKLM and HKCU.
                @"C:\MyWinMDComponents\HKLM Components\MyHKLMControlWinMD.winmd",                    // A vendor component that is installed under HKLM but not HKCU.
                @"C:\MyWinMDComponents\HKCU Components\MyHKLMandHKCUControlWinMD.winmd",             // A vendor component that is installed under HKLM and HKCU.
                @"C:\MyWinMDComponents\HKLM Components\MyHKLMandHKCUControlWinMD.winmd",             // A vendor component that is installed under HKLM and HKCU.
                @"C:\MyComponents\v3.0\MyControlWithFutureTargetNDPVersion.dll",         // The future version of a component.
                @"C:\MyComponents\v2.0\MyControlWithFutureTargetNDPVersion.dll",         // The current version of a component.
                @"C:\MyComponents\v1.0\MyNDP1Control.dll",                               // A control that only has an NDP 1.0 version
                @"C:\MyComponents\v2.0\MyControlWithPastTargetNDPVersion.dll",           // The current version of a component.
                @"C:\MyComponents\v1.0\MyControlWithPastTargetNDPVersion.dll",           // The past version of a component.
                @"C:\MyComponentServicePack\MyControlWithServicePack.dll",               // The service pack 1 version of the control
                @"C:\MyComponentBase\MyControlWithServicePack.dll",                      // The non-service pack version of the control.
                @"C:\MyComponentServicePack2\MyControlWithServicePack.dll",              // The service pack 1 version of the control
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll",  // A devices mscorlib. 
                @"c:\MyLibraries\A.dll",
                @"c:\MyExecutableLibraries\A.exe",
                @"c:\MyLibraries\B.dll",
                @"c:\MyLibraries\C.dll",
                @"c:\MyLibraries\v1\D.dll",
                @"c:\MyLibraries\v1\E\E.dll",
                @"c:\RogueLibraries\v1\D.dll",
                @"c:\MyLibraries\v2\D.dll",
                @"c:\MyStronglyNamed\A.dll",
                @"c:\MyWeaklyNamed\A.dll",
                @"c:\MyInaccessible\A.dll",
                @"c:\MyNameMismatch\Foo.dll",
                @"c:\MyEscapedName\=A=.dll",
                @"c:\MyEscapedName\__'ASP'dw0024ry.dll",
                @"c:\MyApp\DependsOnSimpleA.dll",
                @"C:\Regress312873\a.dll",
                @"C:\Regress312873\b.dll",
                @"C:\Regress312873-2\a.dll",
                @"C:\Regress275161\a.dll",
                @"C:\Regress317975\a.dll",
                @"C:\Regress317975\b.dll",
                @"C:\Regress317975\v2\b.dll",
                @"c:\Regress313086\mscorlib.dll",
                @"c:\V1Control\MyDeviceControlAssembly.dll",
                @"c:\V1ControlSP1\MyDeviceControlAssembly.dll",
                @"C:\Regress339786\FolderA\a.dll",
                @"C:\Regress339786\FolderA\c.dll", // v1 of c
                @"C:\Regress339786\FolderB\b.dll",
                @"C:\Regress339786\FolderB\c.dll", // v2 of c
                @"c:\OldClrBug\MyFileLoadExceptionAssembly.dll",
                @"c:\OldClrBug\DependsMyFileLoadExceptionAssembly.dll",
                @"c:\Regress563286\DependsOnBadImage.dll",
                @"C:\Regress407623\CrystalReportsAssembly.dll",
                @"C:\Regress435487\microsoft.build.engine.dll",
                @"C:\Regress313747\Microsoft.Office.Interop.Excel.dll",
                @"C:\Regress313747\MS.Internal.Test.Automation.Office.Excel.dll",
                @"C:\Regress442570\A.dll",
                @"C:\Regress442570\B.dll",
                @"C:\Regress454863\A.dll",
                @"C:\Regress454863\B.dll",
                @"C:\Regress393931\A.metadata_dll",
                @"c:\Regress387218\A.dll",
                @"c:\Regress387218\B.dll",
                @"c:\Regress387218\v1\D.dll",
                @"c:\Regress387218\v2\D.dll",
                @"c:\Regress390219\A.dll",
                @"c:\Regress390219\B.dll",
                @"c:\Regress390219\v1\D.dll",
                @"c:\Regress390219\v2\D.dll",
                @"c:\Regress315619\A\MyAssembly.dll",
                @"c:\Regress315619\B\MyAssembly.dll",
                @"c:\SGenDependeicies\mycomponent.dll",
                @"c:\SGenDependeicies\mycomponent.XmlSerializers.dll",
                @"c:\SGenDependeicies\mycomponent2.dll",
                @"c:\SGenDependeicies\mycomponent2.XmlSerializers.dll",
                @"c:\Regress315619\A\MyAssembly.dll",
                @"c:\Regress315619\B\MyAssembly.dll",
                @"c:\MyRedist\MyRedistRootAssembly.dll",
                @"c:\MyRedist\MyOtherAssembly.dll",
                @"c:\MyRedist\MyThirdAssembly.dll",
                // ==[Related File Extensions Testing]================================================================================================
                @"C:\AssemblyFolder\SomeAssembly.dll",
                @"C:\AssemblyFolder\SomeAssembly.pdb",
                @"C:\AssemblyFolder\SomeAssembly.xml",
                @"C:\AssemblyFolder\SomeAssembly.pri",
                @"C:\AssemblyFolder\SomeAssembly.licenses",
                @"C:\AssemblyFolder\SomeAssembly.config",
                // ==[Related File Extensions Testing]================================================================================================
                
                // ==[Unification Testing]============================================================================================================
                //@"C:\MyComponents\v0.5\UnifyMe.dll",                                 // For unification testing, a version that doesn't exist.
                @"C:\MyComponents\v1.0\UnifyMe.dll",
                @"C:\MyComponents\v2.0\UnifyMe.dll",
                @"C:\MyComponents\v3.0\UnifyMe.dll",
                //@"C:\MyComponents\v4.0\UnifyMe.dll",
                @"C:\MyApp\v0.5\DependsOnUnified.dll",
                @"C:\MyApp\v1.0\DependsOnUnified.dll",
                @"C:\MyApp\v2.0\DependsOnUnified.dll",
                @"C:\MyApp\v3.0\DependsOnUnified.dll",
                @"C:\MyApp\DependsOnWeaklyNamedUnified.dll",
                @"C:\MyApp\v1.0\DependsOnEverettSystem.dll",
                @"C:\Framework\Everett\System.dll",
                @"C:\Framework\Whidbey\System.dll",
                // ==[Unification Testing]============================================================================================================

                // ==[Test assemblies reference higher versions than the current target framework=====================================================
                @"c:\MyComponents\misc\DependsOnOnlyv4Assemblies.dll",  // Only depends on 4.0.0 assemblies
                @"c:\MyComponents\misc\ReferenceVersion9.dll", //Is in redist list and is a 9.0 assembly
                @"c:\MyComponents\misc\DependsOn9.dll", //Depends on 9.0 assemblies
                @"c:\MyComponents\misc\DependsOn9Also.dll", // Depends on 9.0 assemblies
                @"c:\MyComponents\1.0\DependsOn9.dll", // Depends on 9.0 assemblies
                @"c:\MyComponents\2.0\DependsOn9.dll", // Depends on 9.0 assemblies
                @"c:\Regress444809\A.dll",
                @"c:\Regress444809\v2\A.dll",
                @"c:\Regress444809\B.dll",
                @"c:\Regress444809\C.dll",
                @"c:\Regress444809\D.dll",
                @"c:\MyComponents\4.0Component\DependsOnOnlyv4Assemblies.dll",
                @"C:\Regress714052\MSIL\a.dll",
                @"C:\Regress714052\X86\a.dll",
                @"C:\Regress714052\NONE\a.dll",
                @"C:\Regress714052\Mix\a.dll",
                @"C:\Regress714052\Mix\a.winmd",
                @"C:\Regress714052\MSIL\b.dll",
                @"C:\Regress714052\X86\b.dll",
                @"C:\Regress714052\NONE\b.dll",
                @"C:\Regress714052\Mix\b.dll",
                @"C:\Regress714052\Mix\b.winmd",

                @"C:\MyComponents\V.dll",
                @"C:\MyComponents2\W.dll",
                @"C:\MyComponents\X.dll",
                @"C:\MyComponents\Y.dll",
                @"C:\MyComponents\Z.dll",

                @"C:\MyComponents\Microsoft.Build.dll",
                @"C:\MyComponents\DependsOnMSBuild12.dll",

                // WinMD sample files
                @"C:\WinMD\v4\mscorlib.dll",  // Fake 4.0 mscorlib so we can actually resolve it for one of the tests. With a version of 4
                @"C:\WinMD\v255\mscorlib.dll",  // Fake 4.0 mscorlib so we can actually resolve it for one of the tests. With a version of 255
                @"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll",
                @"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll",
                @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeAndCLR.dll",
                @"C:\WinMD\SampleWindowsRuntimeAndOther.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeOnly.dll",
                @"C:\WinMD\SampleWindowsRuntimeOnly.pri",
                @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd",
                @"C:\WinMD\SampleClrOnly.Winmd",
                @"C:\WinMD\SampleBadWindowsRuntime.Winmd",
                @"C:\WinMD\WinMDWithVersion255.Winmd",
                @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.Winmd",
                @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.dll",
                @"C:\WinMDArchVerification\DependsOnAmd64.Winmd",
                @"C:\WinMDArchVerification\DependsOnAmd64.dll",
                @"C:\WinMDArchVerification\DependsOnArm.Winmd",
                @"C:\WinMDArchVerification\DependsOnArm.dll",
                @"C:\WinMDArchVerification\DependsOnArmv7.Winmd",
                @"C:\WinMDArchVerification\DependsOnArmv7.dll",
                @"C:\WinMDArchVerification\DependsOnX86.Winmd",
                @"C:\WinMDArchVerification\DependsOnX86.dll",
                @"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.Winmd",
                @"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.dll",
                @"C:\WinMDArchVerification\DependsOnIA64.Winmd",
                @"C:\WinMDArchVerification\DependsOnIA64.dll",
                @"C:\WinMDArchVerification\DependsOnUnknown.Winmd",
                @"C:\WinMDArchVerification\DependsOnUnknown.dll",
                @"C:\FakeSDK\References\Debug\X86\DebugX86SDKWinMD.Winmd",
                @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKWinMD.Winmd",
                @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKWinMD.Winmd",
                @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKWinMD.Winmd",
                @"C:\FakeSDK\References\Debug\X86\DebugX86SDKRA.dll",
                @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKRA.dll",
                @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKRA.dll",
                @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKRA.dll",
                @"C:\FakeSDK\References\Debug\X86\SDKReference.dll",
                @"C:\DirectoryContainsOnlyDll\a.dll",
                @"C:\DirectoryContainsdllAndWinmd\b.dll",
                @"C:\DirectoryContainsdllAndWinmd\c.winmd",
                @"C:\DirectoryContainstwoWinmd\a.winmd",
                @"C:\DirectoryContainstwoWinmd\c.winmd",
                @"C:\SystemRuntime\System.Runtime.dll",
                @"C:\SystemRuntime\Portable.dll",
                @"C:\SystemRuntime\Regular.dll",
            };

        /// <summary>
        /// Mocked up GetFiles.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static string[] GetFiles(string path, string pattern)
        {
            if (path.Length > 240)
            {
                throw new PathTooLongException();
            }

            string extension = null;
            if (pattern == "*.xml")
            {
                extension = ".xml";
            }
            else if (pattern == "*.pdb")
            {
                extension = ".pdb";
            }
            else
            {
                Assert.True(false, "Unsupported GetFiles pattern " + pattern);
            }

            ArrayList matches = new ArrayList();
            foreach (string file in s_existentFiles)
            {
                string baseDir = Path.GetDirectoryName(file);

                if (0 == String.Compare(baseDir, path, StringComparison.OrdinalIgnoreCase))
                {
                    string fileExtension = Path.GetExtension(file);

                    if (0 == String.Compare(fileExtension, extension, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(file);
                    }
                }
            }

            return (string[])matches.ToArray(typeof(string));
        }

        /// <summary>
        /// Reads the machine type out of the PEHeader of the native dll
        /// </summary>
        private static UInt16 ReadMachineTypeFromPEHeader(string dllPath)
        {
            if (@"C:\WinMDArchVerification\DependsOnInvalidPeHeader.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_INVALID;
            }
            else if (@"C:\WinMDArchVerification\DependsOnAmd64.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_AMD64;
            }
            else if (@"C:\WinMDArchVerification\DependsOnX86.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_I386;
            }
            else if (@"C:\WinMDArchVerification\DependsOnArm.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_ARM;
            }
            else if (@"C:\WinMDArchVerification\DependsOnArmV7.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_ARMV7;
            }
            else if (@"C:\WinMDArchVerification\DependsOnIA64.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_IA64;
            }
            else if (@"C:\WinMDArchVerification\DependsOnUnknown.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_R4000;
            }
            else if (@"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_UNKNOWN;
            }
            else if (@"C:\WinMD\SampleWindowsRuntimeOnly.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_I386;
            }

            return NativeMethods.IMAGE_FILE_MACHINE_INVALID;
        }

        /// <summary>
        ///  Checks to see if the file is a winmd file.
        /// </summary>
        private static bool IsWinMDFile(string fullPath, GetAssemblyRuntimeVersion getAssemblyRuntimeVersion, FileExists fileExists, out string imageRuntimeVersion, out bool isManagedWinMD)
        {
            imageRuntimeVersion = getAssemblyRuntimeVersion(fullPath);
            isManagedWinMD = false;

            if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                isManagedWinMD = true;
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\WinMDWithVersion255.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (fullPath.StartsWith(@"C:\MyWinMDComponents", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\FakeSDK\WindowsMetadata\SDKWinMD2.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (fullPath.StartsWith(@"C:\DirectoryContains", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(fullPath).Equals(".winmd", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (fullPath.StartsWith(@"C:\WinMDArchVerification", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(fullPath).Equals(".winmd", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\FakeSDK\WindowsMetadata\SDKWinMD.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Checks to see if the assemblyName passed in is in the GAC.
        /// </summary>
        private static string GetPathForAssemblyInGac(AssemblyNameExtension assemblyName, SystemProcessorArchitecture targetProcessorArchitecture, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVersion, FileExists fileExists, bool fullFusionName, bool specificVersion)
        {
            if (assemblyName.Equals(new AssemblyNameExtension("V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")))
            {
                return null;
            }
            else if (assemblyName.Equals(new AssemblyNameExtension("W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")))
            {
                return @"C:\MyComponents2\W.dll";
            }
            else if (assemblyName.Equals(new AssemblyNameExtension("Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")))
            {
                return null;
            }
            else if (assemblyName.Equals(new AssemblyNameExtension("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")))
            {
                return @"C:\MyComponents\X.dll";
            }
            else if (assemblyName.Equals(new AssemblyNameExtension("Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")))
            {
                return null;
            }
            else
            {
                string gacLocation = null;
                if (assemblyName.Version != null)
                {
                    gacLocation = GlobalAssemblyCache.GetLocation(assemblyName, targetProcessorArchitecture, getRuntimeVersion, targetedRuntimeVersion, fullFusionName, fileExists, null, null, specificVersion /* this value does not matter if we are passing a full fusion name*/);
                }
                return gacLocation;
            }
        }

        /// <summary>
        /// Mock the File.Exists method.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>'true' if the file is supposed to exist</returns>
        internal static bool FileExists(string path)
        {
            // For very long paths, File.Exists just returns false
            if (path.Length > 240)
            {
                return false;
            }

            // Do a real File.Exists to make it throw exceptions for illegal paths.
            if (File.Exists(path) && useFrameworkFileExists)
            {
                return true;
            }

            // Do IO monitoring if needed.
            if (uniqueFileExists != null)
            {
                string lowerPath = path.ToLower();
                if (uniqueFileExists[lowerPath] == null)
                {
                    uniqueFileExists[lowerPath] = 0;
                }
                else
                {
                    uniqueFileExists[lowerPath] = (int)uniqueFileExists[lowerPath] + 1;
                }
            }


            // First, MyMissingAssembly doesn't exist anywhere.
            if (path.IndexOf("MyMissingAssembly") != -1)
            {
                return false;
            }

            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }


            foreach (string file in s_existentFiles)
            {
                if (0 == String.Compare(path, file, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }


            // Everything else doesn't exist.
            return false;
        }

        /// <summary>
        /// Mock the Directory.Exists method.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>'true' if the directory is supposed to exist</returns>
        internal static bool DirectoryExists(string path)
        {
            // Now specify the remaining files.
            string[] existentDirs = new string[]
            {
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion",
                @"c:\SGenDependeicies",
                Path.GetTempPath()
            };

            foreach (string dir in existentDirs)
            {
                if (0 == String.Compare(path, dir, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Everything else doesn't exist.
            return false;
        }

        /// <summary>
        /// A mock delagate for Directory.GetDirectories. 
        /// </summary>
        /// <param name="file">The file path.</param>
        /// <param name="file">The file pattern.</param>
        /// <returns>A set of subdirectories</returns>
        internal static string[] GetDirectories(string path, string pattern)
        {
            if (path.EndsWith(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion"))
            {
                string[] paths = new string[] {
                    Path.Combine(path, "en"), Path.Combine(path, "en-GB"), Path.Combine(path, "xx")
                };

                return paths;
            }
            else if (String.Compare(path, @".", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Pretend the current directory has a few subfolders.
                return new string[] {
                    Path.Combine(path, "en"), Path.Combine(path, "en-GB"), Path.Combine(path, "xx")
                };
            }

            return new string[0];
        }

        /// <summary>
        /// Given a path return the corosponding CLR runtime version
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <returns>Image runtime version</returns>
        internal static string GetRuntimeVersion(string path)
        {
            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "WindowsRuntime 1.0, CLR V2.0.50727";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\WinMDWithVersion255.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleClrOnly.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "CLR V2.0.50727";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleBadWindowsRuntime.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "Windows Runtime";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeAndOther.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "WindowsRuntime 1.0, Other V2.0.50727";
            }

            else if (String.Compare(path, @"C:\DirectoryContainsOnlyDll\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "V2.0.50727";
            }
            else if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "V2.0.50727";
            }
            else if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\a.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "WindowsRuntime 1.0";
            }
            else if (path.StartsWith(@"C:\MyWinMDComponents", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows Runtime";
            }
            else if (path.StartsWith(@"C:\WinMDArchVerification", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".winmd"))
            {
                return "WindowsRuntime 1.0";
            }
            else if (path.EndsWith(".dll") || path.EndsWith(".exe") || path.EndsWith(".winmd"))
            {
                return "v2.0.50727";
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// Given a path, return the corresponding AssemblyName
        /// </summary>
        /// <param name="path">Path to the assembly.</param>
        /// <returns>The assemblyname.</returns>
        internal static AssemblyNameExtension GetAssemblyName(string path)
        {
            // Do IO monitoring if needed.
            if (uniqueGetAssemblyName != null)
            {
                string lowerPath = path.ToLower();
                if (uniqueGetAssemblyName[lowerPath] == null)
                {
                    uniqueGetAssemblyName[lowerPath] = 0;
                }
                else
                {
                    uniqueGetAssemblyName[lowerPath] = (int)uniqueGetAssemblyName[lowerPath] + 1;
                }
            }

            // For very long paths, GetAssemblyName throws an exception.
            if (path.Length > 240)
            {
                throw new FileNotFoundException(path);
            }

            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }

            if
            (
                String.Compare(path, @"c:\OldClrBug\MyFileLoadExceptionAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0
            )
            {
                // An older LKG of the CLR could throw a FileLoadException if it doesn't recognize
                // the assembly. We need to support this for dogfooding purposes.
                throw new FileLoadException("Could not load " + path);
            }

            if
            (
                String.Compare(path, @"c:\Regress313086\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
            )
            {
                // This is an mscorlib that returns null for its assembly name.
                return null;
            }

            if
            (
                String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\BadImage.dll", StringComparison.OrdinalIgnoreCase) == 0
            )
            {
                throw new System.BadImageFormatException(@"The format of the file 'c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\BadImage.dll' is invalid");
            }

            if
            (
                String.Compare(path, @"c:\MyProject\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                || String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                || String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
            )
            {
                // This is an mscorlib.dll with no metadata.
                return null;
            }

            if
            (
                String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                || String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
            )
            {
                // This is an mscorlib.dll with no metadata.
                return null;
            }

            if (path.Contains("MyMissingAssembly"))
            {
                throw new FileNotFoundException(path);
            }

            if (String.Compare(path, @"c:\Frameworks\DependsOnFoo45Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral");
            }

            if (String.Compare(path, @"c:\Frameworks\DependsOnFoo4Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnFoo4Framework, Version=4.0.0.0, PublicKeyToken=null, Culture=Neutral");
            }

            if (String.Compare(path, @"c:\Frameworks\DependsOnFoo35Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnFoo35Framework, Version=3.5.0.0, PublicKeyToken=null, Culture=Neutral");
            }

            if (String.Compare(path, @"c:\Regress315619\A\MyAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyAssembly, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress315619\B\MyAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyAssembly, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\Regress442570\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }
            if (String.Compare(path, @"c:\Regress387218\v1\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\Regress442570\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\Regress387218\v2\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("D, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress390219\v1\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("D, Version=1.0.0.0, Culture=fr, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\Regress390219\v2\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("D, Version=2.0.0.0, Culture=en, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\Regress442570\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\Regress442570\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\MyStronglyNamed\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\MyNameMismatch\Foo.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Notice the metadata assembly name does not match the base file name.
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\MyEscapedName\=A=.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Notice the metadata assembly name does not match the base file name.
                return new AssemblyNameExtension("\\=A\\=, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089", true);
            }

            if (String.Compare(path, @"c:\MyEscapedName\__'ASP'dw0024ry.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Notice the metadata assembly name does not match the base file name.
                return new AssemblyNameExtension("__\\'ASP\\'dw0024ry", true);
            }

            if (String.Compare(path, @"c:\MyInaccessible\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate an assembly that throws an UnauthorizedAccessException upon access.
                throw new UnauthorizedAccessException();
            }

            if (String.Compare(path, Path.Combine(Path.GetTempPath(), @"RawFileNameRelative\System.Xml.dll"), StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension(AssemblyRef.SystemXml);
            }

            if (String.Compare(path, Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles\System.Xml.dll"), StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension(AssemblyRef.SystemXml);
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.XML.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension(AssemblyRef.SystemXml);
            }

            // This is an assembly with an earlier version.
            if (String.Compare(path, @"c:\MyProject\System.Xml.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension(AssemblyRef.SystemXml);
            }

            // This is an assembly with an incorrect PKT.
            if (String.Compare(path, @"c:\MyProject\System.Data.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=A77a5c561934e089");
            }

            if (path.EndsWith(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\MyGacAssembly.dll"))
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGacAssembly, Version=9.2.3401.1, Culture=neutral, PublicKeyToken=a6694b450823df78");
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("System, VeRSion=2.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("System, VeRSion=4.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v9.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("System, VeRSion=9.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089");
            }

            if
            (
                String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Data.dll", StringComparison.OrdinalIgnoreCase) == 0
            )
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension(AssemblyRef.SystemData);
            }

            if (path.EndsWith(@"c:\MyLibraries\v1\D.dll"))
            {
                // Version 1 of D
                return new AssemblyNameExtension("D, Version=1.0.0.0, CulTUre=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa");
            }

            if (path.EndsWith(@"c:\RogueLibraries\v1\D.dll"))
            {
                // Version 1 of D, but with a different PKT
                return new AssemblyNameExtension("D, VERsion=1.0.0.0, Culture=neutral, PublicKeyToken=bbbbbbbbbbbbbbbb");
            }

            if (path.EndsWith(@"c:\MyLibraries\v1\E\E.dll"))
            {
                return new AssemblyNameExtension("E, Version=0.0.0.0, Culture=neutral, PUBlicKeyToken=null");
            }


            if (String.Compare(path, @"C:\MyComponents\v0.5\UnifyMe.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                throw new FileNotFoundException();
            }

            if (String.Compare(path, @"C:\MyComponents\v1.0\UnifyMe.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("UnifyMe, Version=1.0.0.0, Culture=nEUtral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
            }

            if (String.Compare(path, @"C:\Framework\Everett\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("System, Version=1.0.5000.0, Culture=neutral, PublICKeyToken=" + AssemblyRef.EcmaPublicKey);
            }

            if (String.Compare(path, @"C:\Framework\Whidbey\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKey);
            }


            if (String.Compare(path, @"C:\MyApp\v1.0\DependsOnEverettSystem.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnEverettSystem, VersION=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe");
            }

            if (String.Compare(path, @"C:\MyApp\v0.5\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnUnified, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\Regress339786\FolderA\C.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("C, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\Regress339786\FolderB\C.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("C, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\MyApp\v1.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnUnified, VERSion=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\MyApp\v2.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnUnified, VeRSIon=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\MyApp\v3.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnUnified, Version=3.0.0.0, Culture=neutral, PublicKEYToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\MyComponents\v2.0\UnifyMe.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyTOKEn=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\MyComponents\v3.0\UnifyMe.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("UnifyMe, Version=3.0.0.0, Culture=neutral, PublICkeyToken=b77a5c561934e089");
            }

            if (path.EndsWith(@"c:\MyLibraries\v2\D.dll"))
            {
                return new AssemblyNameExtension("D, VErsion=2.0.0.0, CulturE=neutral, PublicKEyToken=aaaaaaaaaaaaaaaa");
            }

            if (String.Compare(path, @"C:\Regress317975\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"C:\Regress317975\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"C:\Regress317975\v2\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            // Set up assembly names for testing target framework version checks
            // Is version 4 and will only depends on 4.0 assemblies
            if (String.Compare(path, @"c:\MyComponents\4.0Component\DependsOnOnlyv4Assemblies.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            // Is version 9 and will not have any dependencies, will be in the redist list
            if (String.Compare(path, @"c:\MyComponents\misc\ReferenceVersion9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("ReferenceVersion9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            // Is a third party assembly which depends on a version 9 assembly
            if (String.Compare(path, @"c:\MyComponents\misc\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            //A second assembly which depends on version 9 framework assemblies.
            if (String.Compare(path, @"c:\MyComponents\misc\DependsOn9Also.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOn9Also, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            if (String.Compare(path, @"c:\MyComponents\1.0\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOn9, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            if (String.Compare(path, @"c:\MyComponents\2.0\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOn9, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            if (String.Compare(path, @"c:\Regress444809\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress444809\v2\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress444809\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("B, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress444809\C.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress444809\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\Regress714052\X86\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86");
            }
            if (String.Compare(path, @"C:\Regress714052\Mix\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86");
            }
            if (String.Compare(path, @"C:\Regress714052\Mix\a.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL");
            }

            if (String.Compare(path, @"C:\Regress714052\MSIL\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL");
            }

            if (String.Compare(path, @"C:\Regress714052\None\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"C:\Regress714052\X86\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86");
            }
            if (String.Compare(path, @"C:\Regress714052\Mix\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86");
            }
            if (String.Compare(path, @"C:\Regress714052\Mix\b.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL");
            }
            if (String.Compare(path, @"C:\Regress714052\MSIL\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL");
            }
            if (String.Compare(path, @"C:\Regress714052\None\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"c:\MyComponents\V.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"c:\MyComponents2\W.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"c:\MyComponents\X.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\MyComponents\Z.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\MyComponents\Y.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\MyComponents\Microsoft.Build.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("Microsoft.Build, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            }

            if (String.Compare(path, @"c:\MyComponents\DependsOnMSBuild12.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnMSBuild12, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\WinMD\v4\MsCorlib.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("mscorlib, Version=4.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\WinMD\v255\MsCorlib.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DotNetAssemblyDependsOnWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DotNetAssemblyDependsOn255WinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeOnly, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnInvalidPeHeader, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnAmd64.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnAmd64, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnArm.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnArm, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnIA64.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnIA64, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnArmv7.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnArmv7, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnX86.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnX86, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnUnknown.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnUnknown, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnAnyCPUUnknown, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMD\WinMDWithVersion255.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("WinMDWithVersion255, Version=255.255.255.255");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeOnly2, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeOnly3, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeOnly4, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeReferencingSystem, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeReferencingSystemDNE, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeAndCLR, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponents\MyGridWinMD.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponents2\MyGridWinMD.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD, Version=2.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponent7s\MyGridWinMD.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponents9\MyGridWinMD.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponentsVv1\MyGridWinMD2.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD2, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponentsV1\MyGridWinMD3.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD3, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\Debug\X86\DebugX86SDKWinMD.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DebugX86SDKWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKWinMD.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DebugNeutralSDKWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKWinMD.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("X86SDKWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKWinMD.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("NeutralSDKWINMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\Debug\X86\DebugX86SDKRA.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("Debugx86SDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKRA.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DebugNeutralSDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKRA.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("X86SDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKRA.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("NeutralSDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\Debug\X86\SDKReference.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SDKReference, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\DirectoryContainsOnlyDll\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("b, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\a.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            string defaultName = String.Format("{0}, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral", Path.GetFileNameWithoutExtension(path));
            return new AssemblyNameExtension(defaultName);
        }


        /// <summary>
        /// Cached implementation. Given an assembly name, crack it open and retrieve the list of dependent 
        /// assemblies and  the list of scatter files.
        /// </summary>
        /// <param name="path">Path to the assembly.</param>
        /// <param name="dependencies">Receives the list of dependencies.</param>
        /// <param name="scatterFiles">Receives the list of associated scatter files.</param>
        internal static void GetAssemblyMetadata
        (
            string path,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkNameVersioning frameworkName
        )
        {
            dependencies = GetDependencies(path);
            scatterFiles = null;
            frameworkName = GetTargetFrameworkAttribute(path);

            if (@"C:\Regress275161\a.dll" == path)
            {
                scatterFiles = new string[]
                {
                    @"m1.netmodule",
                    @"m2.netmodule"
                };
            }
        }

        /// <summary>
        /// Cached implementation. Given an assembly name, crack it open and retrieve the TargetFrameworkAttribute
        /// </summary>
        internal static FrameworkNameVersioning GetTargetFrameworkAttribute
        (
            string path
        )
        {
            FrameworkNameVersioning frameworkName = null;

            if (String.Equals(path, @"c:\Frameworks\DependsOnFoo4Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkNameVersioning("FoO, Version=v4.0");
            }
            else if (String.Equals(path, @"c:\Frameworks\DependsOnFoo45Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkNameVersioning("FoO, Version=v4.5");
            }
            else if (String.Equals(path, @"c:\Frameworks\DependsOnFoo35Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkNameVersioning("FoO, Version=v3.5");
            }
            else if (String.Equals(path, @"c:\Frameworks\IndirectDependsOnFoo4Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkNameVersioning("FoO, Version=v4.0");
            }
            else if (String.Equals(path, @"c:\Frameworks\IndirectDependsOnFoo45Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkNameVersioning("FoO, Version=v4.0");
            }
            else if (String.Equals(path, @"c:\Frameworks\IndirectDependsOnFoo35Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkNameVersioning("FoO, Version=v4.0");
            }

            return frameworkName;
        }

        /// <summary>
        /// Given an assembly, with optional assemblyName return all of the dependent assemblies.
        /// </summary>
        /// <param name="path">The full path to the parent assembly</param>
        /// <returns>The array of dependent assembly names.</returns>
        internal static AssemblyNameExtension[] GetDependencies(string path)
        {
            if (String.Compare(path, @"c:\Frameworks\IndirectDependsOnFoo4Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("DependsOnFoo4Framework, Version=4.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"c:\Frameworks\IndirectDependsOnFoo45Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"c:\Frameworks\IndirectDependsOnFoo35Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("DependsOnFoo35Framework, Version=3.5.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress454863\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("B, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress442570\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\Regress313747\Microsoft.Office.Interop.Excel.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c")
                };
            }

            if (String.Compare(path, @"C:\Regress313747\MS.Internal.Test.Automation.Office.Excel.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=94de0004b6e3fcc5")
                };
            }

            if (String.Compare(path, @"c:\Regress387218\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=1.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"c:\Regress387218\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=2.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"c:\Regress390219\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=1.0.0.0, PublicKeyToken=b77a5c561934e089, Culture=fr")
                };
            }

            if (String.Compare(path, @"c:\Regress390219\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=2.0.0.0,  PublicKeyToken=b77a5c561934e089, Culture=en")
                };
            }

            if (String.Compare(path, @"C:\Regress454863\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("B, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress442570\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\Regress313747\Microsoft.Office.Interop.Excel.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c")
                };
            }

            if (String.Compare(path, @"C:\Regress313747\MS.Internal.Test.Automation.Office.Excel.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=94de0004b6e3fcc5")
                };
            }

            if (String.Compare(path, @"c:\OldClrBug\DependsMyFileLoadExceptionAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("MyFileLoadExceptionAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\Regress563286\DependsOnBadImage.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("BadImage, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"c:\MyInaccessible\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                throw new UnauthorizedAccessException();
            }

            if (String.Compare(path, @"c:\Regress313086\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[] { };
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("SampleWindowsRuntimeOnly, Version=1.0.0.0")
                };
            }

            if (String.Compare(path, @"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("WinMDWithVersion255, Version=255.255.255.255")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeAndClr.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                 {
                    new AssemblyNameExtension("mscorlib, Version=4.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089")
                 };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                 {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                 };
            }

            if (String.Compare(path, @"C:\WinMD\WinMDWithVersion255.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("SampleWindowsRuntimeOnly, Version=1.0.0.0")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("SampleWindowsRuntimeOnly, Version=1.0.0.0"),
                    new AssemblyNameExtension("SampleWindowsRuntimeReferencingSystem, Version=1.0.0.0"),
                    new AssemblyNameExtension("WinMDWithVersion255, Version=255.255.255.255")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("SampleWindowsRuntimeReferencingSystemDNE, Version=1.0.0.0"),
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("System, Version=255.255.255.255, Culture=Neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("System.DoesNotExist, Version=255.255.255.255")
                };
            }

            if
            (
                String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                || String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
            )
            {
                return new AssemblyNameExtension[]
                {
                };
            }

            if (String.Compare(path, @"MyRelativeAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                };
            }

            if (String.Compare(path, @"c:\MyApp\DependsOnSimpleA.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("A, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress312873\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("A, Version=0.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress339786\FolderA\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress339786\FolderB\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("C, Version=2.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress317975\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("B, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\myassemblies\My.Assembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=2.0.0.0, Culture=NEUtraL, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\MyComponents\MyGrid.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, VeRsIon=2.0.0.0, Culture=neuTRal, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\MyRawDropControls\MyRawDropControl.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, VeRsIon=2.0.0.0, Culture=neuTRal, PublicKeyToken=b77a5c561934e089")
                };
            }


            if (String.Compare(path, @"c:\MyLibraries\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=1.0.0.0, CuLtUrE=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
                };
            }

            if (String.Compare(path, @"c:\MyLibraries\t.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, VeRsIon=1.0.0.0, Culture=neutral, PublicKeyToken=bbbbbbbbbbbbbbbb")
                };
            }

            if (String.Compare(path, @"c:\MyLibraries\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=2.0.0.0, Culture=neutral, PuBlIcKeYToken=aaaaaaaaaaaaaaaa")
                };
            }

            if (String.Compare(path, @"c:\MyLibraries\v1\d.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("E, VERSIOn=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyLibraries\v2\d.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("E, Version=0.0.0.0, Culture=neutRAL, PUblicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyLibraries\v1\E\E.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                };
            }

            if (String.Compare(path, @"C:\MyApp\v0.5\DependsOnWeaklyNamedUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("UnifyMe, Version=0.0.0.0, PUBLICKeyToken=null, CuLTURE=Neutral")
                };
            }

            if (String.Compare(path, @"C:\MyApp\v1.0\DependsOnEverettSystem.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, VeRsiON=1.0.5000.0, Culture=neutral, PublicKeyToken="+AssemblyRef.EcmaPublicKey)
                };
            }

            if (String.Compare(path, @"C:\MyApp\v0.5\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("UnifyMe, Version=0.5.0.0, CuLTUre=neUTral, PubLICKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\MyApp\v1.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("UNIFyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\MyApp\v2.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("UniFYme, Version=2.0.0.0, Culture=NeutraL, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\MyApp\v3.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("UnIfyMe, Version=3.0.0.0, Culture=nEutral, PublicKEyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\MyProject\MyMissingAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                throw new FileNotFoundException(path);
            }

            // Set up assembly names for testing target framework version checks
            // Is version 4 and will only depends on 4.0 assemblies
            if (String.Compare(path, @"c:\MyComponents\4.0Component\DependsOnOnlyv4Assemblies.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            // Is version 9 and will not have any dependencies, will be in the redist list
            if (String.Compare(path, @"c:\MyComponents\misc\ReferenceVersion9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("RandomAssembly, Version=9.0.0.0, Culture=neutral, PublicKeyToken=c77a5c561934e089")
                };
            }

            // Is a third party assembly which depends on a version 9 assembly
            if (String.Compare(path, @"c:\MyComponents\misc\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("System.Data, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            //A second assembly which depends on version 9 framework assemblies.
            if (String.Compare(path, @"c:\MyComponents\misc\DependsOn9Also.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\MyComponents\1.0\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\MyComponents\2.0\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\Regress444809\C.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("B, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"),
                    new AssemblyNameExtension("A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\Regress444809\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\Regress444809\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("A, Version=20.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyComponents\V.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyComponents2\W.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[] { };
            }

            if (String.Compare(path, @"c:\MyComponents\X.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyComponents\Z.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[] { };
            }

            if (String.Compare(path, @"c:\MyComponents\Y.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyComponents\Microsoft.Build.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[] { };
            }

            if (String.Compare(path, @"c:\MyComponents\DependsOnMSBuild12.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("Microsoft.Build, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
                };
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                     new AssemblyNameExtension("msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                     new AssemblyNameExtension("msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v9.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                     new AssemblyNameExtension("msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\DirectoryContainsOnlyDll\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                     new AssemblyNameExtension("C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                     new AssemblyNameExtension("C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[0];
            }

            if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\a.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                     new AssemblyNameExtension("C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[0];
            }

            if (path.StartsWith(@"C:\FakeSDK\", StringComparison.OrdinalIgnoreCase))
            {
                return new AssemblyNameExtension[0];
            }

            if (String.Compare(path, @"C:\SystemRuntime\Portable.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                    GetAssemblyName(@"C:\SystemRuntime\System.Runtime.dll")
                };
            }


            // Use a default list.
            return new AssemblyNameExtension[]
            {
                new AssemblyNameExtension("SysTem, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77A5c561934e089"),
                new AssemblyNameExtension("msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089")
            };
        }

        /// <summary>
        /// Registry access delegate. Given a hive and a view, return the registry base key.
        /// </summary>
        private static RegistryKey GetBaseKey(RegistryHive hive, RegistryView view)
        {
            if (hive == RegistryHive.CurrentUser)
            {
                return Registry.CurrentUser;
            }
            else if (hive == RegistryHive.LocalMachine)
            {
                return Registry.LocalMachine;
            }

            return null;
        }

        /// <summary>
        /// Simplified registry access delegate. Given a baseKey and a subKey, get all of the subkey
        /// names.
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="subKey">The subkey</param>
        /// <returns>An enumeration of strings.</returns>
        private static IEnumerable<string> GetRegistrySubKeyNames(RegistryKey baseKey, string subKey)
        {
            if (baseKey == Registry.CurrentUser)
            {
                if (String.Compare(subKey, @"Software\Regress714052", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\Mix", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\Mix\Mix", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\None", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\None\None", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "", "vBogusVersion", "v1.a.2.3", "v1.0", "v3.0", "v2.0.50727", "v2.0.x86chk", "RandomJunk" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "ZControlA", "ZControlB", "Infragistics.GridControl.1.0", "Infragistics.MyHKLMControl.1.0", "Infragistics.MyControlWithFutureTargetNDPVersion.1.0", "Infragistics.MyControlWithPastTargetNDPVersion.1.0", "Infragistics.MyControlWithServicePack.1.0" };
                }
                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.x86chk\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "RawDropControls" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v3.0\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Infragistics.MyControlWithFutureTargetNDPVersion.1.0" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Infragistics.MyNDP1Control.1.0", "Infragistics.MyControlWithPastTargetNDPVersion.1.0" };
                }

                if
                (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.GridControl.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v3.0\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyNDP1Control.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.x86chk\AssemblyFoldersEx\RawDropControls", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlA", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlB", StringComparison.OrdinalIgnoreCase) == 0
                )
                {
                    return new string[] { };
                }

                if
                (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0", StringComparison.OrdinalIgnoreCase) == 0
                )
                {
                    // This control has a service pack
                    return new string[] { "sp1", "sp2" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "v2.0.3600" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "PocketPC" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "AFETestDeviceControl" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "1234" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\Microsoft SDKs", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Windows" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\Microsoft SDKs\Windows", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "7.0", "8.0", "v8.0", "9.0" };
                }
            }

            if (baseKey == Registry.LocalMachine)
            {
                if (String.Compare(subKey, @"Software\Regress714052", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "v2.0.0" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "A", "B" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx\A", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx\B", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "X86" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "MSIL" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\None", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "None" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\Mix", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Mix" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\Mix\Mix", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\None\None", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "vBogusVersion", "v2.0.50727" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Infragistics.FancyControl.1.0", "Infragistics.MyHKLMControl.1.0" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.FancyControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "v2.0.3600" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "PocketPC" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\Microsoft SDKs\Windows", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "8.0" };
                }
            }

            Console.WriteLine("subKey={0}", subKey);
            Assert.True(false, "New GetRegistrySubKeyNames parameters encountered, need to add unittesting support");
            return null;
        }

        /// <summary>
        /// Simplified registry access delegate. Given a baseKey and subKey, get the default value
        /// of the subKey.
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="subKey">The subkey</param>
        /// <returns>A string containing the default value.</returns>
        private static string GetRegistrySubKeyDefaultValue(RegistryKey baseKey, string subKey)
        {
            if (baseKey == Registry.CurrentUser)
            {
                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlA", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponentsA";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlB", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponentsB";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.GridControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponents";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.x86chk\AssemblyFoldersEx\RawDropControls", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyRawDropControls";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponents\HKCU Components";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v3.0\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponents\v3.0";
                }

                if
                (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0", StringComparison.OrdinalIgnoreCase) == 0
                )
                {
                    return @"C:\MyComponents\v2.0";
                }

                if
                (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0", StringComparison.OrdinalIgnoreCase) == 0
                )
                {
                    return @"C:\MyComponentBase";
                }

                if
                (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0\sp1", StringComparison.OrdinalIgnoreCase) == 0
                )
                {
                    return @"C:\MyComponentServicePack1";
                }

                if
                (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0\sp2", StringComparison.OrdinalIgnoreCase) == 0
                )
                {
                    return @"C:\MyComponentServicePack2";
                }

                if
                (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyNDP1Control.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                )
                {
                    return @"C:\MyComponents\v1.0";
                }

                if (String.Compare(subKey, @"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\V1Control";
                }
                if (String.Compare(subKey, @"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl\1234", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\V1ControlSP1";
                }
            }

            if (baseKey == Registry.LocalMachine)
            {
                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.FancyControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponents\HKLM Components";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponents\HKLM Components";
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx\B", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\X86";
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx\A", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\MSIL";
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\X86";
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\Mix\Mix", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\Mix";
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\MSIL";
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\X86";
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\MSIL";
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\None\None", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\None";
                }
            }

            Console.WriteLine("subKey={0}", subKey);
            Assert.True(false, "New GetRegistrySubKeyDefaultValue parameters encountered, need to add unittesting support");
            return null;
        }

        /// <summary>
        /// Delegate for System.IO.File.GetLastWriteTime
        /// </summary>
        /// <param name="path">The file name</param>
        /// <returns>The last write time.</returns>
        private static DateTime GetLastWriteTime(string path)
        {
            return DateTime.FromOADate(0.0);
        }

        /// <summary>
        /// Assert that two strings are equal without regard to case.
        /// </summary>
        /// <param name="expected">The expected string.</param>
        /// <param name="actual">The actual string.</param>
        internal protected static void AssertNoCase(string expected, string actual)
        {
            if (0 != String.Compare(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                string message = String.Format("Expected value '{0}' but received '{1}'", expected, actual);
                Console.WriteLine(message);
                Assert.True(false, message);
            }
        }

        /// <summary>
        /// Assert that two strings are equal without regard to case.
        /// </summary>
        /// <param name="expected">The expected string.</param>
        /// <param name="actual">The actual string.</param>
        internal protected static void AssertNoCase(string message, string expected, string actual)
        {
            if (0 != String.Compare(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(message);
                Assert.True(false, message);
            }
        }

        /// <summary>
        /// Write out an appConfig file.
        /// Return the filename that was written.
        /// </summary>
        /// <param name="appConfigFile"></param>
        /// <param name="redirects"></param>
        protected static string WriteAppConfig(string redirects)
        {
            string appConfigContents =
            "<configuration>\n" +
            "    <runtime>\n" +
            redirects +
            "    </runtime>\n" +
            "</configuration>";

            string appConfigFile = FileUtilities.GetTemporaryFile();
            File.WriteAllText(appConfigFile, appConfigContents);
            return appConfigFile;
        }

        /// <summary>
        /// Determines whether the given item array has an item with the given spec.
        /// </summary>
        /// <param name="items">The item array.</param>
        /// <param name="spec">The spec to search for.</param>
        /// <returns>True if the spec was found.</returns>
        protected static bool ContainsItem(ITaskItem[] items, string spec)
        {
            foreach (ITaskItem item in items)
            {
                if (0 == String.Compare(item.ItemSpec, spec, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Execute the task.
        /// </summary>
        /// <remarks>
        /// NOTE! This test is not in fact completely isolated from its environment: it is reading the real redist lists. 
        /// </remarks>
        protected static bool Execute(ResolveAssemblyReference t)
        {
            return Execute(t, true);
        }

        /// <summary>
        /// Execute the task. Without confirming that the number of files resolved with and without find dependencies is identical. 
        /// This is because profiles could cause the number of primary references to be different.
        /// </summary>
        protected static bool Execute(ResolveAssemblyReference t, bool buildConsistencyCheck)
        {
            string tempPath = Path.GetTempPath();
            string redistListPath = Path.Combine(tempPath, Guid.NewGuid() + ".xml");
            string rarCacheFile = Path.Combine(tempPath, Guid.NewGuid() + ".RarCache");
            s_existentFiles.Add(rarCacheFile);

            bool succeeded = false;

            try
            {
                // Set the InstalledAssemblyTables parameter.
                if (t.InstalledAssemblyTables.Length == 0)
                {
                    File.WriteAllText(redistListPath, REDISTLIST);
                    t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                }

                // First, run it in loading-a-project mode.
                t.Silent = true;
                t.FindDependencies = false;
                t.FindSatellites = false;
                t.FindSerializationAssemblies = false;
                t.FindRelatedFiles = false;
                t.StateFile = null;
                t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader);

                // A few checks. These should always be true or it may be a perf issue for project load.
                ITaskItem[] loadModeResolvedFiles = new TaskItem[0];
                if (t.ResolvedFiles != null)
                {
                    loadModeResolvedFiles = (ITaskItem[])t.ResolvedFiles.Clone();
                }
                Assert.Equal(0, t.ResolvedDependencyFiles.Length);
                Assert.Equal(0, t.SatelliteFiles.Length);
                Assert.Equal(0, t.RelatedFiles.Length);
                Assert.Equal(0, t.SuggestedRedirects.Length);
                Assert.Equal(0, t.FilesWritten.Length);

                // Now, run it in building-a-project mode.
                MockEngine e = (MockEngine)t.BuildEngine;
                e.Warnings = 0;
                e.Errors = 0;
                e.Log = "";
                t.Silent = false;
                t.FindDependencies = true;
                t.FindSatellites = true;
                t.FindSerializationAssemblies = true;
                t.FindRelatedFiles = true;
                string cache = rarCacheFile;
                t.StateFile = cache;
                File.Delete(t.StateFile);
                succeeded = t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader);
                if (FileUtilities.FileExistsNoThrow(t.StateFile))
                {
                    Assert.Equal(1, t.FilesWritten.Length);
                    Assert.True(t.FilesWritten[0].ItemSpec.Equals(cache, StringComparison.OrdinalIgnoreCase));
                }

                File.Delete(t.StateFile);

                if (buildConsistencyCheck)
                {
                    // Some consistency checks between load mode and build mode.
                    Assert.Equal(loadModeResolvedFiles.Length, t.ResolvedFiles.Length);
                    for (int i = 0; i < loadModeResolvedFiles.Length; i++)
                    {
                        Assert.Equal(loadModeResolvedFiles[i].ItemSpec, t.ResolvedFiles[i].ItemSpec);
                        Assert.Equal(loadModeResolvedFiles[i].GetMetadata("CopyLocal"), t.ResolvedFiles[i].GetMetadata("CopyLocal"));
                        Assert.Equal(loadModeResolvedFiles[i].GetMetadata("ResolvedFrom"), t.ResolvedFiles[i].GetMetadata("ResolvedFrom"));
                    }
                }

                // Check attributes on resolve files.
                for (int i = 0; i < t.ResolvedFiles.Length; i++)
                {
                    // OriginalItemSpec attribute on resolved items is to support VS in figuring out which
                    // project file reference caused a particular resolved file.
                    string originalItemSpec = t.ResolvedFiles[i].GetMetadata("OriginalItemSpec");
                    Assert.True(ContainsItem(t.Assemblies, originalItemSpec) || ContainsItem(t.AssemblyFiles, originalItemSpec)); //                         "Expected to find OriginalItemSpec in Assemblies or AssemblyFiles task parameters"
                }
            }
            finally
            {
                s_existentFiles.Remove(rarCacheFile);
                if (File.Exists(redistListPath))
                {
                    FileUtilities.DeleteNoThrow(redistListPath);
                }

                if (File.Exists(rarCacheFile))
                {
                    FileUtilities.DeleteNoThrow(rarCacheFile);
                }
            }
            return succeeded;
        }

        /// <summary>
        /// Helper method which allows tests to specify additional assembly search paths.
        /// </summary>
        /// <param name="e"></param>
        internal void ExecuteRAROnItemsAndRedist(ResolveAssemblyReference t, MockEngine e, ITaskItem[] items, string redistString, bool consistencyCheck)
        {
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, consistencyCheck, null);
        }

        /// <summary>
        /// Helper method to get rid of some of the code duplication
        /// </summary>
        internal void ExecuteRAROnItemsAndRedist(ResolveAssemblyReference t, MockEngine e, ITaskItem[] items, string redistString, bool consistencyCheck, List<string> additionalSearchPaths)
        {
            t.BuildEngine = e;
            List<string> searchPaths = new List<string>(DefaultPaths);

            if (additionalSearchPaths != null)
            {
                searchPaths.AddRange(additionalSearchPaths);
            }

            t.Assemblies = items;
            t.SearchPaths = searchPaths.ToArray();
            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                (
                    redistFile,
                    redistString
                );

                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistFile) };

                Execute(t, consistencyCheck);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }
    }

    namespace VersioningAndUnification.Prerequisite
    {
        sealed public class StronglyNamedDependency : ResolveAssemblyReferenceTestFixture
        {
            /// <summary>
            /// Return the default search paths.
            /// </summary>
            /// <value></value>
            new internal string[] DefaultPaths
            {
                get { return new string[] { @"C:\MyApp\v1.0", @"C:\Framework\Whidbey", @"C:\Framework\Everett" }; }
            }

            /// <summary>
            /// In this case,
            /// - A single reference to DependsOnEverettSystem was passed in.
            ///   - This assembly depends on version 1.0.5000.0 of System.DLL.
            /// - No app.config is passed in.
            /// - Version 1.0.5000.0 of System.dll exists.
            /// - Whidbey Version of System.dll exists.
            /// Expected:
            /// - The resulting System.dll returned should be Whidbey version.
            /// Rationale:
            /// We automatically unify FX dependencies.
            /// </summary>
            [Fact]
            public void Exists()
            {
                // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
                // times out the object used for remoting console writes.  Adding a write in the middle of
                // keeps remoting from timing out the object.
                Console.WriteLine("Performing VersioningAndUnification.Prerequisite.StronglyNamedDependency.Exists() test");

                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnEverettSystem, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe")
                    };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedDependencyFiles.Length);
                Assert.Equal(0, engine.Errors);
                Assert.Equal(0, engine.Warnings);
                AssertNoCase
                (
                    "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKey, t.ResolvedDependencyFiles[0].GetMetadata("FusionName")
                );

                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByFrameworkRetarget"), "1.0.5000.0", @"C:\MyApp\v1.0\DependsOnEverettSystem.dll")
                );

                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.NotCopyLocalBecausePrerequisite"))
                );

                AssertNoCase("false", t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal"));
            }

            /// <summary>
            /// In this case,
            /// - A single reference to DependsOnEverettSystem was passed in.
            ///   - This assembly depends on version 1.0.5000.0 of System.DLL.
            /// - No app.config is passed in.
            /// - Version 1.0.5000.0 of System.dll exists.
            /// - Whidbey Version of System.dll *does not* exist.
            /// Expected:
            /// - This should be an unresolved reference, we shouldn't fallback to the old version.
            /// Rationale:
            /// The fusion loader is going to want to respect the unified-to assembly. There's no point in
            /// feeding it the wrong version, and the drawback is that builds would be different from 
            /// machine-to-machine.
            /// </summary>
            [Fact]
            public void HighVersionDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnEverettSystem, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe")
                    };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = new string[] { @"C:\MyApp\v1.0", @"C:\Framework\Everett" }; ;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(0, t.ResolvedDependencyFiles.Length);
                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByFrameworkRetarget"), "1.0.5000.0", @"C:\MyApp\v1.0\DependsOnEverettSystem.dll")
                );
            }

            [Fact]
            public void VerifyAssemblyPulledOutOfFrameworkDoesntGetFrameworkFileAttribute()
            {
                MockEngine e = new MockEngine();

                string actualFrameworkDirectory = @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion";
                string alternativeFrameworkDirectory = @"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion";

                ITaskItem[] items = new TaskItem[] { new TaskItem(Path.Combine(actualFrameworkDirectory, "System.dll")) };

                // Version and directory match framework - it is a framework assembly
                string redistString1 = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                          "<File AssemblyName='System' Version='2.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                      "</FileList >";

                ResolveAssemblyReference t1 = new ResolveAssemblyReference();
                t1.TargetFrameworkVersion = "v4.5";
                t1.TargetFrameworkDirectories = new string[] { actualFrameworkDirectory };
                ExecuteRAROnItemsAndRedist(t1, e, items, redistString1, true, new List<string>() { "{RawFileName}" });

                Assert.False(String.IsNullOrEmpty(t1.ResolvedFiles[0].GetMetadata("FrameworkFile")));

                // Higher version than framework, but directory matches - it is a framework assembly
                string redistString2 = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                              "<File AssemblyName='System' Version='1.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                          "</FileList >";

                ResolveAssemblyReference t2 = new ResolveAssemblyReference();
                t2.TargetFrameworkVersion = "v4.5";
                t2.TargetFrameworkDirectories = new string[] { actualFrameworkDirectory };
                ExecuteRAROnItemsAndRedist(t2, e, items, redistString2, true, new List<string>() { "{RawFileName}" });

                Assert.False(String.IsNullOrEmpty(t2.ResolvedFiles[0].GetMetadata("FrameworkFile")));

                // Version is lower but directory does not match - it is a framework assembly
                string redistString3 = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                              "<File AssemblyName='System' Version='3.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                          "</FileList >";

                ResolveAssemblyReference t3 = new ResolveAssemblyReference();
                t3.TargetFrameworkVersion = "v4.5";
                t3.TargetFrameworkDirectories = new string[] { alternativeFrameworkDirectory };
                ExecuteRAROnItemsAndRedist(t3, e, items, redistString3, true, new List<string>() { "{RawFileName}" });

                Assert.False(String.IsNullOrEmpty(t3.ResolvedFiles[0].GetMetadata("FrameworkFile")));

                // Version is higher and directory does not match - this assembly has been pulled out of .NET
                string redistString4 = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                              "<File AssemblyName='System' Version='1.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                          "</FileList >";

                ResolveAssemblyReference t4 = new ResolveAssemblyReference();
                t4.TargetFrameworkVersion = "v4.5";
                t4.TargetFrameworkDirectories = new string[] { alternativeFrameworkDirectory };
                ExecuteRAROnItemsAndRedist(t4, e, items, redistString4, true, new List<string>() { "{RawFileName}" });

                Assert.True(String.IsNullOrEmpty(t4.ResolvedFiles[0].GetMetadata("FrameworkFile")));
            }
        }
    }

    namespace VersioningAndUnification.AppConfig
    {
        sealed public class FilePrimary : ResolveAssemblyReferenceTestFixture
        {
            /// <summary>
            /// In this case,
            /// - A single primary file reference to assembly version 1.0.0.0 was passed in.
            /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// -- The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// Primary references are never unified. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void Exists()
            {
                // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
                // times out the object used for remoting console writes.  Adding a write in the middle of
                // keeps remoting from timing out the object.
                Console.WriteLine("Performing VersioningAndUnification.AppConfig.FilePrimary.Exists() test");

                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\MyComponents\v1.0\UnifyMe.dll")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }


            /// <summary>
            /// Test the case where the appconfig has a malformed binding redirect version.
            /// </summary>
            [Fact]
            public void BadAppconfigOldVersion()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\MyComponents\v1.0\UnifyMe.dll")
                    };


                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "    <runtime>\n" +
                        "<assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'>\n" +
                            "<dependentAssembly>\n" +
                                "<assemblyIdentity name='Micron.Facilities.Data' publicKeyToken='2D8C82D3A1452EF1' culture='neutral'/>\n" +
                                    "<bindingRedirect oldVersion='1.*' newVersion='2.0.0.0'/>\n" +
                            "</dependentAssembly>\n" +
                         "</assemblyBinding>\n" +
                        "</runtime>\n"
                );

                try
                {
                    // Now, pass feed resolved primary references into ResolveAssemblyReference.
                    ResolveAssemblyReference t = new ResolveAssemblyReference();

                    t.BuildEngine = engine;
                    t.AssemblyFiles = assemblyFiles;
                    t.SearchPaths = DefaultPaths;
                    t.AppConfigFile = appConfigFile;

                    bool succeeded = Execute(t);

                    Assert.False(succeeded);
                    engine.AssertLogContains("MSB3249");
                }
                finally
                {
                    if (File.Exists(appConfigFile))
                    {
                        // Cleanup.
                        File.Delete(appConfigFile);
                    }
                }
            }

            /// <summary>
            /// Test the case where the appconfig has a malformed binding redirect version.
            /// </summary>
            [Fact]
            public void BadAppconfigNewVersion()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\MyComponents\v1.0\UnifyMe.dll")
                    };


                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "    <runtime>\n" +
                        "<assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'>\n" +
                            "<dependentAssembly>\n" +
                                "<assemblyIdentity name='Micron.Facilities.Data' publicKeyToken='2D8C82D3A1452EF1' culture='neutral'/>\n" +
                                    "<bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.*.0'/>\n" +
                            "</dependentAssembly>\n" +
                         "</assemblyBinding>\n" +
                        "</runtime>\n"
                );

                try
                {
                    // Now, pass feed resolved primary references into ResolveAssemblyReference.
                    ResolveAssemblyReference t = new ResolveAssemblyReference();

                    t.BuildEngine = engine;
                    t.AssemblyFiles = assemblyFiles;
                    t.SearchPaths = DefaultPaths;
                    t.AppConfigFile = appConfigFile;

                    bool succeeded = Execute(t);

                    Assert.False(succeeded);
                    engine.AssertLogContains("MSB3249");
                }
                finally
                {
                    if (File.Exists(appConfigFile))
                    {
                        // Cleanup.
                        File.Delete(appConfigFile);
                    }
                }
            }

            /// <summary>
            /// In this case,
            /// - A single reference to DependsOnUnified was passed in.
            ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
            /// - An app.config was passed in that promotes UnifyMe version from 1.0.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of UnifyMe exists.
            /// - Version 2.0.0.0 of UnifyMe exists.
            /// -Version 2.0.0.0 of UnifyMe is in the Black List
            /// Expected:
            /// - There should be a warning indicating that DependsOnUnified has a dependency UnifyMe 2.0.0.0 which is not in a TargetFrameworkSubset.
            /// - There will be no unified message.
            /// Rationale:
            /// Strongly named dependencies should unify according to the bindingRedirects in the app.config, if the unified version is in the black list it should be removed and warned.
            /// </summary>
            [Fact]
            public void ExistsPromotedDependencyInTheBlackList()
            {
                string implicitRedistListContents =
                                   "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                                       "<File AssemblyName='UniFYme' Version='2.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                                   "</FileList >";

                string engineOnlySubset =
                  "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                      "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "</FileList >";

                string redistListPath = FileUtilities.GetTemporaryFile();
                string subsetListPath = FileUtilities.GetTemporaryFile();
                string appConfigFile = null;
                try
                {
                    File.WriteAllText(redistListPath, implicitRedistListContents);
                    File.WriteAllText(subsetListPath, engineOnlySubset);


                    // Create the engine.
                    MockEngine engine = new MockEngine();

                    ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                    // Construct the app.config.
                    appConfigFile = WriteAppConfig
                    (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                    );

                    // Now, pass feed resolved primary references into ResolveAssemblyReference.
                    ResolveAssemblyReference t = new ResolveAssemblyReference();
                    t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                    t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };

                    t.BuildEngine = engine;
                    t.Assemblies = assemblyNames;
                    t.SearchPaths = DefaultPaths;
                    t.AppConfigFile = appConfigFile;

                    bool succeeded = Execute(t);

                    Assert.True(succeeded);
                    Assert.Equal(0, t.ResolvedDependencyFiles.Length);
                    engine.AssertLogDoesntContain
                    (
                        String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, @"C:\MyApp\v1.0\DependsOnUnified.dll")
                    );
                }
                finally
                {
                    File.Delete(redistListPath);
                    File.Delete(subsetListPath);

                    // Cleanup.
                    File.Delete(appConfigFile);
                }
            }

            /// <summary>
            /// In this case,
            /// - A single primary file reference to assembly version 1.0.0.0 was passed in.
            /// - An app.config was passed in that promotes a *different* assembly version name from 
            //    1.0.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// - The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// One entry in the app.config file should not be able to impact the mapping of an assembly
            /// with a different name.
            /// </summary>
            [Fact]
            public void ExistsDifferentName()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\MyComponents\v1.0\UnifyMe.dll")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='DontUnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single primary file reference to assembly version 1.0.0.0 was passed in.
            /// - An app.config was passed in that promotes assembly version from range 0.0.0.0-1.5.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// - The resulting assembly returned should be 2.0.0.0.
            /// Rationale:
            /// Primary references are never unified. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void ExistsOldVersionRange()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\MyComponents\v1.0\UnifyMe.dll")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='0.0.0.0-1.5.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single primary file reference to assembly version 1.0.0.0 was passed in.
            /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 4.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 4.0.0.0 of the file *does not* exist.
            /// Expected:
            /// -- The resulting assembly returned should be 2.0.0.0.
            /// Rationale:
            /// Primary references are never unified. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void HighVersionDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\MyComponents\v1.0\UnifyMe.dll")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='4.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single primary file reference to assembly version 0.5.0.0 was passed in.
            /// - An app.config was passed in that promotes assembly version from 0.0.0.0-2.0.0.0 to 2.0.0.0
            /// - Version 0.5.0.0 of the file *does not* exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// -- The resulting assembly returned should be 2.0.0.0.
            /// Rationale:
            /// There's no way for the resolve algorithm to determine that the file reference corresponds
            /// to a particular AssemblyName. Because of this, there's no way to determine that we want to 
            /// promote from 0.5.0.0 to 2.0.0.0. In this case, just use the assembly name that was passed in.
            /// </summary>
            [Fact]
            public void LowVersionDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\MyComponents\v0.5\UnifyMe.dll")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='0.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                Assert.Equal(t.ResolvedFiles[0].ItemSpec, assemblyFiles[0].ItemSpec);


                // Cleanup.
                File.Delete(appConfigFile);
            }
        }

        sealed public class SpecificVersionPrimary : ResolveAssemblyReferenceTestFixture
        {
            /// <summary>
            /// In this case,
            /// - A single primary version-strict reference was passed in to assembly version 1.0.0.0
            /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// - The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// Primary references are never unified. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void Exists()
            {
                // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
                // times out the object used for remoting console writes.  Adding a write in the middle of
                // keeps remoting from timing out the object.
                Console.WriteLine("Performing VersioningAndUnification.Prerequisite.SpecificVersionPrimary.Exists() test");

                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };
                assemblyNames[0].SetMetadata("SpecificVersion", "true");

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));
                AssertNoCase(@"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single primary version-strict reference was passed in to assembly version 1.0.0.0
            /// - An app.config was passed in that promotes a *different* assembly version name from 
            //    1.0.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// -- The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// Primary references are never unified. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void ExistsDifferentName()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };
                assemblyNames[0].SetMetadata("SpecificVersion", "true");

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='DontUnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single primary version-strict reference was passed in to assembly version 1.0.0.0
            /// - An app.config was passed in that promotes assembly version from range 0.0.0.0-1.5.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// -- The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// Primary references are never unified. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void ExistsOldVersionRange()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };
                assemblyNames[0].SetMetadata("SpecificVersion", "true");

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='0.0.0.0-1.5.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single primary version-strict reference was passed in to assembly version 1.0.0.0
            /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 4.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 4.0.0.0 of the file *does not* exist.
            /// Expected:
            /// -- The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// Primary references are never unified. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void HighVersionDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };
                assemblyNames[0].SetMetadata("SpecificVersion", "true");

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='4.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single primary version-strict reference was passed in to assembly version 0.5.0.0
            /// - An app.config was passed in that promotes assembly version from 0.0.0.0-2.0.0.0 to 2.0.0.0
            /// - Version 0.5.0.0 of the file *does not* exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// - The reference is not resolved.
            /// Rationale:
            /// Primary references are never unified--even those that don't exist on disk. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void LowVersionDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("UnifyMe, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };
                assemblyNames[0].SetMetadata("SpecificVersion", "true");

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='0.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(0, t.ResolvedFiles.Length);

                // Cleanup.
                File.Delete(appConfigFile);
            }
        }

        sealed public class NonSpecificVersionStrictPrimary : ResolveAssemblyReferenceTestFixture
        {
            /// <summary>
            /// Return the default search paths.
            /// </summary>
            /// <value></value>
            new internal string[] DefaultPaths
            {
                get { return new string[] { @"C:\MyComponents\v0.5", @"C:\MyComponents\v1.0", @"C:\MyComponents\v2.0", @"C:\MyComponents\v3.0" }; }
            }


            /// <summary>
            /// In this case,
            /// - A single primary non-version-strict reference was passed in to assembly version 1.0.0.0
            /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// - The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// Primary references are never unified. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void Exists()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };
                assemblyNames[0].SetMetadata("SpecificVersion", "false");


                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }



            /// <summary>
            /// In this case,
            /// - A single primary non-version-strict reference was passed in to assembly version 1.0.0.0
            /// - An app.config was passed in that promotes a *different* assembly version name from 
            //    1.0.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// -- The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// One entry in the app.config file should not be able to impact the mapping of an assembly
            /// with a different name.
            /// </summary>
            [Fact]
            public void ExistsDifferentName()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };
                assemblyNames[0].SetMetadata("SpecificVersion", "false");

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='DontUnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }


            /// <summary>
            /// In this case,
            /// - A single primary non-version-strict reference was passed in to assembly version 1.0.0.0
            /// - An app.config was passed in that promotes assembly version from range 0.0.0.0-1.5.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// -- The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// Primary references are never unified. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void ExistsOldVersionRange()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };
                assemblyNames[0].SetMetadata("SpecificVersion", "false");

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='0.0.0.0-1.5.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single primary non-version-strict reference was passed in to assembly version 1.0.0.0
            /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 4.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 4.0.0.0 of the file *does not* exist.
            /// Expected:
            /// -- The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// Primary references are never unified. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void HighVersionDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };
                assemblyNames[0].SetMetadata("SpecificVersion", "false");

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='4.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single primary non-version-strict reference was passed in to assembly version 0.5.0.0
            /// - An app.config was passed in that promotes assembly version from 0.0.0.0-2.0.0.0 to 2.0.0.0
            /// - Version 0.5.0.0 of the file *does not* exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// -- The resulting assembly returned should be 1.0.0.0 (remember this is non-version-strict)
            /// Rationale:
            /// Primary references are never unified--even those that don't exist on disk. This is because:
            /// (a) The user expects that a primary reference will be respected.
            /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
            ///     dependencies anyway to make things work consistently. This would be a significant
            ///     perf hit when loading large solutions.
            /// </summary>
            [Fact]
            public void LowVersionDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("UnifyMe, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };
                assemblyNames[0].SetMetadata("SpecificVersion", "false");

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='0.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }
        }

        sealed public class StronglyNamedDependency : ResolveAssemblyReferenceTestFixture
        {
            /// <summary>
            /// Return the default search paths.
            /// </summary>
            /// <value></value>
            new internal string[] DefaultPaths
            {
                get { return new string[] { @"C:\MyApp\v0.5", @"C:\MyApp\v1.0", @"C:\MyComponents\v0.5", @"C:\MyComponents\v1.0", @"C:\MyComponents\v2.0", @"C:\MyComponents\v3.0" }; }
            }


            /// <summary>
            /// In this case,
            /// - A single reference to DependsOnUnified was passed in.
            ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
            /// - An app.config was passed in that promotes UnifyMe version from 1.0.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of UnifyMe exists.
            /// - Version 2.0.0.0 of UnifyMe exists.
            /// Expected:
            /// - The resulting UnifyMe returned should be 2.0.0.0.
            /// Rationale:
            /// Strongly named dependencies should unify according to the bindingRedirects in the app.config.
            /// </summary>
            [Fact]
            public void Exists()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedDependencyFiles.Length);
                AssertNoCase("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, @"C:\MyApp\v1.0\DependsOnUnified.dll")
                );

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single reference to DependsOnUnified was passed in.
            ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
            /// - An app.config was passed in that promotes UnifyMe version from 1.0.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of UnifyMe exists.
            /// - Version 2.0.0.0 of UnifyMe exists.
            /// -Version 2.0.0.0 of UnifyMe is in the Black List
            /// Expected:
            /// - There should be a warning indicating that DependsOnUnified has a dependency UnifyMe 2.0.0.0 which is not in a TargetFrameworkSubset.
            /// - There will be no unified message.
            /// Rationale:
            /// Strongly named dependencies should unify according to the bindingRedirects in the app.config, if the unified version is in the black list it should be removed and warned.
            /// </summary>
            [Fact]
            public void ExistsPromotedDependencyInTheBlackList()
            {
                string engineOnlySubset =
                   "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                       "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                   "</FileList >";

                string implicitRedistListContents =
                                   "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                                       "<File AssemblyName='UniFYme' Version='2.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                                   "</FileList >";

                string redistListPath = FileUtilities.GetTemporaryFile();
                string subsetListPath = FileUtilities.GetTemporaryFile();
                string appConfigFile = null;
                try
                {
                    File.WriteAllText(redistListPath, implicitRedistListContents);
                    File.WriteAllText(subsetListPath, engineOnlySubset);


                    // Create the engine.
                    MockEngine engine = new MockEngine();

                    ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                    // Construct the app.config.
                    appConfigFile = WriteAppConfig
                    (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                    );

                    // Now, pass feed resolved primary references into ResolveAssemblyReference.
                    ResolveAssemblyReference t = new ResolveAssemblyReference();
                    t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                    t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };

                    t.BuildEngine = engine;
                    t.Assemblies = assemblyNames;
                    t.SearchPaths = DefaultPaths;
                    t.AppConfigFile = appConfigFile;

                    bool succeeded = Execute(t, false);

                    Assert.True(succeeded);
                    Assert.Equal(0, t.ResolvedDependencyFiles.Length);
                    engine.AssertLogDoesntContain
                    (
                        String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, @"C:\MyApp\v1.0\DependsOnUnified.dll")
                    );
                }
                finally
                {
                    File.Delete(redistListPath);
                    File.Delete(subsetListPath);

                    // Cleanup.
                    File.Delete(appConfigFile);
                }
            }

            /// <summary>
            /// In this case,
            /// - A single reference to DependsOnUnified was passed in.
            ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
            /// - An app.config was passed in that promotes a *different* assembly version name from 
            //    1.0.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// -- The resulting assembly returned should be 1.0.0.0.
            /// Rationale:
            /// An unrelated bindingRedirect in the app.config should have no bearing on unification 
            /// of another file.
            /// </summary>
            [Fact]
            public void ExistsDifferentName()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='DontUnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedDependencyFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));

                // Cleanup.
                File.Delete(appConfigFile);
            }


            /// <summary>
            /// In this case,
            /// - A single reference to DependsOnUnified was passed in.
            ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
            /// - An app.config was passed in that promotes assembly version from range 0.0.0.0-1.5.0.0 to 2.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// -- The resulting assembly returned should be 2.0.0.0.
            /// Rationale:
            /// Strongly named dependencies should unify according to the bindingRedirects in the app.config, even
            /// if a range is involved.
            /// </summary>
            [Fact]
            public void ExistsOldVersionRange()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='0.0.0.0-1.5.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedDependencyFiles.Length);
                AssertNoCase("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, @"C:\MyApp\v1.0\DependsOnUnified.dll")
                );

                // Cleanup.
                File.Delete(appConfigFile);
            }


            /// <summary>
            /// In this case,
            /// - A single reference to DependsOnUnified was passed in.
            ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
            /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 4.0.0.0
            /// - Version 1.0.0.0 of the file exists.
            /// - Version 4.0.0.0 of the file *does not* exist.
            /// Expected:
            /// - The dependent assembly should be unresolved.
            /// Rationale:
            /// The fusion loader is going to want to respect the app.config file. There's no point in
            /// feeding it the wrong version.
            /// </summary>
            [Fact]
            public void HighVersionDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='1.0.0.0' newVersion='4.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(0, t.ResolvedDependencyFiles.Length);
                string shouldContain;

                string code = t.Log.ExtractMessageCode
                        (
                            String.Format(AssemblyResources.GetString("ResolveAssemblyReference.FailedToResolveReference"),
                                String.Format(AssemblyResources.GetString("General.CouldNotLocateAssembly"), "UNIFyMe, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
                            out shouldContain
                        );


                engine.AssertLogContains
                (
                    shouldContain
                );

                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, @"C:\MyApp\v1.0\DependsOnUnified.dll")
                );

                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnifiedDependency"), "UNIFyMe, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                );



                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - A single reference to DependsOnUnified was passed in.
            ///   - This assembly depends on version 0.5.0.0 of UnifyMe.
            /// - An app.config was passed in that promotes assembly version from 0.0.0.0-2.0.0.0 to 2.0.0.0
            /// - Version 0.5.0.0 of the file *does not* exists.
            /// - Version 2.0.0.0 of the file exists.
            /// Expected:
            /// - The resulting assembly returned should be 2.0.0.0.
            /// Rationale:
            /// The lower (unified-from) version need not exist on disk (in fact we shouldn't even try to 
            /// resolve it) in order to arrive at the correct answer.
            /// </summary>
            [Fact]
            public void LowVersionDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='0.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedDependencyFiles.Length);
                AssertNoCase("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "0.5.0.0", appConfigFile, @"C:\MyApp\v0.5\DependsOnUnified.dll")
                );

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - An app.config is passed in that has some garbage in the version number.
            /// Expected:
            /// - An error and task failure.
            /// Rationale:
            /// Can't proceed with a bad app.config.
            /// </summary>
            [Fact]
            public void GarbageVersionInAppConfigFile()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='GarbledOldVersion' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='Garbled' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);
                Assert.False(succeeded);
                Assert.Equal(1, engine.Errors);

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - An app.config is passed in that has a missing oldVersion in a bindingRedirect. 
            /// Expected:
            /// - An error and task failure.
            /// Rationale:
            /// Can't proceed with a bad app.config.
            /// </summary>
            [Fact]
            public void GarbageAppConfigMissingOldVersion()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='MissingOldVersion' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);
                Assert.False(succeeded);
                Assert.Equal(1, engine.Errors);
                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("AppConfig.BindingRedirectMissingOldVersion"))
                );

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - An app.config is passed in that has a missing newVersion in a bindingRedirect. 
            /// Expected:
            /// - An error and task failure.
            /// Rationale:
            /// Can't proceed with a bad app.config.
            /// </summary>
            [Fact]
            public void GarbageAppConfigMissingNewVersion()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='MissingNewVersion' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);
                Assert.False(succeeded);
                Assert.Equal(1, engine.Errors);
                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("AppConfig.BindingRedirectMissingNewVersion"))
                );

                // Cleanup.
                File.Delete(appConfigFile);
            }


            /// <summary>
            /// In this case,
            /// - An app.config is passed in that has some missing information in &lt;assemblyIdentity&gt; element.
            /// Expected:
            /// - An error and task failure.
            /// Rationale:
            /// Can't proceed with a bad app.config.
            /// </summary>
            [Fact]
            public void GarbageAppConfigAssemblyNameMissingPKTAndCulture()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='GarbledOldVersion' />\n" +
                "            <bindingRedirect oldVersion='Garbled' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);
                Assert.False(succeeded);
                Assert.Equal(1, engine.Errors);

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - An app.config is specified 
            /// *and*
            /// - AutoUnify=true.
            /// Expected:
            /// - Success.
            /// Rationale:
            /// With the introduction of the GenerateBindingRedirects task, RAR now accepts AutoUnify and App.Config at the same time.
            /// </summary>
            [Fact]
            public void AppConfigSpecifiedWhenAutoUnifyEqualsTrue()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Construct the app.config.
                string appConfigFile = WriteAppConfig
                (
                "        <dependentAssembly>\n" +
                "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                "            <bindingRedirect oldVersion='0.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
                "        </dependentAssembly>\n"
                );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;
                t.AutoUnify = true;

                bool succeeded = Execute(t);

                // With the introduction of GenerateBindingRedirects task, RAR now accepts AutoUnify and App.Config at the same time.
                Assert.True(succeeded);
                Assert.Equal(0, engine.Errors);

                // Cleanup.
                File.Delete(appConfigFile);
            }

            /// <summary>
            /// In this case,
            /// - An app.config is specified, but the file doesn't exist.
            /// Expected:
            /// - An error and task failure.
            /// Rationale:
            /// App.config must exist if specifed.
            /// </summary>
            [Fact]
            public void AppConfigDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = @"C:\MyNonexistentFolder\MyNonExistentApp.config";

                bool succeeded = Execute(t);
                Assert.False(succeeded);
                Assert.Equal(1, engine.Errors);
            }
        }
    }

    namespace VersioningAndUnification.AutoUnify
    {
        sealed public class StronglyNamedDependency : ResolveAssemblyReferenceTestFixture
        {
            /// <summary>
            /// Return the default search paths.
            /// </summary>
            /// <value></value>
            new internal string[] DefaultPaths
            {
                get { return new string[] { @"C:\MyApp\v0.5", @"C:\MyApp\v1.0", @"C:\MyApp\v2.0", @"C:\MyApp\v3.0", @"C:\MyComponents\v0.5", @"C:\MyComponents\v1.0", @"C:\MyComponents\v2.0", @"C:\MyComponents\v3.0" }; }
            }


            /// <summary>
            /// In this case,
            /// - Two references are passed in:
            ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
            ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
            /// - The AutoUnify flag is set to 'true'.
            /// - Version 1.0.0.0 of UnifyMe exists.
            /// - Version 2.0.0.0 of UnifyMe exists.
            /// Expected:
            /// - There should be exactly one UnifyMe dependency returned and it should be version 2.0.0.0.
            /// Rationale:
            /// When AutoUnify is true, we need to resolve to the highest version of each particular assembly 
            /// dependency seen.
            /// </summary>
            [Fact]
            public void Exists()
            {
                // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
                // times out the object used for remoting console writes.  Adding a write in the middle of
                // keeps remoting from timing out the object.
                Console.WriteLine("Performing VersioningAndUnification.AutoUnify.StronglyNamedDependency.Exists() test");

                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                        new TaskItem("DependsOnUnified, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AutoUnify = true;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                AssertNoCase("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
                AssertNoCase(@"C:\MyComponents\v2.0\UnifyMe.dll", t.ResolvedDependencyFiles[0].ItemSpec);

                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnifiedDependency"), "UniFYme, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                );

                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "1.0.0.0", @"C:\MyApp\v1.0\DependsOnUnified.dll")
                );
            }

            /// <summary>
            /// In this case,
            /// - Two references are passed in:
            ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
            ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
            /// - The AutoUnify flag is set to 'true'.
            /// - Version 1.0.0.0 of UnifyMe exists.
            /// - Version 2.0.0.0 of UnifyMe exists.
            ///   - DependsOnUnified 2.0.0.0 is on the black list. 
            /// Expected:
            /// - There should be exactly one UnifyMe dependency returned and it should be version 1.0.0.0.
            /// Rationale:
            /// When AutoUnify is true, we need to resolve to the highest version of each particular assembly 
            /// dependency seen. However if the higher assembly is a dependency of an assembly in the black list it should not be considered during unification.
            /// </summary>
            [Fact]
            public void ExistsWithPrimaryReferenceOnBlackList()
            {
                string implicitRedistListContents =
                          "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                              "<File AssemblyName='DependsOnUnified' Version='2.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                          "</FileList >";

                string engineOnlySubset =
                  "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                      "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "</FileList >";

                string redistListPath = FileUtilities.GetTemporaryFile();
                string subsetListPath = FileUtilities.GetTemporaryFile();
                try
                {
                    File.WriteAllText(redistListPath, implicitRedistListContents);
                    File.WriteAllText(subsetListPath, engineOnlySubset);


                    // Create the engine.
                    MockEngine engine = new MockEngine();

                    ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                        new TaskItem("DependsOnUnified, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                    // Now, pass feed resolved primary references into ResolveAssemblyReference.
                    ResolveAssemblyReference t = new ResolveAssemblyReference();

                    t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                    t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };
                    t.BuildEngine = engine;
                    t.Assemblies = assemblyNames;
                    t.SearchPaths = DefaultPaths;
                    t.AutoUnify = true;

                    bool succeeded = Execute(t);

                    Assert.True(succeeded);
                    Assert.Equal(1, t.ResolvedFiles.Length); // "Expected there to only be one resolved file"
                    Assert.True(t.ResolvedFiles[0].ItemSpec.Contains(@"C:\MyApp\v1.0\DependsOnUnified.dll")); // "Expected the ItemSpec of the resolved file to be the item spec of the 1.0.0.0 assembly"
                    Assert.Equal(1, t.ResolvedDependencyFiles.Length); // "Expected there to be two resolved dependencies"
                    AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
                    AssertNoCase(@"C:\MyComponents\v1.0\UnifyMe.dll", t.ResolvedDependencyFiles[0].ItemSpec);

                    engine.AssertLogDoesntContain
                    (
                        String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnifiedDependency"), "UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL")
                    );

                    engine.AssertLogDoesntContain
                    (
                        String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "1.0.0.0", @"C:\MyApp\v2.0\DependsOnUnified.dll")
                    );
                }
                finally
                {
                    File.Delete(redistListPath);
                    File.Delete(subsetListPath);
                }
            }


            /// <summary>
            /// In this case,
            /// - Two references are passed in:
            ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
            ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
            /// - The AutoUnify flag is set to 'true'.
            /// - Version 1.0.0.0 of UnifyMe exists.
            /// - Version 2.0.0.0 of UnifyMe exists.
            /// - UnifyMe 2.0.0.0 is on the black list
            /// Expected:
            /// - There should be exactly one UnifyMe dependency returned and it should be version 1.0.0.0.
            ///  Also there should be a warning about the primary reference DependsOnUnified 2.0.0.0 having a dependency which was in the black list.
            /// Rationale:
            /// When AutoUnify is true, we need to resolve to the highest version of each particular assembly 
            /// dependency seen. However if the higher assembly is a dependency of an assembly in the black list it should not be considered during unification.
            /// </summary>
            [Fact]
            public void ExistsPromotedDependencyInTheBlackList()
            {
                string implicitRedistListContents =
                                   "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                                       "<File AssemblyName='UniFYme' Version='2.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                                   "</FileList >";

                string engineOnlySubset =
                  "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                      "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "</FileList >";

                string redistListPath = FileUtilities.GetTemporaryFile();
                string subsetListPath = FileUtilities.GetTemporaryFile();
                string appConfigFile = null;
                try
                {
                    File.WriteAllText(redistListPath, implicitRedistListContents);
                    File.WriteAllText(subsetListPath, engineOnlySubset);


                    // Create the engine.
                    MockEngine engine = new MockEngine();

                    ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                    // Construct the app.config.
                    appConfigFile = WriteAppConfig
                    (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                    );

                    // Now, pass feed resolved primary references into ResolveAssemblyReference.
                    ResolveAssemblyReference t = new ResolveAssemblyReference();
                    t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                    t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };

                    t.BuildEngine = engine;
                    t.Assemblies = assemblyNames;
                    t.SearchPaths = DefaultPaths;
                    t.AppConfigFile = appConfigFile;

                    bool succeeded = Execute(t, false);

                    Assert.True(succeeded);
                    Assert.Equal(0, t.ResolvedDependencyFiles.Length);
                    engine.AssertLogDoesntContain
                    (
                        String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, @"C:\MyApp\v1.0\DependsOnUnified.dll")
                    );
                }
                finally
                {
                    File.Delete(redistListPath);
                    File.Delete(subsetListPath);

                    // Cleanup.
                    File.Delete(appConfigFile);
                }
            }

            /// <summary>
            /// In this case,
            /// - Two references are passed in:
            ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
            ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
            /// - The AutoUnify flag is set to 'true'.
            /// - Version 1.0.0.0 of UnifyMe exists.
            /// - Version 2.0.0.0 of UnifyMe exists.
            ///   - UnifyMe 2.0.0.0 is on the black list because it is higher than what is in the redist list, 1.0.0.0 is also in a black list because it is not in the subset but is in the redist list.
            /// Expected:
            /// - There should be no UnifyMe dependency returned 
            /// There should be a warning indicating the primary reference DependsOnUnified 1.0.0.0 has a dependency that in the black list
            /// There should be a warning indicating the primary reference DependsOnUnified 2.0.0.0 has a dependency that in the black list
            /// </summary>
            [Fact]
            public void ExistsWithBothDependentReferenceOnBlackList()
            {
                string implicitRedistListContents =
                          "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                              "<File AssemblyName='UniFYme' Version='1.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                          "</FileList >";

                string engineOnlySubset =
                  "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                      "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "</FileList >";

                string redistListPath = FileUtilities.GetTemporaryFile();
                string subsetListPath = FileUtilities.GetTemporaryFile();
                try
                {
                    File.WriteAllText(redistListPath, implicitRedistListContents);
                    File.WriteAllText(subsetListPath, engineOnlySubset);


                    // Create the engine.
                    MockEngine engine = new MockEngine();

                    ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                        new TaskItem("DependsOnUnified, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                    // Now, pass feed resolved primary references into ResolveAssemblyReference.
                    ResolveAssemblyReference t = new ResolveAssemblyReference();

                    t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                    t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };
                    t.BuildEngine = engine;
                    t.Assemblies = assemblyNames;
                    t.SearchPaths = DefaultPaths;
                    t.AutoUnify = true;

                    bool succeeded = Execute(t, false);

                    Assert.True(succeeded);
                    Assert.Equal(0, t.ResolvedFiles.Length); // "Expected there to be no resolved files"

                    Assert.False(ContainsItem(t.ResolvedFiles, @"C:\MyApp\v1.0\DependsOnUnified.dll")); // "Expected the ItemSpec of the resolved file to not be the item spec of the 1.0.0.0 assembly"
                    Assert.False(ContainsItem(t.ResolvedFiles, @"C:\MyApp\v2.0\DependsOnUnified.dll")); // "Expected the ItemSpec of the resolved file to not be the item spec of the 2.0.0.0 assembly"
                    string stringList = ResolveAssemblyReference.GenerateSubSetName(null, new ITaskItem[] { new TaskItem(subsetListPath) });
                    engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", assemblyNames[0].ItemSpec, "UniFYme, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", stringList));
                    engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", assemblyNames[1].ItemSpec, "UniFYme, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "2.0.0.0", "1.0.0.0"));
                }
                finally
                {
                    File.Delete(redistListPath);
                    File.Delete(subsetListPath);
                }
            }

            /// <summary>
            /// In this case,
            /// - Three references are passed in:
            ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
            ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
            ///   - DependsOnUnified 3.0.0.0 depends on UnifyMe 3.0.0.0.
            /// - The AutoUnify flag is set to 'true'.
            /// - Version 1.0.0.0 of UnifyMe exists.
            /// - Version 2.0.0.0 of UnifyMe exists.
            /// - Version 3.0.0.0 of UnifyMe exists.
            /// - Vesion 3.0.0.0 of DependsOn is on black list
            /// Expected:
            /// - There should be exactly one UnifyMe dependency returned and it should be version 2.0.0.0.
            /// - There should be messages saying that 2.0.0.0 was unified from 1.0.0.0.
            /// Rationale:
            /// AutoUnify works even when unifying multiple prior versions.
            /// </summary>
            [Fact]
            public void MultipleUnifiedFromNamesMiddlePrimaryOnBlackList()
            {
                string implicitRedistListContents =
           "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
               "<File AssemblyName='DependsOnUnified' Version='3.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
           "</FileList >";

                string engineOnlySubset =
                  "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                      "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "</FileList >";

                string redistListPath = FileUtilities.GetTemporaryFile();
                string subsetListPath = FileUtilities.GetTemporaryFile();
                File.WriteAllText(redistListPath, implicitRedistListContents);
                File.WriteAllText(subsetListPath, engineOnlySubset);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                        new TaskItem("DependsOnUnified, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                        new TaskItem("DependsOnUnified, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };
                t.AutoUnify = true;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(2, t.ResolvedFiles.Length); // "Expected to find two resolved assemblies"
                Assert.True(ContainsItem(t.ResolvedFiles, @"C:\MyApp\v1.0\DependsOnUnified.dll")); // "Expected the ItemSpec of the resolved file to be the item spec of the 1.0.0.0 assembly"
                Assert.True(ContainsItem(t.ResolvedFiles, @"C:\MyApp\v2.0\DependsOnUnified.dll")); // "Expected the ItemSpec of the resolved file to be the item spec of the 2.0.0.0 assembly"
                AssertNoCase("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
                AssertNoCase(@"C:\MyComponents\v2.0\UnifyMe.dll", t.ResolvedDependencyFiles[0].ItemSpec);

                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnifiedDependency"), "UniFYme, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                );

                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "1.0.0.0", @"C:\MyApp\v1.0\DependsOnUnified.dll")
                );

                engine.AssertLogDoesntContain
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "2.0.0.0", @"C:\MyApp\v2.0\DependsOnUnified.dll")
                );
            }

            /// <summary>
            /// In this case,
            /// - Two references are passed in:
            ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
            ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
            ///   - DependsOnUnified 3.0.0.0 depends on UnifyMe 2.0.0.0.
            /// - The AutoUnify flag is set to 'true'.
            /// - Version 1.0.0.0 of UnifyMe exists.
            /// - Version 2.0.0.0 of UnifyMe exists.
            /// - Version 3.0.0.0 of UnifyMe exists.
            /// Expected:
            /// - There should be exactly one UnifyMe dependency returned and it should be version 3.0.0.0.
            /// - There should be messages saying that 3.0.0.0 was unified from 1.0.0.0 *and* 2.0.0.0.
            /// Rationale:
            /// AutoUnify works even when unifying multiple prior versions.
            /// </summary>
            [Fact]
            public void MultipleUnifiedFromNames()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                        new TaskItem("DependsOnUnified, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                        new TaskItem("DependsOnUnified, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };
                t.AutoUnify = true;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                AssertNoCase("UnifyMe, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
                AssertNoCase(@"C:\MyComponents\v3.0\UnifyMe.dll", t.ResolvedDependencyFiles[0].ItemSpec);

                engine.AssertLogContains
                (
                   String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnifiedDependency"), "UniFYme, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                );

                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "1.0.0.0", @"C:\MyApp\v1.0\DependsOnUnified.dll")
                );

                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "2.0.0.0", @"C:\MyApp\v2.0\DependsOnUnified.dll")
                );
            }

            /// <summary>
            /// In this case,
            /// - Two references are passed in:
            ///   - DependsOnUnified 0.5.0.0 depends on UnifyMe 0.5.0.0.
            ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 2.0.0.0.
            /// - The AutoUnify flag is set to 'true'.
            /// - Version 0.5.0.0 of UnifyMe *does not* exist.
            /// - Version 1.0.0.0 of UnifyMe exists.
            /// Expected:
            /// - There should be exactly one UnifyMe dependency returned and it should be version 1.0.0.0.
            /// - There should be message saying that 1.0.0.0 was unified from 0.5.0.0
            /// Rationale:
            /// AutoUnify works even when unifying prior versions that don't exist on disk.
            /// </summary>
            [Fact]
            public void LowVersionDoesntExist()
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("DependsOnUnified, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                        new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                    };


                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AutoUnify = true;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedDependencyFiles.Length);
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
                engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "0.5.0.0", @"C:\MyApp\v0.5\DependsOnUnified.dll")
                );
            }
        }
    }

    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task.
    /// </summary>
    sealed public class ReferenceTests : ResolveAssemblyReferenceTestFixture
    {
        /// <summary>
        /// Check to make sure if, the specific version metadata is set on a primary reference, that true is returned from CheckForSpecificMetadataOnParent
        /// </summary>
        [Fact]
        public void CheckForSpecificMetadataOnParent()
        {
            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem = new TaskItem("TestReference");
            taskItem.SetMetadata("SpecificVersion", "true");
            reference.MakePrimaryAssemblyReference(taskItem, true, ".dll");
            Assert.True(reference.CheckForSpecificVersionMetadataOnParentsReference(false));
        }

        /// <summary>
        /// Check to make sure if, the specific version metadata is set on all primary references which a dependency depends on, that true is returned from CheckForSpecificMetadataOnParent
        /// </summary>
        [Fact]
        public void CheckForSpecificMetadataOnParentAllParentsHaveMetadata()
        {
            Reference primaryReference1 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem = new TaskItem("TestPrimary1");
            taskItem.SetMetadata("SpecificVersion", "true");
            primaryReference1.MakePrimaryAssemblyReference(taskItem, true, ".dll");
            primaryReference1.FullPath = "FullPath";

            Reference primaryReference2 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem2 = new TaskItem("TestPrimary2");
            taskItem2.SetMetadata("SpecificVersion", "true");
            primaryReference2.MakePrimaryAssemblyReference(taskItem2, true, ".dll");
            primaryReference2.FullPath = "FullPath";

            Reference dependentReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            dependentReference.FullPath = "FullPath";

            dependentReference.MakeDependentAssemblyReference(primaryReference1);
            dependentReference.MakeDependentAssemblyReference(primaryReference2);

            Assert.True(dependentReference.CheckForSpecificVersionMetadataOnParentsReference(false));
        }

        /// <summary>
        /// Check to make sure if, the specific version metadata is set on some primary references which a dependency depends on, that false is returned from CheckForSpecificMetadataOnParent
        /// </summary>
        [Fact]
        public void CheckForSpecificMetadataOnParentNotAllParentsHaveMetadata()
        {
            Reference primaryReference1 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem = new TaskItem("TestPrimary1");
            taskItem.SetMetadata("SpecificVersion", "false");
            primaryReference1.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            primaryReference1.FullPath = "FullPath";

            Reference primaryReference2 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem2 = new TaskItem("TestPrimary2");
            taskItem2.SetMetadata("SpecificVersion", "true");
            primaryReference2.MakePrimaryAssemblyReference(taskItem2, true, ".dll");
            primaryReference2.FullPath = "FullPath";

            Reference dependentReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            dependentReference.FullPath = "FullPath";

            dependentReference.MakeDependentAssemblyReference(primaryReference1);
            dependentReference.MakeDependentAssemblyReference(primaryReference2);

            Assert.False(dependentReference.CheckForSpecificVersionMetadataOnParentsReference(false)); // "Expected check to return false but it returned true."
        }

        /// <summary>
        /// Check to make sure if, the specific version metadata is set on some primary references which a dependency depends on, that false is returned from CheckForSpecificMetadataOnParent
        /// </summary>
        [Fact]
        public void CheckForSpecificMetadataOnParentNotAllParentsHaveMetadata2()
        {
            Reference primaryReference1 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem = new TaskItem("TestPrimary1");
            primaryReference1.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            primaryReference1.FullPath = "FullPath";

            Reference primaryReference2 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem2 = new TaskItem("TestPrimary2");
            taskItem2.SetMetadata("SpecificVersion", "true");
            primaryReference2.MakePrimaryAssemblyReference(taskItem2, true, ".dll");
            primaryReference2.FullPath = "FullPath";

            Reference dependentReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            dependentReference.FullPath = "FullPath";

            dependentReference.MakeDependentAssemblyReference(primaryReference1);
            dependentReference.MakeDependentAssemblyReference(primaryReference2);

            Assert.False(dependentReference.CheckForSpecificVersionMetadataOnParentsReference(false)); // "Expected check to return false but it returned true."
        }

        /// <summary>
        /// Check to make sure if, the specific version metadata is set on some primary references which a dependency depends on, that true is returned from CheckForSpecificMetadataOnParent if the anyParentHasmetadata parameter is set to true.
        /// </summary>
        [Fact]
        public void CheckForSpecificMetadataOnParentNotAllParentsHaveMetadata3()
        {
            Reference primaryReference1 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem = new TaskItem("TestPrimary1");
            taskItem.SetMetadata("SpecificVersion", "false");
            primaryReference1.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            primaryReference1.FullPath = "FullPath";

            Reference primaryReference2 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            ITaskItem taskItem2 = new TaskItem("TestPrimary2");
            taskItem2.SetMetadata("SpecificVersion", "true");
            primaryReference2.MakePrimaryAssemblyReference(taskItem2, true, ".dll");
            primaryReference2.FullPath = "FullPath";

            Reference dependentReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            dependentReference.FullPath = "FullPath";

            dependentReference.MakeDependentAssemblyReference(primaryReference1);
            dependentReference.MakeDependentAssemblyReference(primaryReference2);

            Assert.True(dependentReference.CheckForSpecificVersionMetadataOnParentsReference(true)); // "Expected check to return false but it returned true."
        }
    }


    /// <summary>
    /// Test a few perf scenarios.
    /// </summary>
    sealed public class Perf : ResolveAssemblyReferenceTestFixture
    {
        [Fact]
        public void AutoUnifyUsesMinimumIO()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Perf.AutoUnifyUsesMinimumIO() test");

            // Manually instantiate a test fixture and run it.
            VersioningAndUnification.AutoUnify.StronglyNamedDependency t = new VersioningAndUnification.AutoUnify.StronglyNamedDependency();
            t.StartIOMonitoring();
            t.Exists();
            t.StopIOMonitoringAndAssert_Minimal_IOUse();
        }
    }



    /// <summary>
    /// Unit test the cases where we need to determine if the target framework is greater than the current target framework through the use of the target framework attribute
    /// </summary>
    sealed public class VerifyTargetFrameworkAttribute : ResolveAssemblyReferenceTestFixture
    {
        /// <summary>
        /// Verify there are no warnings if the target framework identifier passed to rar and the target framework identifier in the dll do not match.
        /// </summary>
        [Fact]
        public void FrameworksDoNotMatch()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnFoo4Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "BAR, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "BAR";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo4Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework but we are a lower version. With a primary reference in the project.
        /// </summary>
        [Fact]
        public void LowerVersionSameFrameworkDirect()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnFoo35Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=v4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo35Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework and the same version and a direct reference
        /// </summary>
        [Fact]
        public void SameVersionSameFrameworkDirect()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnFoo4Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo4Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if the reference was built for a higher framework but specific version is true
        /// </summary>
        [Fact]
        public void HigherVersionButSpecificVersionDirect()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral");
            item.SetMetadata("SpecificVersion", "true");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework but we are a lower version.
        /// </summary>
        [Fact]
        public void LowerVersionSameFrameworkInDirect()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("IndirectDependsOnFoo35Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=v4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\IndirectDependsOnFoo35Framework.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Frameworks\DependsOnFoo35Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework and the same version.
        /// </summary>
        [Fact]
        public void SameVersionSameFrameworkInDirect()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("IndirectDependsOnFoo4Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\IndirectDependsOnFoo4Framework.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Frameworks\DependsOnFoo4Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework and a higher version but specific version is true.
        /// </summary>
        [Fact]
        public void HigherVersionButSpecificVersionInDirect()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("IndirectDependsOnFoo45Framework, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral");
            item.SetMetadata("SpecificVersion", "true");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\IndirectDependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Frameworks\DependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are warnings if there is an indirect reference to a dll that is higher that what the current target framework is.
        /// </summary>
        [Fact]
        public void HigherVersionInDirect()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("IndirectDependsOnFoo45Framework, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t, false);

            Assert.Equal(1, e.Warnings); // "One warning expected in this scenario."
            e.AssertLogContains("MSB3275");
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);
            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
        }

        /// <summary>
        /// Verify there are no warnings if there is an indirect reference to a dll that is higher that what the current target framework is but IgnoreFrameworkAttributeVersionMismatch is true.
        /// </summary>
        [Fact]
        public void HigherVersionInDirectIgnoreMismatch()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("IndirectDependsOnFoo45Framework, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            t.IgnoreTargetFrameworkAttributeVersionMismatch = true;
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\IndirectDependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Frameworks\DependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if there is a direct reference to a dll that is higher that what the current target framework is but the property IgnoreFrameworkAttributeVersionMismatch is true.
        /// </summary>
        [Fact]
        public void HigherVersionDirectIgnoreMismatch()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOnFoo45Framework");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            t.IgnoreTargetFrameworkAttributeVersionMismatch = true;

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
        }


        /// <summary>
        /// Verify there are warnings if there is a direct reference to a dll that is higher that what the current target framework is.
        /// </summary>
        [Fact]
        public void HigherVersionDirect()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOnFoo45Framework");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t, false);

            Assert.Equal(1, e.Warnings); // "One warning expected in this scenario."
            e.AssertLogContains("MSB3274");
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);
            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
        }

        /// <summary>
        /// Verify there are no warnings if there is a direct reference to a dll that is higher that what the current target framework is but 
        /// find dependencies is false. This is because we do not want to add an extra read for this attribute during the project load phase. 
        /// which has dependencies set to false.  A regular build or design time build has this set to true so we do the correct check.
        /// </summary>
        [Fact]
        public void HigherVersionDirectDependenciesFalse()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOnFoo45Framework");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.FindDependencies = false;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Assert.True(t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader));


            Assert.Equal(0, e.Warnings); // "No warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
        }
    }

    /// <summary>
    /// Unit test the cases where we need to determine if the target framework is greater than the current target framework
    /// </summary>
    sealed public class VerifyTargetFrameworkHigherThanRedist : ResolveAssemblyReferenceTestFixture
    {
        /// <summary>
        /// Verify there are no warnings when the assembly being resolved is not in the redist list and only has dependencies to references in the redist list with the same
        /// version as is described in the redist list.
        /// </summary>
        [Fact]
        public void TargetCurrentTargetFramework()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnOnlyv4Assemblies")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOnOnlyv4Assemblies.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// ReferenceVersion9 depends on mscorlib 9. However the redist list only allows 4.0 since framework unification for dependencies only
        /// allows upward unification this would result in a warning. Therefore we need to remap mscorlib 9 to 4.0
        /// 
        /// </summary>
        [Fact]
        public void RemapAssemblyBasic()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("ReferenceVersion9"),
                new TaskItem("DependsOnOnlyv4Assemblies"),
                new TaskItem("AnotherOne")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='mscorlib' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                       "<Remap>" +
                                          "<From AssemblyName='mscorlib' Version='9.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true'>" +
                                          "   <To AssemblyName='mscorlib' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                          " </From>" +
                                           "<From AssemblyName='DependsOnOnlyv4Assemblies'>" +
                                          "   <To AssemblyName='ReferenceVersion9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' />" +
                                          " </From>" +
                                           "<From AssemblyName='AnotherOne'>" +
                                          "   <To AssemblyName='ReferenceVersion9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' />" +
                                          " </From>" +
                                       "</Remap>" +
                                      "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(0, e.Warnings); // "Expected NO warning in this scenario."
            e.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.RemappedReference", "DependsOnOnlyv4Assemblies", "ReferenceVersion9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            e.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.RemappedReference", "AnotherOne", "ReferenceVersion9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");

            Assert.Equal(1, t.ResolvedFiles.Length);

            Assert.True(t.ResolvedFiles[0].GetMetadata("OriginalItemSpec").Equals("AnotherOne", StringComparison.OrdinalIgnoreCase));

            Assert.True(t.ResolvedFiles[0].ItemSpec.Equals(@"c:\MyComponents\misc\ReferenceVersion9.dll", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify an error is emitted when the reference itself is in the redist list but is a higher version that is described in the redist list. 
        /// In this case ReferenceVersion9 is version=9.0.0.0 but in the redist we show its highest version as 4.0.0.0.
        /// </summary>
        [Fact]
        public void HigherThanHighestInRedistList()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("ReferenceVersion9")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='ReferenceVersion9' Version='4.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains("MSB3257");
            e.AssertLogContains("ReferenceVersion9");
            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Verify that if the reference that is higher than the highest version in the redist list is an MSBuild assembly, we do 
        /// not warn -- this is a hack until we figure out how to properly deal with .NET assemblies being removed from the framework.
        /// </summary>
        [Fact]
        public void HigherThanHighestInRedistListForMSBuildAssembly()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='Microsoft.Build' Version='4.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t1 = new ResolveAssemblyReference();
            t1.TargetFrameworkVersion = "v4.5";

            ExecuteRAROnItemsAndRedist(t1, e, items, redistString, false);

            Assert.Equal(0, e.Warnings); // "Expected successful resolution with no warnings."
            e.AssertLogContains("Microsoft.Build.dll");
            Assert.Equal(1, t1.ResolvedFiles.Length);

            ResolveAssemblyReference t2 = new ResolveAssemblyReference();
            t2.TargetFrameworkVersion = "v4.0";

            ExecuteRAROnItemsAndRedist(t2, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains("Microsoft.Build.dll");
            Assert.Equal(0, t2.ResolvedFiles.Length);

            ResolveAssemblyReference t3 = new ResolveAssemblyReference();
            t3.TargetFrameworkVersion = "v4.5";
            t3.UnresolveFrameworkAssembliesFromHigherFrameworks = true;

            ExecuteRAROnItemsAndRedist(t3, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains("Microsoft.Build.dll");
            Assert.Equal(1, t1.ResolvedFiles.Length);
        }

        /// <summary>
        /// Expect no warning from a 3rd party redist list since they are not considered for multi targeting warnings.
        /// </summary>
        [Fact]
        public void HigherThanHighestInRedistList3rdPartyRedist()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("ReferenceVersion9")
            };

            string redistString = "<FileList Redist='MyRandomREdist' >" +
                                      "<File AssemblyName='mscorlib' Version='4.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(0, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogDoesntContain("MSB3257");
            e.AssertLogContains("ReferenceVersion9");
            Assert.Equal(1, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Test the same case as above except for add the specific version metadata to ignore the warning.
        /// </summary>
        [Fact]
        public void HigherThanHighestInRedistListWithSpecificVersionMetadata()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("ReferenceVersion9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            items[0].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='ReferenceVersion9' Version='4.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            e.AssertLogDoesntContain("MSB3258");
            e.AssertLogDoesntContain("MSB3257");
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\ReferenceVersion9.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify the case where the assembly itself is not in the redist list but it depends on an assembly which is in the redist list and is a higher version that what is listed in the redist list.
        /// In this case the assembly DependsOn9 depends on System 9.0.0.0 while the redist list only goes up to 4.0.0.0.
        /// </summary>
        [Fact]
        public void DependenciesHigherThanHighestInRedistList()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOn9")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                      "<File AssemblyName='System.Data' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(2, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", "DependsOn9", "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "9.0.0.0", "4.0.0.0"));
            e.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", "DependsOn9", "System.Data, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "9.0.0.0", "4.0.0.0"));
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Verify that if the reference that is higher than the highest version in the redist list is an MSBuild assembly, we do 
        /// not warn -- this is a hack until we figure out how to properly deal with .NET assemblies being removed from the framework.
        /// </summary>
        [Fact]
        public void DependenciesHigherThanHighestInRedistListForMSBuildAssembly()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnMSBuild12")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='Microsoft.Build' Version='4.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t1 = new ResolveAssemblyReference();
            t1.TargetFrameworkVersion = "v5.0";

            ExecuteRAROnItemsAndRedist(t1, e, items, redistString, false);

            Assert.Equal(0, e.Warnings); // "Expected successful resolution with no warnings."
            e.AssertLogContains("DependsOnMSBuild12");
            e.AssertLogContains("Microsoft.Build.dll");
            Assert.Equal(1, t1.ResolvedFiles.Length);

            ResolveAssemblyReference t2 = new ResolveAssemblyReference();
            t2.TargetFrameworkVersion = "v4.0";

            ExecuteRAROnItemsAndRedist(t2, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario"
            e.AssertLogContains("DependsOnMSBuild12");
            e.AssertLogContains("Microsoft.Build.dll");
            Assert.Equal(0, t2.ResolvedFiles.Length);

            ResolveAssemblyReference t3 = new ResolveAssemblyReference();
            //t2.TargetFrameworkVersion is null

            ExecuteRAROnItemsAndRedist(t3, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario"
            e.AssertLogContains("DependsOnMSBuild12");
            e.AssertLogContains("Microsoft.Build.dll");
            Assert.Equal(0, t3.ResolvedFiles.Length);
        }

        /// <summary>
        /// Make sure when specific version is set to true and the dependencies of the reference are a higher version than what is in the redist list do not warn, do not unresolve
        /// </summary>
        [Fact]
        public void DependenciesHigherThanHighestInRedistListSpecificVersionMetadata()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            items[0].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                     "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                     "<File AssemblyName='System.Data' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            e.AssertLogDoesntContain("MSB3258");
            e.AssertLogDoesntContain("MSB3257");
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOn9.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify the case where two assemblies depend on an assembly which is in the redist list but has a higher version than what is described in the redist list.
        /// DependsOn9 and DependsOn9Also both depend on System, Version=9.0.0.0 one of the items has the SpecificVersion metadata set. In this case
        /// we expect to only see a warning from one of the assemblies.
        /// </summary>
        [Fact]
        public void TwoDependenciesHigherThanHighestInRedistListIgnoreOnOne()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089"),
                new TaskItem("DependsOn9Also")
            };

            items[0].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", "DependsOn9Also", "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "9.0.0.0", "4.0.0.0"));
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOn9.dll")); // "Expected to not find assembly, but did."
            Assert.False(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOn9Also.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify the case where two assemblies depend on an assembly which is in the redist list but has a higher version than what is described in the redist list.
        /// DependsOn9 and DependsOn9Also both depend on System, Version=9.0.0.0. Both of the items has the specificVersion metadata set. In this case
        /// we expect to only see no warnings from the assemblies.
        /// </summary>
        [Fact]
        public void TwoDependenciesHigherThanHighestInRedistListIgnoreOnBoth()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089"),
                new TaskItem("DependsOn9Also, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            items[0].SetMetadata("SpecificVersion", "true");
            items[1].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            e.AssertLogDoesntContain("MSB3258");
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOn9.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOn9Also.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Test the case where two assemblies with different versions but the same name depend on an assembly which is in the redist list but has a higher version than 
        /// what is described in the redist list. We expect two warnings because both assemblies are goign to be resolved even though one of them will not be copy local.
        /// </summary>
        [Fact]
        public void TwoDependenciesSameNameDependOnHigherVersion()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOn9, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089"),
                new TaskItem("DependsOn9, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(2, e.Warnings); // "Expected two warnings."
            e.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", "DependsOn9, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089", "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "9.0.0.0", "4.0.0.0"));
            e.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", "DependsOn9, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089", "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "9.0.0.0", "4.0.0.0"));
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Test the case where the project has two references, one of them has dependencies which are contained within the projects target framework
        /// and there is another reference which has dependencies on a future framework (this is the light up scenario assembly).
        /// 
        /// Make sure that if specific version is set on the lightup assembly that we do not unresolve it, and we also should not unify its dependencies.
        /// </summary>
        [Fact]
        public void MixedDependenciesSpecificVersionOnHigherVersionMetadataSet()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089"),
                new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            items[1].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            List<string> additionalPaths = new List<string>();
            additionalPaths.Add(@"c:\MyComponents\4.0Component\");
            additionalPaths.Add(@"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion");
            additionalPaths.Add(@"c:\WINNT\Microsoft.NET\Framework\v9.0.MyVersion\");


            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false, additionalPaths);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(2, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\4.0Component\DependsOnOnlyv4Assemblies.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOn9.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Test the case where the project has two references, one of them has dependencies which are contained within the projects target framework
        /// and there is another reference which has dependencies on a future framework (this is the light up scenario assembly).
        /// 
        /// Verify that if specific version is set on the other reference that we get the expected behavior:
        /// Un resolve the light up assembly.
        /// </summary>
        [Fact]
        public void MixedDependenciesSpecificVersionOnLowerVersionMetadataSet()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089"),
                new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            items[0].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            List<string> additionalPaths = new List<string>();
            additionalPaths.Add(@"c:\MyComponents\4.0Component\");
            additionalPaths.Add(@"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion");
            additionalPaths.Add(@"c:\WINNT\Microsoft.NET\Framework\v9.0.MyVersion\");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false, additionalPaths);

            Assert.Equal(1, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\4.0Component\DependsOnOnlyv4Assemblies.dll")); // "Expected to find assembly, but didn't."
            Assert.False(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOn9.dll")); // "Expected to find assembly, but didn't."
        }
    }


    /// <summary>
    /// Unit test the cases where we need to determine if the target framework is greater than the current target framework
    /// </summary>
    sealed public class VerifyIgnoreVersionForFrameworkReference : ResolveAssemblyReferenceTestFixture
    {
        /// <summary>
        /// Verify that we ignore the version information on the assembly
        /// </summary>
        [Fact]
        public void IgnoreVersionBasic()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOn9, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='DependsOn9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.IgnoreVersionForFrameworkReferences = true;
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);


            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOn9.dll")); // "Expected to find assembly, but didn't."

            // Do the resolution without the metadata, expect it to not work since we should not be able to find Dependson9 version 10.0.0.0
            e = new MockEngine();

            item = new TaskItem("DependsOn9, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");

            items = new ITaskItem[]
            {
                item
            };

            redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                     "<File AssemblyName='DependsOn9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                 "</FileList >";

            t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains("MSB3257");
            e.AssertLogContains("DependsOn9");
            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Verify that we ignore the version information on the assembly
        /// </summary>
        [Fact]
        public void IgnoreVersionBasicTestMetadata()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOn9, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            item.SetMetadata("IgnoreVersionForFrameworkReference", "True");


            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='DependsOn9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOn9.dll")); // "Expected to find assembly, but didn't."

            // Do the resolution without the metadata, expect it to not work since we should not be able to find Dependson9 version 10.0.0.0
            e = new MockEngine();

            item = new TaskItem("DependsOn9, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");

            items = new ITaskItem[]
            {
                item
            };

            redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                     "<File AssemblyName='DependsOn9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                 "</FileList >";

            t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains("MSB3257");
            e.AssertLogContains("DependsOn9");
            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Verify that we ignore the version information on the assembly
        /// </summary>
        [Fact]
        public void IgnoreVersionDisableIfSpecificVersionTrue()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            item.SetMetadata("IgnoreVersionForFrameworkReference", "True");
            item.SetMetadata("SpecificVersion", "True");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='DependsOn9' Version='2.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyComponents\misc\DependsOn9.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify that we ignore the version information on the assembly
        /// </summary>
        [Fact]
        public void IgnoreVersionDisableIfHintPath()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            item.SetMetadata("IgnoreVersionForFrameworkReference", "True");
            item.SetMetadata("HintPath", @"c:\MyComponents\misc\DependsOn9.dll");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='DependsOn9' Version='2.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);


            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains("MSB3257");
            e.AssertLogContains("DependsOn9");
            Assert.Equal(0, t.ResolvedFiles.Length);
        }
    }

    /// <summary>
    /// Unit tests for the InstalledSDKResolver task.
    /// </summary>
    sealed public class InstalledSDKResolverFixture : ResolveAssemblyReferenceTestFixture
    {
        /// <summary>
        /// Verify that we do not find the winmd file even if it on the search path if the sdkname does not match something passed into the ResolvedSDKs property.
        /// </summary>
        [Fact]
        public void SDkNameNotInResolvedSDKListButOnSearchPath()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();
            TaskItem taskItem = new TaskItem(@"SDKWinMD");
            taskItem.SetMetadata("SDKName", "NotInstalled, Version=1.0");

            TaskItem[] assemblies = new TaskItem[] { taskItem };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblies;
            t.SearchPaths = new String[] { @"C:\FakeSDK\References" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(0, t.ResolvedFiles.Length);

            Assert.Equal(0, engine.Errors);
            Assert.Equal(1, engine.Warnings);
        }

        /// <summary>
        /// Verify when we are trying to match a name which is is the reference assembly directory
        /// </summary>
        [Fact]
        public void SDkNameMatchInRADirectory()
        {
            ResolveSDKFromRefereneAssemblyLocation("DebugX86SDKWinMD", @"C:\FakeSDK\References\Debug\X86\DebugX86SDKWinMD.Winmd");
            ResolveSDKFromRefereneAssemblyLocation("DebugNeutralSDKWinMD", @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKWinMD.Winmd");
            ResolveSDKFromRefereneAssemblyLocation("x86SDKWinMD", @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKWinMD.Winmd");
            ResolveSDKFromRefereneAssemblyLocation("NeutralSDKWinMD", @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKWinMD.Winmd");
            ResolveSDKFromRefereneAssemblyLocation("SDKReference", @"C:\FakeSDK\References\Debug\X86\SDKReference.dll");
            ResolveSDKFromRefereneAssemblyLocation("DebugX86SDKRA", @"C:\FakeSDK\References\Debug\X86\DebugX86SDKRA.dll");
            ResolveSDKFromRefereneAssemblyLocation("DebugNeutralSDKRA", @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKRA.dll");
            ResolveSDKFromRefereneAssemblyLocation("x86SDKRA", @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKRA.dll");
            ResolveSDKFromRefereneAssemblyLocation("NeutralSDKRA", @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKRA.dll");
        }

        private static void ResolveSDKFromRefereneAssemblyLocation(string referenceName, string expectedPath)
        {
            // Create the engine.
            MockEngine engine = new MockEngine();
            TaskItem taskItem = new TaskItem(referenceName);
            taskItem.SetMetadata("SDKName", "FakeSDK, Version=1.0");

            TaskItem resolvedSDK = new TaskItem(@"C:\FakeSDK");
            resolvedSDK.SetMetadata("SDKName", "FakeSDK, Version=1.0");
            resolvedSDK.SetMetadata("TargetedSDKConfiguration", "Debug");
            resolvedSDK.SetMetadata("TargetedSDKArchitecture", "X86");

            TaskItem[] assemblies = new TaskItem[] { taskItem };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblies;
            t.ResolvedSDKReferences = new ITaskItem[] { resolvedSDK };
            t.SearchPaths = new String[] { @"C:\SomeOtherPlace" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.True(t.ResolvedFiles[0].ItemSpec.Equals(expectedPath, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task.
    /// </summary>
    sealed public class WinMDTests : ResolveAssemblyReferenceTestFixture
    {
        #region AssemblyInformationIsWinMDFile Tests

        /// <summary>
        /// Verify a null file path passed in return the fact the file is not a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileNullFilePath()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(null, getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// Verify if a empty file path is passed in that the file is not a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileEmptyFilePath()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(String.Empty, getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// If the file does nto exist then we should report this is not a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileFileDoesNotExistFilePath()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(@"C:\WinMD\SampleDoesNotExist.Winmd", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// The file exists and has the correct windowsruntime metadata, we should report this is a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileGoodFile()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.True(AssemblyInformation.IsWinMDFile(@"C:\WinMD\SampleWindowsRuntimeOnly.Winmd", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// This file is a mixed file with CLR and windowsruntime metadata we should report this is a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileMixedFile()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.True(AssemblyInformation.IsWinMDFile(@"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.True(isManagedWinMD);
        }

        /// <summary>
        /// The file has only CLR metadata we should report this is not a winmd file
        /// </summary>
        [Fact]
        public void IsWinMDFileCLROnlyFile()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(@"C:\WinMD\SampleClrOnly.Winmd", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// The windows runtime string is not correctly formatted, report this is not a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileBadWindowsRuntimeFile()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(@"C:\WinMD\SampleBadWindowsRuntime.Winmd", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// We should report that a regluar net assembly is not a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileRegularNetAssemblyFile()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(@"C:\Framework\Whidbey\System.dll", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// When a project to project reference is passed in we want to verify that 
        /// the winmd references get the correct metadata applied to them 
        /// </summary>
        [Fact]
        public void VerifyP2PHaveCorrectMetadataWinMD()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();
            TaskItem taskItem = new TaskItem(@"C:\WinMD\SampleWindowsRuntimeOnly.Winmd");

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        taskItem
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            t.TargetProcessorArchitecture = "X86";
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(2, t.RelatedFiles.Length);

            bool dllFound = false;
            bool priFound = false;

            foreach (ITaskItem item in t.RelatedFiles)
            {
                if (item.ItemSpec.EndsWith(@"C:\WinMD\SampleWindowsRuntimeOnly.dll"))
                {
                    dllFound = true;
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.imageRuntime).Length);
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.winMDFile).Length);
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.winmdImplmentationFile).Length);
                }
                if (item.ItemSpec.EndsWith(@"C:\WinMD\SampleWindowsRuntimeOnly.pri"))
                {
                    priFound = true;

                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.imageRuntime).Length);
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.winMDFile).Length);
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.winmdImplmentationFile).Length);
                }
            }

            Assert.True(dllFound && priFound); // "Expected to find .dll and .pri related files."
            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFileType).Equals("Native", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).Equals("SampleWindowsRuntimeOnly.dll"));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals("WindowsRuntime 1.0", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// When a project to project reference is passed in we want to verify that 
        /// the winmd references get the correct metadata applied to them 
        /// </summary>
        [Fact]
        public void VerifyP2PHaveCorrectMetadataWinMDManaged()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();
            TaskItem taskItem = new TaskItem(@"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd");

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        taskItem
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(0, t.RelatedFiles.Length);


            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFileType).Equals("Managed", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).Length);
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals("WindowsRuntime 1.0, CLR V2.0.50727", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// When a project to project reference is passed in we want to verify that 
        /// the winmd references get the correct metadata applied to them 
        /// </summary>
        [Fact]
        public void VerifyP2PHaveCorrectMetadataNonWinMD()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                       new TaskItem(@"C:\AssemblyFolder\SomeAssembly.dll")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);

            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(0, t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile).Length);
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when we reference a winmd file as a reference item make sure we ignore the mscorlib.
        /// </summary>
        [Fact]
        public void IgnoreReferenceToMscorlib()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"SampleWindowsRuntimeOnly"), new TaskItem(@"SampleWindowsRuntimeAndClr")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.TargetProcessorArchitecture = "X86";
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            engine.AssertLogDoesntContain("conflict");
        }

        /// <summary>
        /// Verify when we reference a mixed winmd file that we do resolve the reference to the mscorlib
        /// </summary>
        [Fact]
        public void MixedWinMDGoodReferenceToMscorlib()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"SampleWindowsRuntimeAndClr")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.Resolved", @"C:\WinMD\v4\mscorlib.dll");
        }


        /// <summary>
        /// Verify when a winmd file depends on another winmd file that we do resolve the dependency
        /// </summary>
        [Fact]
        public void WinMdFileDependsOnAnotherWinMDFile()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"SampleWindowsRuntimeOnly2")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.TargetProcessorArchitecture = "X86";
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.True(t.ResolvedFiles[0].ItemSpec.Equals(@"C:\WinMD\SampleWindowsRuntimeOnly2.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals(@"WindowsRuntime 1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));

            Assert.True(t.ResolvedDependencyFiles[0].ItemSpec.Equals(@"C:\WinMD\SampleWindowsRuntimeOnly.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals(@"WindowsRuntime 1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(bool.Parse(t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
        }



        /// <summary>
        /// We have two dlls which depend on a winmd, the first dll does not have the winmd beside it, the second one does
        /// we want to make sure that the winmd file is resolved beside the second dll.
        /// </summary>
        [Fact]
        public void ResolveWinmdBesideDll()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\DirectoryContainsOnlyDll\A.dll"),
                        new TaskItem(@"C:\DirectoryContainsdllAndWinmd\B.dll"),
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { "{RAWFILENAME}" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.True(t.ResolvedDependencyFiles[0].ItemSpec.Equals(@"C:\DirectoryContainsdllAndWinmd\C.winmd", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// We have a winmd file and a dll depend on a winmd, there are copies of the winmd beside each of the files. 
        /// we want to make sure that the winmd file is resolved beside the winmd since that is the first file resolved.
        /// </summary>
        [Fact]
        public void ResolveWinmdBesideDll2()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\DirectoryContainstwoWinmd\A.winmd"),
                        new TaskItem(@"C:\DirectoryContainsdllAndWinmd\B.dll"),
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"{RAWFILENAME}" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.True(t.ResolvedDependencyFiles[0].ItemSpec.Equals(@"C:\DirectoryContainstwoWinmd\C.winmd", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when a winmd file depends on another winmd file that itself has framework dependencies that we do not resolve any of the
        /// dependencies due to the winmd to winmd reference
        /// </summary>
        [Fact]
        public void WinMdFileDependsOnAnotherWinMDFileWithFrameworkDependencies()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"SampleWindowsRuntimeOnly3")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"{TargetFrameworkDirectory}", @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion" };
            t.TargetProcessorArchitecture = "x86";
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(4, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);

            Assert.True(t.ResolvedFiles[0].ItemSpec.Equals(@"C:\WinMD\SampleWindowsRuntimeOnly3.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals(@"WindowsRuntime 1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
        }

        /// <summary>
        /// Make sure when a dot net assembly depends on a WinMDFile that 
        /// we get the winmd file resolved. Also make sure that if there is Implementation, ImageRuntime, or IsWinMD set on the dll that 
        /// it does not get propigated to the winmd file dependency.
        /// </summary>
        [Fact]
        public void DotNetAssemblyDependsOnAWinMDFile()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();
            TaskItem item = new TaskItem(@"DotNetAssemblyDependsOnWinMD");
            // This should not be used for anything, it is recalculated in rar, this is to make sure it is not forwarded to child items.
            item.SetMetadata(ItemMetadataNames.imageRuntime, "FOO");
            // This should not be used for anything, it is recalculated in rar, this is to make sure it is not forwarded to child items.
            item.SetMetadata(ItemMetadataNames.winMDFile, "NOPE");
            item.SetMetadata(ItemMetadataNames.winmdImplmentationFile, "IMPL");
            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        item
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.TargetProcessorArchitecture = "X86";
            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);

            Assert.True(t.ResolvedFiles[0].ItemSpec.Equals(@"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals(@"v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile).Equals("NOPE", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).Equals("IMPL", StringComparison.OrdinalIgnoreCase));

            Assert.True(t.ResolvedDependencyFiles[0].ItemSpec.Equals(@"C:\WinMD\SampleWindowsRuntimeOnly.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals(@"WindowsRuntime 1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(bool.Parse(t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
            Assert.True(t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).Equals("SampleWindowsRuntimeOnly.dll"));
        }

        /// <summary>
        /// Resolve a winmd file which depends on a native implementation dll that has an invalid pe header. 
        /// This will always result in an error since the dll is malformed
        /// </summary>
        [Fact]
        public void ResolveWinmdWithInvalidPENativeDependency()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();
            TaskItem item = new TaskItem(@"DependsOnInvalidPeHeader");
            ITaskItem[] assemblyFiles = new TaskItem[] { item };

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMDArchVerification" };
            bool succeeded = Execute(t);

            // Should fail since PE Header is not valid and this is always an error.
            Assert.False(succeeded);
            Assert.Equal(1, engine.Errors);
            Assert.Equal(0, engine.Warnings);

            // The original winmd will resolve but its impelmentation dll must not be there
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(0, t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).Length);

            string invalidPEMessage = ResourceUtilities.FormatResourceString("ResolveAssemblyReference.ImplementationDllHasInvalidPEHeader");
            string fullMessage = ResourceUtilities.FormatResourceString("ResolveAssemblyReference.ProblemReadingImplementationDll", @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.dll", invalidPEMessage);
            engine.AssertLogContains(fullMessage);
        }

        /// <summary>
        /// Resolve a winmd file which depends a native dll that matches the targeted architecture
        /// </summary>
        [Fact]
        public void ResolveWinmdWithArchitectureDependencyMatchingArchitecturesX86()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();
            TaskItem item = new TaskItem("DependsOnX86");
            ITaskItem[] assemblyFiles = new TaskItem[] { item };

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMDArchVerification" };
            t.TargetProcessorArchitecture = "X86";
            t.WarnOrErrorOnTargetArchitectureMismatch = "Error";

            bool succeeded = Execute(t);
            Assert.Equal(1, t.ResolvedFiles.Length);

            Assert.True(t.ResolvedFiles[0].ItemSpec.Equals(@"C:\WinMDArchVerification\DependsOnX86.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals(@"WindowsRuntime 1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));

            Assert.True(succeeded);
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).Equals("DependsOnX86.dll"));
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
        }

        /// <summary>
        /// Resolve a winmd file which depends a native dll that matches the targeted architecture
        /// </summary>
        [Fact]
        public void ResolveWinmdWithArchitectureDependencyAnyCPUNative()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            // IMAGE_FILE_MACHINE unknown is supposed to work on all machine types
            TaskItem item = new TaskItem("DependsOnAnyCPUUnknown");
            ITaskItem[] assemblyFiles = new TaskItem[] { item };

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMDArchVerification" };
            t.TargetProcessorArchitecture = "X86";
            t.WarnOrErrorOnTargetArchitectureMismatch = "Error";

            bool succeeded = Execute(t);
            Assert.Equal(1, t.ResolvedFiles.Length);

            Assert.True(t.ResolvedFiles[0].ItemSpec.Equals(@"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals(@"WindowsRuntime 1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));

            Assert.True(succeeded);
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).Equals("DependsOnAnyCPUUnknown.dll"));
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
        }

        /// <summary>
        /// Resolve a winmd file which depends on a native implementation dll that has an invalid pe header. 
        /// A warning or error is expected in the log depending on the WarnOrErrorOnTargetArchitecture property value.
        /// </summary>
        [Fact]
        public void ResolveWinmdWithArchitectureDependency()
        {
            VerifyImplementationArchitecture("DependsOnX86", "MSIL", "X86", "Error");
            VerifyImplementationArchitecture("DependsOnX86", "MSIL", "X86", "Warning");
            VerifyImplementationArchitecture("DependsOnX86", "MSIL", "X86", "None");
            VerifyImplementationArchitecture("DependsOnX86", "AMD64", "X86", "Error");
            VerifyImplementationArchitecture("DependsOnX86", "AMD64", "X86", "Warning");
            VerifyImplementationArchitecture("DependsOnX86", "AMD64", "X86", "None");
            VerifyImplementationArchitecture("DependsOnAmd64", "MSIL", "AMD64", "Error");
            VerifyImplementationArchitecture("DependsOnAmd64", "MSIL", "AMD64", "Warning");
            VerifyImplementationArchitecture("DependsOnAmd64", "MSIL", "AMD64", "None");
            VerifyImplementationArchitecture("DependsOnAmd64", "X86", "AMD64", "Error");
            VerifyImplementationArchitecture("DependsOnAmd64", "X86", "AMD64", "Warning");
            VerifyImplementationArchitecture("DependsOnAmd64", "X86", "AMD64", "None");
            VerifyImplementationArchitecture("DependsOnARM", "MSIL", "ARM", "Error");
            VerifyImplementationArchitecture("DependsOnARM", "MSIL", "ARM", "Warning");
            VerifyImplementationArchitecture("DependsOnARM", "MSIL", "ARM", "None");
            VerifyImplementationArchitecture("DependsOnARMV7", "MSIL", "ARM", "Error");
            VerifyImplementationArchitecture("DependsOnARMV7", "MSIL", "ARM", "Warning");
            VerifyImplementationArchitecture("DependsOnARMv7", "MSIL", "ARM", "None");
            VerifyImplementationArchitecture("DependsOnIA64", "MSIL", "IA64", "Error");
            VerifyImplementationArchitecture("DependsOnIA64", "MSIL", "IA64", "Warning");
            VerifyImplementationArchitecture("DependsOnIA64", "MSIL", "IA64", "None");
            VerifyImplementationArchitecture("DependsOnUnknown", "MSIL", "Unknown", "Error");
            VerifyImplementationArchitecture("DependsOnUnknown", "MSIL", "Unknown", "Warning");
            VerifyImplementationArchitecture("DependsOnUnknown", "MSIL", "Unknown", "None");
        }

        private void VerifyImplementationArchitecture(string winmdName, string targetProcessorArchitecture, string implementationFileArch, string warnOrErrorOnTargetArchitectureMismatch)
        {
            // Create the engine.
            MockEngine engine = new MockEngine();
            TaskItem item = new TaskItem(winmdName);
            ITaskItem[] assemblyFiles = new TaskItem[] { item };

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMDArchVerification" };
            t.TargetProcessorArchitecture = targetProcessorArchitecture;
            t.WarnOrErrorOnTargetArchitectureMismatch = warnOrErrorOnTargetArchitectureMismatch;

            bool succeeded = Execute(t);
            Assert.Equal(1, t.ResolvedFiles.Length);

            Assert.True(t.ResolvedFiles[0].ItemSpec.Equals(@"C:\WinMDArchVerification\" + winmdName + ".winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals(@"WindowsRuntime 1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));

            string fullMessage = null;
            if (implementationFileArch.Equals("Unknown"))
            {
                fullMessage = ResourceUtilities.FormatResourceString("ResolveAssemblyReference.UnknownProcessorArchitecture", @"C:\WinMDArchVerification\" + winmdName + ".dll", @"C:\WinMDArchVerification\" + winmdName + ".winmd", NativeMethods.IMAGE_FILE_MACHINE_R4000.ToString("X", CultureInfo.InvariantCulture));
            }
            else
            {
                fullMessage = ResourceUtilities.FormatResourceString("ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArchOfImplementation", targetProcessorArchitecture, implementationFileArch, @"C:\WinMDArchVerification\" + winmdName + ".dll", @"C:\WinMDArchVerification\" + winmdName + ".winmd");
            }

            if (warnOrErrorOnTargetArchitectureMismatch.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                engine.AssertLogDoesntContain(fullMessage);
            }
            else
            {
                engine.AssertLogContains(fullMessage);
            }

            if (warnOrErrorOnTargetArchitectureMismatch.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            {
                // Should fail since PE Header is not valid and this is always an error.
                Assert.True(succeeded);
                Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).Equals(winmdName + ".dll"));
                Assert.Equal(0, engine.Errors);
                Assert.Equal(1, engine.Warnings);
            }
            else if (warnOrErrorOnTargetArchitectureMismatch.Equals("Error", StringComparison.OrdinalIgnoreCase))
            {
                // Should fail since PE Header is not valid and this is always an error.
                Assert.False(succeeded);
                Assert.Equal(0, t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).Length);
                Assert.Equal(1, engine.Errors);
                Assert.Equal(0, engine.Warnings);
            }
            else if (warnOrErrorOnTargetArchitectureMismatch.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(succeeded);
                Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).Equals(winmdName + ".dll"));
                Assert.Equal(0, engine.Errors);
                Assert.Equal(0, engine.Warnings);
            }
        }

        /// <summary>
        /// Verify when a winmd file depends on another winmd file that we resolve both and that the metadata is correct.
        /// </summary>
        [Fact]
        public void DotNetAssemblyDependsOnAWinMDFileWithVersion255()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"DotNetAssemblyDependsOn255WinMD")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);

            Assert.True(t.ResolvedFiles[0].ItemSpec.Equals(@"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals(@"v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile).Length);

            Assert.True(t.ResolvedDependencyFiles[0].ItemSpec.Equals(@"C:\WinMD\WinMDWithVersion255.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).Equals(@"WindowsRuntime 1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(bool.Parse(t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
        }
        #endregion
    }


    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task.
    /// </summary>
    sealed public class Miscellaneous : ResolveAssemblyReferenceTestFixture
    {
        private static List<string> s_assemblyFolderExTestVersions = new List<string>
        {
            "v1.0",
            "v2.0.50727",
            "v3.0",
            "v3.5",
            "v4.0",
            "v4.0.2116",
            "v4.1",
            "v4.0.255",
            "v4.0.255.87",
            "v4.0.9999",
            "v4.0.0000",
            "v4.0001.0",
            "v4.0.2116.87",
            "v3.0SP1",
            "v3.0 BAZ",
            "v5.0",
            "v1",
            "v5",
            "v3.5.0.x86chk",
            "v3.5.1.x86chk",
            "v3.5.256.x86chk",
            "v",
            "1",
            "1.0",
            "1.0.0",
            "V3.5.0.0.0",
            "V3..",
            "V-1",
            "V9999999999999999",
            "Dan_rocks_bigtime",
            "v00001.0"
        };

        private string _fullRedistListContents =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
            "</FileList >";

        /// <summary>
        /// The contents of a subsetFile which only contain the Microsoft.Build.Engine assembly in the white list
        /// </summary>
        private string _engineOnlySubset =
                   "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                       "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                   "</FileList >";

        /// <summary>
        /// The contents of a subsetFile which only contain the System.Xml assembly in the white list
        /// </summary>
        private string _xmlOnlySubset =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                   "</FileList >";

        /// <summary>
        /// The contents of a subsetFile which contain both the Microsoft.Build.Engine and System.Xml assemblies in the white list
        /// </summary>
        private string _engineAndXmlSubset =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                   "</FileList >";



        /// <summary>
        /// Let us have the following dependency structure
        /// 
        /// X which is in the gac, depends on Z which is not in the GAC
        /// 
        /// Let copyLocalDependenciesWhenParentReferenceInGac be set to false
        /// 
        /// Since copyLocalDependenciesWhenParentReferenceInGac is set to false and the parent of Z is in the GAC
        /// </summary>
        [Fact]
        public void CopyLocalDependenciesWhenParentReferenceInGacFalseAllParentsInGac()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = new string[] { "{gac}", @"c:\MyComponents" };
            t.CopyLocalDependenciesWhenParentReferenceInGac = false;
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            AssertNoCase("false", t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal"));
            AssertNoCase("false", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
        }



        [Fact]
        public void ValidateFrameworkNameError()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = new string[] { @"c:\MyComponents" };
            t.TargetFrameworkMoniker = "I am a random frameworkName";
            bool succeeded = Execute(t);

            Assert.False(succeeded);
            Assert.Equal(1, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            string message = ResourceUtilities.FormatResourceString("ResolveAssemblyReference.InvalidParameter", "TargetFrameworkMoniker", t.TargetFrameworkMoniker, String.Empty);
            engine.AssertLogContains(message);
        }

        /// <summary>
        /// Let us have the following dependency structure
        /// 
        /// X which is in the gac, depends on Z which is not in the GAC
        /// Y which is not in the gac, depends on Z which is not in the GAC
        /// 
        /// Let copyLocalDependenciesWhenParentReferenceInGac be set to false
        /// 
        /// Since copyLocalDependenciesWhenParentReferenceInGac is set to false but one of the parents of Z is not in the GAC and Z is not in the gac we should be copy local
        /// </summary>
        [Fact]
        public void CopyLocalDependenciesWhenParentReferenceInGacFalseSomeParentsInGac()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"),
                        new TaskItem("Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = new string[] { "{gac}", @"c:\MyComponents" };
            t.CopyLocalDependenciesWhenParentReferenceInGac = false;
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            AssertNoCase("false", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
            AssertNoCase("true", t.ResolvedFiles[1].GetMetadata("CopyLocal"));
            AssertNoCase("true", t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal"));
        }

        /// <summary>
        /// Make sure that when we parse the runtime version that if there is a bad one we default to 2.0.
        /// </summary>
        [Fact]
        public void TestSetRuntimeVersion()
        {
            Version parsedVersion = ResolveAssemblyReference.SetTargetedRuntimeVersion("4.0.21006");
            Assert.True(parsedVersion.Equals(new Version("4.0.21006")));

            parsedVersion = ResolveAssemblyReference.SetTargetedRuntimeVersion("BadVersion");
            Assert.True(parsedVersion.Equals(new Version("2.0.50727")));
        }

        /// <summary>
        /// Let us have the following dependency structure
        /// 
        /// X which is in the gac, depends on Z which is not in the GAC
        /// 
        /// Let copyLocalDependenciesWhenParentReferenceInGac be set to true
        /// 
        /// Since copyLocalDependenciesWhenParentReferenceInGac is set to true and Z is not in the GAC it will be copy local true
        /// </summary>
        [Fact]
        public void CopyLocalDependenciesWhenParentReferenceInGacTrueAllParentsInGac()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = new string[] { "{gac}", @"c:\MyComponents" };
            t.CopyLocalDependenciesWhenParentReferenceInGac = true;
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            AssertNoCase("true", t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal"));
            AssertNoCase("false", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
        }

        /// <summary>
        /// Let us have the following dependency structure
        /// 
        /// X which is in the gac, depends on Z which is not in the GAC
        /// Y which is not in the gac, depends on Z which is not in the GAC
        /// 
        /// Let copyLocalDependenciesWhenParentReferenceInGac be set to true
        /// 
        /// Since copyLocalDependenciesWhenParentReferenceInGac is set to true and Z is not in the GAC it will be copy local true
        /// </summary>
        [Fact]
        public void CopyLocalDependenciesWhenParentReferenceInGacTrueSomeParentsInGac()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"),
                        new TaskItem("Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = new string[] { "{gac}", @"c:\MyComponents" };
            t.CopyLocalDependenciesWhenParentReferenceInGac = true;
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            AssertNoCase("false", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
            AssertNoCase("true", t.ResolvedFiles[1].GetMetadata("CopyLocal"));
            AssertNoCase("true", t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal"));
        }

        [Fact]
        public void CopyLocalDependenciesWhenParentReferenceNotInGac()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        // V not in GAC, depends on W (in GAC)
                        // V - CopyLocal should be true (resolved locally)
                        // W - CopyLocal should be false (resolved {gac})
                        new TaskItem("V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"),
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = new string[] { "{gac}", @"c:\MyComponents" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.CopyLocalFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            AssertNoCase("true", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
            AssertNoCase("false", t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal"));
        }

        /// <summary>
        /// Test the legacy behavior for copy local (set to false when an assembly exists in the gac no matter
        /// where it was actually resolved). Sets DoNotCopyLocalIfInGac = true
        /// </summary>
        [Fact]
        public void CopyLocalLegacyBehavior()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        // V not in GAC, depends on W (in GAC)
                        // V - CopyLocal should be true (resolved locally)
                        // W - CopyLocal should be false (resolved from "c:\MyComponents" BUT exists in GAC, so false)
                        // (changed the order of the search paths to emulate this)
                        new TaskItem("V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"),
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.DoNotCopyLocalIfInGac = true;
            t.SearchPaths = new string[] { @"c:\MyComponents", "{gac}", };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.CopyLocalFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            AssertNoCase("true", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
            AssertNoCase("false", t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal"));
        }

        /// <summary>
        /// Very basic test.
        /// </summary>
        [Fact]
        public void Basic()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Miscellaneous.Basic() test");

            // Create the engine.
            MockEngine engine = new MockEngine();

            // Construct a list of assembly files.
            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(@"c:\MyProject\MyMissingAssembly.dll")
            };

            // Also construct a set of assembly names to pass in.
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new TaskItem("MyPrivateAssembly"),
                new TaskItem("MyGacAssembly"),
                new TaskItem("MyCopyLocalAssembly"),
                new TaskItem("MyDontCopyLocalAssembly"),
                new TaskItem("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

            assemblyNames[0].SetMetadata("RandomAttributeThatShouldBeForwarded", "1776");
            // Metadata which should NOT be forwarded
            assemblyNames[0].SetMetadata(ItemMetadataNames.imageRuntime, "FOO");
            assemblyNames[0].SetMetadata(ItemMetadataNames.winMDFile, "NOPE");
            assemblyNames[0].SetMetadata(ItemMetadataNames.winmdImplmentationFile, "IMPL");


            assemblyNames[1].SetMetadata("Private", "true");
            assemblyNames[2].SetMetadata("Private", "false");
            assemblyNames[4].SetMetadata("Private", "false");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            t.Assemblies = assemblyNames;
            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };
            t.SearchPaths = DefaultPaths;
            Execute(t);

            // Now, loop over the closure of dependencies and make sure we have what we need.
            bool enSatellitePdbFound = false;
            bool systemXmlFound = false;
            bool systemDataFound = false;
            bool systemFound = false;
            bool mscorlibFound = false;
            bool myGacAssemblyFound = false;
            bool myPrivateAssemblyFound = false;
            bool myCopyLocalAssemblyFound = false;
            bool myDontCopyLocalAssemblyFound = false;
            bool engbSatellitePdbFound = false;
            bool missingAssemblyFound = false;

            // Process the primary items.
            foreach (ITaskItem item in t.ResolvedFiles)
            {
                if (String.Compare(item.ItemSpec, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.XML.dll", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    systemXmlFound = true;
                    AssertNoCase("", item.GetMetadata("DestinationSubDirectory"));
                    AssertNoCase("1776", item.GetMetadata("RandomAttributeThatShouldBeForwarded"));
                    AssertNoCase("false", item.GetMetadata("CopyLocal"));
                    AssertNoCase("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", item.GetMetadata("FusionName"));
                    AssertNoCase("v2.0.50727", item.GetMetadata(ItemMetadataNames.imageRuntime));
                    AssertNoCase("NOPE", item.GetMetadata(ItemMetadataNames.winMDFile));
                    AssertNoCase("IMPL", item.GetMetadata(ItemMetadataNames.winmdImplmentationFile));
                }
                else if (item.ItemSpec.EndsWith(@"v2.0.MyVersion\System.Data.dll"))
                {
                    systemDataFound = true;
                    AssertNoCase("", item.GetMetadata("DestinationSubDirectory"));
                    AssertNoCase("", item.GetMetadata("RandomAttributeThatShouldBeForwarded"));
                    AssertNoCase("false", item.GetMetadata("CopyLocal"));
                    AssertNoCase("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", item.GetMetadata("FusionName"));
                }
                else if (item.ItemSpec.EndsWith(@"v2.0.MyVersion\MyGacAssembly.dll"))
                {
                    myGacAssemblyFound = true;
                    AssertNoCase("", item.GetMetadata("DestinationSubDirectory"));
                    AssertNoCase("", item.GetMetadata("RandomAttributeThatShouldBeForwarded"));
                    AssertNoCase("false", item.GetMetadata("CopyLocal"));
                }
                else if (item.ItemSpec.EndsWith(@"MyProject\MyPrivateAssembly.exe"))
                {
                    myPrivateAssemblyFound = true;
                    AssertNoCase("", item.GetMetadata("DestinationSubDirectory"));
                    AssertNoCase("", item.GetMetadata("RandomAttributeThatShouldBeForwarded"));
                    AssertNoCase("true", item.GetMetadata("CopyLocal"));
                }
                else if (item.ItemSpec.EndsWith(@"MyProject\MyCopyLocalAssembly.dll"))
                {
                    myCopyLocalAssemblyFound = true;
                    AssertNoCase("", item.GetMetadata("DestinationSubDirectory"));
                    AssertNoCase("", item.GetMetadata("RandomAttributeThatShouldBeForwarded"));
                    AssertNoCase("true", item.GetMetadata("CopyLocal"));
                }
                else if (item.ItemSpec.EndsWith(@"MyProject\MyDontCopyLocalAssembly.dll"))
                {
                    myDontCopyLocalAssemblyFound = true;
                    AssertNoCase("", item.GetMetadata("DestinationSubDirectory"));
                    AssertNoCase("", item.GetMetadata("RandomAttributeThatShouldBeForwarded"));
                    AssertNoCase("false", item.GetMetadata("CopyLocal"));
                }
                else if (item.ItemSpec.EndsWith(@"MyProject\MyMissingAssembly.dll"))
                {
                    missingAssemblyFound = true;
                    AssertNoCase("", item.GetMetadata("DestinationSubDirectory"));
                    AssertNoCase("", item.GetMetadata("RandomAttributeThatShouldBeForwarded"));

                    // Its debatable whether this file should be CopyLocal or not.
                    // It doesn't exist on disk, but is it ResolveAssemblyReference's job to make sure that it does?
                    // For now, let the default CopyLocal rules apply.
                    AssertNoCase("true", item.GetMetadata("CopyLocal"));
                    AssertNoCase("MyMissingAssembly", item.GetMetadata("FusionName"));
                }
                else if (String.Compare(item.ItemSpec, @"c:\MyProject\System.Xml.dll", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // The version of System.Xml.dll in C:\MyProject is an older version.
                    // This version is not a match. When want the current version which should have been in a different directory.
                    Assert.True(false, "Wrong version of System.Xml.dll matched--version was wrong");
                }
                else if (String.Compare(item.ItemSpec, @"c:\MyProject\System.Data.dll", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // The version of System.Data.dll in C:\MyProject has an incorrect PKT
                    // This version is not a match. 
                    Assert.True(false, "Wrong version of System.Data.dll matched--public key token was wrong");
                }
                else
                {
                    Console.WriteLine(item.ItemSpec);
                    Assert.True(false, String.Format("A new resolved file called '{0}' was found. If this is intentional, then add unittests above.", item.ItemSpec));
                }
            }

            // Process the dependencies.
            foreach (ITaskItem item in t.ResolvedDependencyFiles)
            {
                if (item.ItemSpec.EndsWith(@"v2.0.MyVersion\SysTem.dll"))
                {
                    systemFound = true;
                    AssertNoCase("", item.GetMetadata("DestinationSubDirectory"));
                    AssertNoCase("", item.GetMetadata("RandomAttributeThatShouldBeForwarded"));
                    AssertNoCase("false", item.GetMetadata("CopyLocal"));
                    AssertNoCase("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", item.GetMetadata("FusionName"));
                }
                else if (item.ItemSpec.EndsWith(@"v2.0.MyVersion\mscorlib.dll"))
                {
                    mscorlibFound = true;
                    AssertNoCase("", item.GetMetadata("DestinationSubDirectory"));
                    AssertNoCase("1776", item.GetMetadata("RandomAttributeThatShouldBeForwarded"));
                    AssertNoCase("false", item.GetMetadata("CopyLocal"));
                    AssertNoCase("v2.0.50727", item.GetMetadata(ItemMetadataNames.imageRuntime));
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.winMDFile).Length);
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.winmdImplmentationFile).Length);

                    // Notice how the following doesn't have 'version'. This is because all versions of mscorlib 'unify'
                    Assert.Equal(AssemblyRef.Mscorlib, item.GetMetadata("FusionName"));
                }
                else
                {
                    Console.WriteLine(item.ItemSpec);
                    Assert.True(false, String.Format("A new dependency called '{0}' was found. If this is intentional, then add unittests above.", item.ItemSpec));
                }
            }

            // Process the related files.
            foreach (ITaskItem item in t.RelatedFiles)
            {
                Console.WriteLine(item.ItemSpec);
                Assert.True(false, String.Format("A new dependency called '{0}' was found. If this is intentional, then add unittests above.", item.ItemSpec));
            }

            // Process the satellites.
            foreach (ITaskItem item in t.SatelliteFiles)
            {
                if (String.Compare(item.ItemSpec, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en\System.XML.resources.pdb", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    enSatellitePdbFound = true;
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.imageRuntime).Length);
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.winMDFile).Length);
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.winmdImplmentationFile).Length);
                }
                else if (String.Compare(item.ItemSpec, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en-GB\System.XML.resources.pdb", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    engbSatellitePdbFound = true;
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.imageRuntime).Length);
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.winMDFile).Length);
                    Assert.Equal(0, item.GetMetadata(ItemMetadataNames.winmdImplmentationFile).Length);
                }
                else
                {
                    Console.WriteLine(item.ItemSpec);
                    Assert.True(false, String.Format("A new dependency called '{0}' was found. If this is intentional, then add unittests above.", item.ItemSpec));
                }
            }

            Assert.False(enSatellitePdbFound); // "Expected to not find satellite pdb."
            Assert.True(systemXmlFound); // "Expected to find returned item."
            Assert.True(systemDataFound); // "Expected to find returned item."
            Assert.True(systemFound); // "Expected to find returned item."
            Assert.False(mscorlibFound); // "Expected to not find returned item."
            Assert.True(myGacAssemblyFound); // "Expected to find returned item."
            Assert.True(myPrivateAssemblyFound); // "Expected to find returned item."
            Assert.True(myCopyLocalAssemblyFound); // "Expected to find returned item."
            Assert.True(myDontCopyLocalAssemblyFound); // "Expected to find returned item."
            Assert.False(engbSatellitePdbFound); // "Expected to not find satellite pdb."
            Assert.True(missingAssemblyFound); // "Expected to find returned item."
        }

        /// <summary>
        /// Auxiliary enumeration for EmbedInteropTypes test.
        /// Defines indices for accessing test's data structures.
        /// </summary>
        private enum EmbedInteropTypes_Indices
        {
            MyMissingAssembly = 0,
            MyCopyLocalAssembly = 1,
            MyDontCopyLocalAssembly = 2,

            EndMarker
        };

        /// <summary>
        /// Make sure the imageruntime is correctly returned.
        /// </summary>
        [Fact]
        public void TestGetImageRuntimeVersion()
        {
            string imageRuntimeReportedByAsssembly = this.GetType().Assembly.ImageRuntimeVersion;
            string pathForAssembly = this.GetType().Assembly.Location;

            string inspectedRuntimeVersion = AssemblyInformation.GetRuntimeVersion(pathForAssembly);
            Assert.True(imageRuntimeReportedByAsssembly.Equals(inspectedRuntimeVersion, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Make sure the imageruntime is correctly returned.
        /// </summary>
        [Fact]
        public void TestGetImageRuntimeVersionBadPath()
        {
            string realFile = FileUtilities.GetTemporaryFile();
            try
            {
                string inspectedRuntimeVersion = AssemblyInformation.GetRuntimeVersion(realFile);
                Assert.Equal(inspectedRuntimeVersion, String.Empty);
            }
            finally
            {
                File.Delete(realFile);
            }
        }

        /// <summary>
        /// When specifying "EmbedInteropTypes" on a project targeting Fx higher thatn v4.0 -
        /// CopyLocal should be overriden to false
        /// </summary>
        [Fact]
        public void EmbedInteropTypes()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Miscellaneous.Basic() test");

            // Construct a list of assembly files.
            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(@"c:\MyProject\MyMissingAssembly.dll")
            };

            assemblyFiles[0].SetMetadata("Private", "true");
            assemblyFiles[0].SetMetadata("EmbedInteropTypes", "true");

            // Construct a list of assembly names.
            ITaskItem[] assemblies = new TaskItem[]
            {
                new TaskItem("MyCopyLocalAssembly"),
                new TaskItem("MyDontCopyLocalAssembly")
            };

            assemblies[0].SetMetadata("Private", "true");
            assemblies[0].SetMetadata("EmbedInteropTypes", "true");
            assemblies[1].SetMetadata("Private", "false");
            assemblies[1].SetMetadata("EmbedInteropTypes", "true");

            // the matrix of TargetFrameworkVersion values we are testing
            string[] fxVersions =
            {
                "v2.0",
                "v3.0",
                "v3.5",
                "v4.0"
            };

            // expected ItemSpecs for corresponding assemblies
            string[] expectedItemSpec =
            {
                @"MyProject\MyMissingAssembly.dll",         // MyMissingAssembly
                @"MyProject\MyCopyLocalAssembly.dll",       // MyCopyLocalAssembly
                @"MyProject\MyDontCopyLocalAssembly.dll",   // MyDontCopyLocalAssembly
            };

            // matrix of expected CopyLocal value per assembly per framwork
            string[,] expectedCopyLocal =
            {
                // v2.0     v3.0     v3.5      v4.0
                { "true",  "true",  "true",  "false" },    // MyMissingAssembly
                { "true",  "true",  "true",  "false" },    // MyCopyLocalAssembly
                { "false", "false", "false", "false" }     // MyDontCopyLocalAssembly
            };


            int assembliesCount = (int)EmbedInteropTypes_Indices.EndMarker;

            // now let's verify our data structures are all set up correctly
            Assert.Equal(fxVersions.GetLength(0), expectedCopyLocal.GetLength(1)); // "fxVersions: test setup is incorrect"
            Assert.Equal(expectedItemSpec.Length, assembliesCount); // "expectedItemSpec: test setup is incorrect"
            Assert.Equal(expectedCopyLocal.GetLength(0), assembliesCount); // "expectedCopyLocal: test setup is incorrect"

            for (int i = 0; i < fxVersions.Length; i++)
            {
                // Create the engine.
                MockEngine engine = new MockEngine();
                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = engine;
                t.Assemblies = assemblies;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;

                string fxVersion = fxVersions[i];
                t.TargetFrameworkDirectories = new string[] { String.Format(@"c:\WINNT\Microsoft.NET\Framework\{0}.MyVersion", fxVersion) };
                t.TargetFrameworkVersion = fxVersion;
                Execute(t);

                bool[] assembliesFound = new bool[assembliesCount];

                // Now, process primary items and make sure we have what we need.
                foreach (ITaskItem item in t.ResolvedFiles)
                {
                    string copyLocal = item.GetMetadata("CopyLocal");

                    int j;
                    for (j = 0; j < assembliesCount; j++)
                    {
                        if (item.ItemSpec.EndsWith(expectedItemSpec[j]))
                        {
                            assembliesFound[j] = true;
                            string assemblyName = Enum.GetName(typeof(EmbedInteropTypes_Indices), j);
                            AssertNoCase(fxVersion + ": unexpected CopyValue for " + assemblyName, expectedCopyLocal[j, i], copyLocal);
                            break;
                        }
                    }

                    if (j == assembliesCount)
                    {
                        Console.WriteLine(item.ItemSpec);
                        Assert.True(false, String.Format("{0}: A new resolved file called '{1}' was found. If this is intentional, then add unittests above.", fxVersion, item.ItemSpec));
                    }
                }

                for (int j = 0; j < assembliesCount; j++)
                {
                    string assemblyName = Enum.GetName(typeof(EmbedInteropTypes_Indices), j);
                    Assert.True(assembliesFound[j], fxVersion + ": Expected to find returned item " + assemblyName);
                }
            }
        }

        /// <summary>
        /// If items lists are empty, then this is a NOP not a failure.
        /// </summary>
        [Fact]
        public void NOPForEmptyItemLists()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };
            t.SearchPaths = DefaultPaths;

            bool succeeded = Execute(t);

            Assert.True(succeeded); // "Expected success."
        }


        /// <summary>
        /// If no related file extensions are input to RAR, .pdb and .xml should be used
        /// by default.
        /// </summary>
        [Fact]
        public void DefaultAllowedRelatedFileExtensionsAreUsed()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Miscellaneous.DefaultRelatedFileExtensionsAreUsed() test");

            // Create the engine.
            MockEngine engine = new MockEngine();

            // Construct a list of assembly files.
            ITaskItem[] assemblies = new TaskItem[]
            {
                new TaskItem(@"c:\AssemblyFolder\SomeAssembly.dll")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblies;
            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };
            t.SearchPaths = DefaultPaths;
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(t.ResolvedFiles[0].ItemSpec.EndsWith(@"AssemblyFolder\SomeAssembly.dll"));

            // Process the related files.
            Assert.Equal(3, t.RelatedFiles.Length);

            bool pdbFound = false;
            bool xmlFound = false;
            bool priFound = false;

            foreach (ITaskItem item in t.RelatedFiles)
            {
                if (item.ItemSpec.EndsWith(@"AssemblyFolder\SomeAssembly.pdb"))
                {
                    pdbFound = true;
                }
                if (item.ItemSpec.EndsWith(@"AssemblyFolder\SomeAssembly.xml"))
                {
                    xmlFound = true;
                }
                if (item.ItemSpec.EndsWith(@"AssemblyFolder\SomeAssembly.pri"))
                {
                    priFound = true;
                }
            }

            Assert.True(pdbFound && xmlFound && priFound); // "Expected to find .pdb, .xml, and .pri related files."
        }

        /// <summary>
        /// RAR should use any given related file extensions.
        /// </summary>
        [Fact]
        public void InputAllowedRelatedFileExtensionsAreUsed()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Miscellaneous.InputRelatedFileExtensionsAreUsed() test");

            // Create the engine.
            MockEngine engine = new MockEngine();

            // Construct a list of assembly files.
            ITaskItem[] assemblies = new TaskItem[]
            {
                new TaskItem(@"c:\AssemblyFolder\SomeAssembly.dll")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblies;
            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };
            t.SearchPaths = DefaultPaths;
            t.AllowedRelatedFileExtensions = new string[] { @".licenses", ".xml" }; //no .pdb or .config
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(t.ResolvedFiles[0].ItemSpec.EndsWith(@"AssemblyFolder\SomeAssembly.dll"));

            // Process the related files.
            Assert.Equal(2, t.RelatedFiles.Length);

            bool licensesFound = false;
            bool xmlFound = false;
            foreach (ITaskItem item in t.RelatedFiles)
            {
                if (item.ItemSpec.EndsWith(@"AssemblyFolder\SomeAssembly.licenses"))
                {
                    licensesFound = true;
                }
                if (item.ItemSpec.EndsWith(@"AssemblyFolder\SomeAssembly.xml"))
                {
                    xmlFound = true;
                }
            }

            Assert.True(licensesFound && xmlFound); // "Expected to find .licenses and .xml related files."
        }

        /// <summary>
        /// Simulate a CreateProject resolution. This is primarily for IO monitoring.
        /// </summary>
        public void SimulateCreateProjectAgainstWhidbeyInternal(string fxfolder)
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing SimulateCreateProjectAgainstWhidbey() test");

            // Create the engine.
            MockEngine engine = new MockEngine();

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = new ITaskItem[] {
                new TaskItem("System"),
                new TaskItem("System.Deployment"),
                new TaskItem("System.Drawing"),
                new TaskItem("System.Windows.Forms"),
            };
            t.TargetFrameworkDirectories = new string[] { fxfolder };

            t.SearchPaths = new string[]
            {
                "{CandidateAssemblyFiles}",
                // Reference path
                "{HintPathFromItem}",
                @"{TargetFrameworkDirectory}",
                @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
                "{AssemblyFolders}",
                "{GAC}",
                "{RawFileName}"
            };

            bool succeeded = Execute(t);

            Assert.True(succeeded); // "Expected success."
        }

        /// <summary>
        /// Test with a standard path.
        /// </summary>
        [Fact]
        public void SimulateCreateProjectAgainstWhidbey()
        {
            SimulateCreateProjectAgainstWhidbeyInternal(ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version45));
        }

        /// <summary>
        /// Test with a standard trailing-slash path.
        /// </summary>
        [Fact]
        public void SimulateCreateProjectAgainstWhidbeyWithTrailingSlash()
        {
            SimulateCreateProjectAgainstWhidbeyInternal(ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version45) + @"\");
        }


        /// <summary>
        /// Invalid candidate assembly files should not crash
        /// </summary>
        [Fact]
        public void Regress286699_InvalidCandidateAssemblyFiles()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("mscorlib") };
            t.CandidateAssemblyFiles = new string[] { "|" };

            bool retval = Execute(t);

            Assert.False(retval);

            // Should not crash.
        }

        /// <summary>
        /// Invalid assembly files should not crash
        /// </summary>
        [Fact]
        public void Regress286699_InvalidAssemblyFiles()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("mscorlib") };
            t.AssemblyFiles = new ITaskItem[] { new TaskItem("|") };

            bool retval = Execute(t);

            Assert.False(retval);

            // Should not crash.
        }

        /// <summary>
        /// Invalid assemblies param should not crash
        /// </summary>
        [Fact]
        public void Regress286699_InvalidAssembliesParameter()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("|!@#$%::") };

            bool retval = Execute(t);

            // I think this should return true
            Assert.True(retval);

            // Should not crash.
        }

        /// <summary>
        /// Target framework path with a newline should not crash.
        /// </summary>
        [Fact]
        public void Regress286699_InvalidTargetFrameworkDirectory()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Regress286699_InvalidTargetFrameworkDirectory() test");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.TargetFrameworkDirectories = new string[] { "\nc:\\blah\\v2.0.1234" };

            bool retval = Execute(t);

            Assert.False(retval);

            // Should not crash.
        }

        /// <summary>
        /// Invalid search path should not crash.
        /// </summary>
        [Fact]
        public void Regress286699_InvalidSearchPath()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("mscorlib") };
            t.SearchPaths = new string[] { "|" };

            bool retval = Execute(t);

            Assert.False(retval);

            // Should not crash.
        }

        /// <summary>
        /// Invalid app.config path should not crash.
        /// </summary>
        [Fact]
        public void Regress286699_InvalidAppConfig()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("mscorlib") };
            t.AppConfigFile = "|";

            bool retval = Execute(t);

            Assert.False(retval);

            // Should not crash.
        }

        /// <summary>
        /// Make sure that nonexistent references are just eliminated. 
        /// </summary>
        [Fact]
        public void NonExistentReference()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();
            t.Assemblies = new ITaskItem[] {
                new TaskItem("System.Xml"), new TaskItem("System.Nonexistent")
            };
            t.SearchPaths = new string[] { Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName), "{AssemblyFolders}", "{HintPathFromItem}", "{RawFileName}" };
            t.Execute();
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(0, String.Compare(ToolLocationHelper.GetPathToDotNetFrameworkFile("System.Xml.dll", TargetDotNetFrameworkVersion.Version45), t.ResolvedFiles[0].ItemSpec, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// Consider this situation.
        ///
        ///    Assembly A
        ///     References: B (a simple name)
        ///
        ///    Assembly B
        ///     Assembly Name: B, PKT=aaa, Version=bbb, Culture=ccc
        ///
        /// A does _not_ want to load B because it simple name B does not match the 
        /// B's assembly name.
        ///
        /// Because of this, we want to be sure that if A asks for B (as a simple name)
        /// that we don't find a strongly named assembly.
        /// </summary>
        [Fact]
        public void StrongWeakMismatchInDependency()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();
            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("DependsOnSimpleA")
            };

            t.SearchPaths = new string[] { @"c:\MyApp", @"c:\MyStronglyNamed", @"c:\MyWeaklyNamed" };
            Execute(t);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.Equal(@"c:\MyWeaklyNamed\A.dll", t.ResolvedDependencyFiles[0].ItemSpec);
        }

        /// <summary>
        /// If an Item has a HintPath and there is a {HintPathFromItem} in the SearchPaths
        /// property, then the task should be able to resolve an assembly there.
        /// </summary>
        [Fact]
        public void UseSuppliedHintPath()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing UseSuppliedHintPath() test");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            ITaskItem i = new TaskItem("My.Assembly");

            i.SetMetadata("HintPath", @"C:\myassemblies\My.Assembly.dll");
            i.SetMetadata("Baggage", @"Carry-On");
            t.Assemblies = new ITaskItem[] { i };
            t.SearchPaths = DefaultPaths;
            Execute(t);
            Assert.Equal(@"C:\myassemblies\My.Assembly.dll", t.ResolvedFiles[0].ItemSpec);
            Assert.Equal(1, t.ResolvedFiles.Length);

            // All attributes, including HintPath, should be forwarded from input to output
            Assert.Equal(@"C:\myassemblies\My.Assembly.dll", t.ResolvedFiles[0].GetMetadata("HintPath"));
            Assert.Equal(@"Carry-On", t.ResolvedFiles[0].GetMetadata("Baggage"));
        }

        /// <summary>
        /// Regress this devices bug.
        /// If a simple name is provided then we need to accept the first simple file name match.
        /// Devices frameworks files are signed with a different PK so there should be no unification
        /// with normal fx files.
        /// </summary>
        [Fact]
        public void Regress200872()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("mscorlib") };
            t.SearchPaths = new string[]
            {
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion"
            };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Do the most basic AssemblyFoldersEx resolve.
        /// </summary>
        [Fact]
        public void AssemblyFoldersExBasic()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyGrid") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\MyComponents\MyGrid.dll", t.ResolvedFiles[0].ItemSpec);
            AssertNoCase(@"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// Verify that higher alphabetical values for a component are chosen over lower alphabetic values of a component.
        /// </summary>
        [Fact]
        public void AssemblyFoldersExVerifyComponentFolderSorting()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("CustomComponent") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\MyComponentsB\CustomComponent.dll", t.ResolvedFiles[0].ItemSpec);
            AssertNoCase(@"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// If the target framework version provided by the targets file doesn't begin
        /// with the letter "v", we should tolerate it and treat it as if it does.
        /// </summary>
        [Fact]
        public void AssemblyFoldersExTargetFrameworkVersionDoesNotBeginWithV()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyGrid") };
            t.SearchPaths = new string[] { @"{Registry:Software\Microsoft\.NetFramework,2.0,AssemblyFoldersEx}" };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\MyComponents\MyGrid.dll", t.ResolvedFiles[0].ItemSpec);
            AssertNoCase(@"{Registry:Software\Microsoft\.NetFramework,2.0,AssemblyFoldersEx}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Target AMD64 and try to get an assembly out of the X86 directory.
        /// Expect it not to resolve and get a message on the console
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchDoesNotMatch()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "AMD64";
            Execute(t);

            Assert.Equal(0, t.ResolvedFiles.Length);
            string message = ResourceUtilities.FormatResourceString("ResolveAssemblyReference.TargetedProcessorArchitectureDoesNotMatch", @"C:\Regress714052\X86\A.dll", "X86", "AMD64");
            mockEngine.AssertLogContains(message);
        }

        /// <summary>
        /// Regress DevDiv Bugs 714052.
        /// 
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Target MSIL and get an assembly out of the X86 directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchMSILX86()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "MSIL";
            t.WarnOrErrorOnTargetArchitectureMismatch = "None";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(0, mockEngine.Warnings);
            Assert.Equal(0, mockEngine.Errors);
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,X86}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// Verify if there is a mismatch between what the project targets and the architecture of the resolved primary reference log a warning.
        /// </summary>
        [Fact]
        public void VerifyProcessArchitectureMismatchWarning()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A"), new TaskItem("B") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "MSIL";
            t.WarnOrErrorOnTargetArchitectureMismatch = "Warning";
            Execute(t);

            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(2, mockEngine.Warnings);
            Assert.Equal(0, mockEngine.Errors);
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"A", "X86");
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"B", "X86");
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,X86}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// Verify if there is a mismatch between what the project targets and the architecture of the resolved primary reference log a warning.
        /// </summary>
        [Fact]
        public void VerifyProcessArchitectureMismatchWarningDefault()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A"), new TaskItem("B") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "MSIL";
            Execute(t);

            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(2, mockEngine.Warnings);
            Assert.Equal(0, mockEngine.Errors);
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"A", "X86");
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"B", "X86");
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,X86}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// Verify if there is a mismatch between what the project targets and the architecture of the resolved primary reference log a error.
        /// </summary>
        [Fact]
        public void VerifyProcessArchitectureMismatchError()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A"), new TaskItem("B") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "MSIL";
            t.WarnOrErrorOnTargetArchitectureMismatch = "Error";
            Execute(t);

            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(0, mockEngine.Warnings);
            Assert.Equal(2, mockEngine.Errors);
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"A", "X86");
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"B", "X86");
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,X86}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Target None and get an assembly out of the X86 directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchNoneX86()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "NONE";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,X86}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// If we are targeting NONE and there are two assemblies with the same name then we want to pick the first one rather than look for an assembly which 
        /// has a MSIL architecture or a NONE architecture. NONE means you do not care what architecure is picked.
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchNoneMix()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,MIX}" };
            t.TargetProcessorArchitecture = "NONE";
            t.WarnOrErrorOnTargetArchitectureMismatch = "Error";  // should not do anything
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(0, mockEngine.Warnings);
            Assert.Equal(0, mockEngine.Errors);
            Assert.True(t.ResolvedFiles[0].ItemSpec.Equals(@"C:\Regress714052\Mix\a.winmd", StringComparison.OrdinalIgnoreCase));
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,Mix}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Assume the folders are searched in the order  A and B.  A contains an x86 assembly and B contains an MSIL assembly. 
        /// When targeting MSIL we want to return the MSIL assembly even if we find one in a previous folder first.
        /// Target MSIL and get an assembly out of the MSIL directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchMSILLastFolder()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEx}" };
            t.TargetProcessorArchitecture = "MSIL";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(t.ResolvedFiles[0].ItemSpec, @"C:\Regress714052\MSIL\A.dll");
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEX}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Assume the folders are searched in the order  A and B.  A contains an x86 assembly and B contains an MSIL assembly. 
        /// When targeting None we want to return the MSIL assembly even if we find one in a previous folder first.
        /// Target None and get an assembly out of the MSIL directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchNoneLastFolder()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEx}" };
            t.TargetProcessorArchitecture = "None";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(t.ResolvedFiles[0].ItemSpec, @"C:\Regress714052\MSIL\A.dll");
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEX}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }
        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Assume the folders are searched in the order  A and B.  A contains an x86 assembly and B contains an MSIL assembly. 
        /// When targeting X86 we want to return the MSIL assembly even if we find one in a previous folder first.
        /// Target MSIL and get an assembly out of the MSIL directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchX86FirstFolder()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEx}" };
            t.TargetProcessorArchitecture = "X86";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(t.ResolvedFiles[0].ItemSpec, @"C:\Regress714052\X86\A.dll");
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEX}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Target X86 and get an assembly out of the MSIL directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchX86MSIL()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,MSIL}" };
            t.TargetProcessorArchitecture = "X86";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,MSIL}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Target X86 and get an assembly out of the None directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchX86None()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,None}" };
            t.TargetProcessorArchitecture = "X86";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,None}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Target None and get an assembly out of the None directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchNoneNone()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,None}" };
            t.TargetProcessorArchitecture = "None";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,None}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }
        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Target MSIL and get an assembly out of the None directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArcMSILNone()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,None}" };
            t.TargetProcessorArchitecture = "MSIL";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,None}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }
        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Target None and get an assembly out of the MSIL directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchNoneMSIL()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,MSIL}" };
            t.TargetProcessorArchitecture = "None";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,MSIL}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Target MSIL and get an assembly out of the MSIL directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchMSILMSIL()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine();
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,MSIL}" };
            t.TargetProcessorArchitecture = "MSIL";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,MSIL}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving. 
        /// 
        /// Target X86 and get an assembly out of the X86 directory.
        /// 
        /// </summary>
        [Fact]
        public void AssemblyFoldersExProcessorArchMatches()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "X86";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\Regress714052\X86\A.dll", t.ResolvedFiles[0].ItemSpec);
            AssertNoCase(@"{Registry:Software\Regress714052,v2.0.0,X86}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// If the target framework version specified in the registry search path
        /// provided by the targets file has some bogus value, we should just ignore it.
        /// 
        /// This means if there are remaining search paths to inspect, we should
        /// carry on and inspect those.
        /// </summary>
        [Fact]
        public void AssemblyFoldersExTargetFrameworkVersionBogusValue()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            ITaskItem assemblyToResolve = new TaskItem("MyGrid");
            assemblyToResolve.SetMetadata("HintPath", @"C:\MyComponents\MyGrid.dll");
            t.Assemblies = new ITaskItem[] { assemblyToResolve };
            t.SearchPaths = new string[] { @"{Registry:Software\Microsoft\.NetFramework,x.y.z,AssemblyFoldersEx}", "{HintPathFromItem}" };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(t.ResolvedFiles[0].GetMetadata("ResolvedFrom").Equals("{HintPathFromItem}", StringComparison.OrdinalIgnoreCase)); //                 "Assembly should have been resolved from HintPathFromItem!"
        }

        /// <summary>
        /// Tolerate keys like v2.0.x86chk.
        /// </summary>
        [Fact]
        public void Regress357227_AssemblyFoldersExAgainstRawDrop()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyRawDropControl") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\MyRawDropControls\MyRawDropControl.dll", t.ResolvedFiles[0].ItemSpec);
            AssertNoCase(@"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));
        }

        /// <summary>
        /// Matches that exist only in the HKLM hive.
        /// </summary>
        [Fact]
        public void AssemblyFoldersExHKLM()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyHKLMControl") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\MyComponents\HKLM Components\MyHKLMControl.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Matches that exist in both HKLM and HKCU should favor HKCU
        /// </summary>
        [Fact]
        public void AssemblyFoldersExHKCUTrumpsHKLM()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing AssemblyFoldersExHKCUTrumpsHKLM() test");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyHKLMandHKCUControl") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\MyComponents\HKCU Components\MyHKLMandHKCUControl.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// When matches that have v3.0 (future) and v2.0 (current) versions, the 2.0 version wins.
        /// </summary>
        [Fact]
        public void AssemblyFoldersExFutureTargetNDPVersionsDontMatch()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyControlWithFutureTargetNDPVersion") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\MyComponents\v2.0\MyControlWithFutureTargetNDPVersion.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If there is no v2.0 (current target NDP) match, then v1.0 should match.
        /// </summary>
        [Fact]
        public void AssemblyFoldersExMatchBackVersion()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyNDP1Control") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\MyComponents\v1.0\MyNDP1Control.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If there is a 2.0 and a 1.0 then match 2.0.
        /// </summary>
        [Fact]
        public void AssemblyFoldersExCurrentTargetVersionTrumpsPastTargetVersion()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyControlWithPastTargetNDPVersion") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\MyComponents\v2.0\MyControlWithPastTargetNDPVersion.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If a control has a service pack then that wins over the control itself
        /// </summary>
        [Fact]
        public void AssemblyFoldersExServicePackTrumpsBaseVersion()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyControlWithServicePack") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\MyComponentServicePack2\MyControlWithServicePack.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Conditions (OSVersion/Platform) can be passed in SearchPaths to filter the result.
        /// Test MaxOSVersion condition
        /// </summary>
        [Fact]
        public void AssemblyFoldersExConditionFilterMaxOS()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing AssemblyFoldersExConditionFilterMaxOS() test");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyDeviceControlAssembly") };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                @"c:\MyProject",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion",
                @"{Registry:Software\Microsoft\.NETCompactFramework,v2.0,PocketPC\AssemblyFoldersEx,OSVersion=4.0.0:Platform=3C41C503-53EF-4c2a-8DD4-A8217CAD115E}",
                "{AssemblyFolders}",
                "{HintPathFromItem}"
            };

            SetupAssemblyFoldersExTestConditionRegistryKey();

            try
            {
                Execute(t);
            }
            finally
            {
                RemoveAssemblyFoldersExTestConditionRegistryKey();
            }

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\V1ControlSP1\MyDeviceControlAssembly.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Conditions (OSVersion/Platform) can be passed in SearchPaths to filter the result.
        /// Test MinOSVersion condition
        /// </summary>
        [Fact]
        public void AssemblyFoldersExConditionFilterMinOS()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyDeviceControlAssembly") };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                @"c:\MyProject",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion",
                @"{Registry:Software\Microsoft\.NETCompactFramework,v2.0,PocketPC\AssemblyFoldersEx,OSVersion=5.1.0:Platform=3C41C503-53EF-4c2a-8DD4-A8217CAD115E}",
                "{AssemblyFolders}",
                "{HintPathFromItem}"
            };

            SetupAssemblyFoldersExTestConditionRegistryKey();

            try
            {
                Execute(t);
            }
            finally
            {
                RemoveAssemblyFoldersExTestConditionRegistryKey();
            }

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\V1Control\MyDeviceControlAssembly.dll", t.ResolvedFiles[0].ItemSpec);
        }

        [Fact]
        public void GatherVersions10DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v1.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(3, returnedVersions.Count);
            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersions20DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v2.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(4, returnedVersions.Count);
            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersions30DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v3.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(7, returnedVersions.Count);

            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v3.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[4].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[5].RegistryKey).Equals("v3.0SP1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[6].RegistryKey).Equals("v3.0 BAZ", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersionsVDotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(27, returnedVersions.Count);

            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v5.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v4.0001.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v4.1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[4].RegistryKey).Equals("v4.0.255.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[5].RegistryKey).Equals("v4.0.255", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[6].RegistryKey).Equals("v4.0.0000", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[7].RegistryKey).Equals("v4.0.9999", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[8].RegistryKey).Equals("v4.0.2116.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[9].RegistryKey).Equals("v4.0.2116", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[10].RegistryKey).Equals("v4.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[11].RegistryKey).Equals("v3.5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[12].RegistryKey).Equals("v3.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[13].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[14].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[15].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[16].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[17].RegistryKey).Equals("v3.0SP1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[18].RegistryKey).Equals("v3.0 BAZ", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[19].RegistryKey).Equals("v3.5.0.x86chk", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[20].RegistryKey).Equals("v3.5.1.x86chk", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[21].RegistryKey).Equals("v3.5.256.x86chk", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[22].RegistryKey).Equals("v", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[23].RegistryKey).Equals("V3.5.0.0.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[24].RegistryKey).Equals("V3..", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[25].RegistryKey).Equals("V-1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[26].RegistryKey).Equals("v9999999999999999", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersions35DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v3.5", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(10, returnedVersions.Count);
            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v3.5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v3.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[4].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[5].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[6].RegistryKey).Equals("v3.5.0.x86chk", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[7].RegistryKey).Equals("v3.5.1.x86chk", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[8].RegistryKey).Equals("v3.5.256.x86chk", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[9].RegistryKey).Equals("V3.5.0.0.0", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersions40DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(10, returnedVersions.Count);
            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v4.0.9999", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v4.0.2116.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v4.0.2116", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v4.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[4].RegistryKey).Equals("v3.5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[5].RegistryKey).Equals("v3.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[6].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[7].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[8].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[9].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersions400DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.0.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(11, returnedVersions.Count);
            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v4.0.0000", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v4.0.9999", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v4.0.2116.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v4.0.2116", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[4].RegistryKey).Equals("v4.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[5].RegistryKey).Equals("v3.5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[6].RegistryKey).Equals("v3.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[7].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[8].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[9].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[10].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersions41DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.1", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(14, returnedVersions.Count);

            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v4.1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v4.0.255.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v4.0.255", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v4.0.0000", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[4].RegistryKey).Equals("v4.0.9999", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[5].RegistryKey).Equals("v4.0.2116.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[6].RegistryKey).Equals("v4.0.2116", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[7].RegistryKey).Equals("v4.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[8].RegistryKey).Equals("v3.5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[9].RegistryKey).Equals("v3.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[10].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[11].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[12].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[13].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersions410DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.1.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(15, returnedVersions.Count);

            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v4.0001.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v4.1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v4.0.255.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v4.0.255", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[4].RegistryKey).Equals("v4.0.0000", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[5].RegistryKey).Equals("v4.0.9999", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[6].RegistryKey).Equals("v4.0.2116.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[7].RegistryKey).Equals("v4.0.2116", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[8].RegistryKey).Equals("v4.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[9].RegistryKey).Equals("v3.5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[10].RegistryKey).Equals("v3.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[11].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[12].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[13].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[14].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
        }


        [Fact]
        public void GatherVersions40255DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.0.255", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(13, returnedVersions.Count);
            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v4.0.255.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v4.0.255", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v4.0.0000", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v4.0.9999", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[4].RegistryKey).Equals("v4.0.2116.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[5].RegistryKey).Equals("v4.0.2116", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[6].RegistryKey).Equals("v4.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[7].RegistryKey).Equals("v3.5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[8].RegistryKey).Equals("v3.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[9].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[10].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[11].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[12].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersions5DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v5.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(17, returnedVersions.Count);

            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v5.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v4.0001.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v4.1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[4].RegistryKey).Equals("v4.0.255.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[5].RegistryKey).Equals("v4.0.255", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[6].RegistryKey).Equals("v4.0.0000", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[7].RegistryKey).Equals("v4.0.9999", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[8].RegistryKey).Equals("v4.0.2116.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[9].RegistryKey).Equals("v4.0.2116", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[10].RegistryKey).Equals("v4.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[11].RegistryKey).Equals("v3.5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[12].RegistryKey).Equals("v3.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[13].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[14].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[15].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[16].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersionsv5DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v5", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(17, returnedVersions.Count);

            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v5.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[1].RegistryKey).Equals("v5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[2].RegistryKey).Equals("v4.0001.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[3].RegistryKey).Equals("v4.1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[4].RegistryKey).Equals("v4.0.255.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[5].RegistryKey).Equals("v4.0.255", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[6].RegistryKey).Equals("v4.0.0000", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[7].RegistryKey).Equals("v4.0.9999", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[8].RegistryKey).Equals("v4.0.2116.87", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[9].RegistryKey).Equals("v4.0.2116", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[10].RegistryKey).Equals("v4.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[11].RegistryKey).Equals("v3.5", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[12].RegistryKey).Equals("v3.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[13].RegistryKey).Equals("v2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[14].RegistryKey).Equals("v1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[15].RegistryKey).Equals("v1", StringComparison.OrdinalIgnoreCase));
            Assert.True(((string)returnedVersions[16].RegistryKey).Equals("v00001.0", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void GatherVersions35x86chkDotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v3.5.0.x86chk", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(1, returnedVersions.Count);

            Assert.True(((string)returnedVersions[0].RegistryKey).Equals("v3.5.0.x86chk", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Conditions (OSVersion/Platform) can be passed in SearchPaths to filter the result.
        /// Test Platform condition
        /// </summary>
        [Fact]
        public void AssemblyFoldersExConditionFilterPlatform()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("MyDeviceControlAssembly") };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                @"c:\MyProject",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion",
                @"{Registry:Software\Microsoft\.NETCompactFramework,v2.0,PocketPC\AssemblyFoldersEx,Platform=3C41C503-X-4c2a-8DD4-A8217CAD115E}",
                "{AssemblyFolders}",
                "{HintPathFromItem}"
            };

            SetupAssemblyFoldersExTestConditionRegistryKey();

            try
            {
                Execute(t);
            }
            finally
            {
                RemoveAssemblyFoldersExTestConditionRegistryKey();
            }

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"C:\V1Control\MyDeviceControlAssembly.dll", t.ResolvedFiles[0].ItemSpec);
        }

        private void SetupAssemblyFoldersExTestConditionRegistryKey()
        {
            // Setup the following registry keys:
            //  HKCU\SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl
            //          @c:\V1Control
            //          @MinOSVersion=5.0.0
            //  HKCU\SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl\1234
            //          @c:\V1ControlSP1
            //          @MinOSVersion=4.0.0
            //          @MaxOSVersion=4.1.0
            //          @Platform=4118C335-430C-497f-BE48-11C3316B135E;3C41C503-53EF-4c2a-8DD4-A8217CAD115E

            RegistryKey baseKey = Registry.CurrentUser;
            RegistryKey folderKey = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl");
            folderKey.SetValue("", @"C:\V1Control");
            folderKey.SetValue("MinOSVersion", "5.0.0");

            RegistryKey servicePackKey = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl\1234");
            servicePackKey.SetValue("", @"C:\V1ControlSP1");
            servicePackKey.SetValue("MinOSVersion", "4.0.0");

            servicePackKey.SetValue("MaxOSVersion", "4.1.0");
            servicePackKey.SetValue("Platform", "4118C335-430C-497f-BE48-11C3316B135E;3C41C503-53EF-4c2a-8DD4-A8217CAD115E");
        }

        private void RemoveAssemblyFoldersExTestConditionRegistryKey()
        {
            RegistryKey baseKey = Registry.CurrentUser;
            try
            {
                baseKey.DeleteSubKeyTree(@"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl");
            }
            catch (Exception)
            {
            }
        }


        /// <summary>
        /// CandidateAssemblyFiles are extra files passed in through the CandidateAssemblyFiles
        /// that should be considered for matching whem search paths contains {CandidateAssemblyFiles}
        /// </summary>
        [Fact]
        public void CandidateAssemblyFiles()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("System.XML") };
            t.SearchPaths = new string[] { "{CandidateAssemblyFiles}" };
            t.CandidateAssemblyFiles = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll" };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll", t.ResolvedFiles[0].ItemSpec);
        }


        /// <summary>
        /// Make sure three part version numbers put on the required target framework do not cause a problem.
        /// </summary>
        [Fact]
        public void ThreePartVersionNumberRequiredFrameworkHigherThanTargetFramework()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();
            TaskItem item = new TaskItem("System.XML");
            item.SetMetadata("RequiredTargetFramework", "v4.0.255");
            t.Assemblies = new ITaskItem[] { item };
            t.SearchPaths = new string[] { "{CandidateAssemblyFiles}" };
            t.CandidateAssemblyFiles = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll" };
            t.TargetFrameworkVersion = "v4.0";
            Execute(t);

            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Make sure three part version numbers put on the required target framework do not cause a problem.
        /// </summary>
        [Fact]
        public void ThreePartVersionNumberRequiredFrameworkLowerThanTargetFramework()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();
            TaskItem item = new TaskItem("System.XML");
            item.SetMetadata("RequiredTargetFramework", "v4.0.255");
            t.Assemblies = new ITaskItem[] { item };
            t.SearchPaths = new string[] { "{CandidateAssemblyFiles}" };
            t.CandidateAssemblyFiles = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll" };
            t.TargetFrameworkVersion = "v4.0.256";
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Try a candidate assembly file that has an extension but no base name.
        /// </summary>
        [Fact]
        public void Regress242970()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] { new TaskItem("System.XML") };
            t.SearchPaths = new string[] { "{CandidateAssemblyFiles}" };
            t.CandidateAssemblyFiles = new string[]
            {
                @"NonUI\testDirectoryRoot\.hiddenfile",
                @"NonUI\testDirectoryRoot\.dll",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll"
            };

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll", t.ResolvedFiles[0].ItemSpec);

            // For {CandidateAssemblyFiles} we don't even want to see a comment logged for files with non-standard extensions.
            // This is because {CandidateAssemblyFiles} is very likely to contain non-assemblies and its best not to clutter
            // up the log.
            engine.AssertLogDoesntContain
            (
                String.Format(".hiddenfile")
            );

            // ...but we do want to see a log entry for standard extensions, even if the base file name is empty.
            engine.AssertLogContains
            (
                String.Format(@"NonUI\testDirectoryRoot\.dll")
            );
        }

        /// <summary>
        /// If a file name is passed in through the Assemblies parameter and the search paths contains {RawFileName}
        /// then try to resolve directly to that file name.
        /// </summary>
        [Fact]
        public void RawFileName()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing RawFileName() test");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll") };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                @"C:\MyProject",
                "{TargetFrameworkDirectory}",
                @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
                "{AssemblyFolders}",
                "{HintPathFromItem}",
                "{GAC}"
            };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Make sure when there are duplicate entries in the redist list, with different versions of ingac (true and false) that we will not read in two entries, 
        /// we will instead pick the one with ingac true and ignore the ingac false entry.   If there is one of more entries in the redist list with ingac false 
        /// and no entries with ingac true for a given assembly then we should only have one entry with ingac false.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingForRedistLists()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' />" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "<File AssemblyName='System.XML' Version='3.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' />" +
                  "<File AssemblyName='Microsoft.BuildEngine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' />" +
                  "<File AssemblyName='Microsoft.BuildEngine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "<File AssemblyName='Microsoft.BuildEngine' Version='3.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' />" +
              "</FileList >";

            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContentsDuplicates);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, null);
                Assert.Equal(0, errors.Count); // "Expected no Errors"
                Assert.Equal(0, errorFileNames.Count); // "Expected no Error file names"
                Assert.Equal(4, assembliesReadIn.Count);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Make sure that if there are different SimpleName then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandling()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208'/>" +
                  "<File AssemblyName='System.XML' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 1, 0);
        }

        /// <summary>
        /// Make sure that if there are different IsRedistRoot then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentIsRedistRoot()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' IsRedistRoot='true'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "<File AssemblyName='System.XML' Version='3.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' IsRedistRoot='false' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 3, 0);
        }

        /// <summary>
        /// Make sure that if there are different IsRedistRoot then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentName()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true'/>" +
                  "<File AssemblyName='MyAssembly' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "<File AssemblyName='AnotherAssembly' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 3, 0);
        }

        /// <summary>
        /// Make sure that if there are different culture then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentCulture()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-EN' FileVersion='2.0.50727.208' InGAC='true'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='fr-FR' FileVersion='2.0.50727.208' InGAC='true' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 3, 0);
        }

        /// <summary>
        /// Make sure that if there are different public key tokens then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentPublicKeyToken()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' />" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3d' Culture='Neutral' FileVersion='2.0.50727.208' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 3, 0);
        }

        /// <summary>
        /// Make sure that if there are different retargetable flags then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentRetargetable()
        {
            string fullRedistListContentsDuplicates =

              "<FileList Redist='Microsoft-Windows-CLRCoreComp'>" +
                  "<Remap>" +
                  "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                  "</Remap>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' Retargetable='Yes'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' Retargetable='Yes'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' Retargetable='No'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 2, 1);
        }
        /// <summary>
        /// Make sure that if there are different versons that they are all picked
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentVersion()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208'/>" +
                 "<Remap>" +
                     "<From AssemblyName='System.Xml2' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                        "<To AssemblyName='Remapped2' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                 "</Remap>" +
                  "<File AssemblyName='System.Xml' Version='3.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' />" +
                  "<File AssemblyName='System.Xml' Version='4.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208'/>" +
                  "<Remap>" +
                     "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                        "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                 "</Remap>" +
            "</FileList >";

            List<AssemblyEntry> entries = ExpectRedistEntries(fullRedistListContentsDuplicates, 3, 2);
        }

        /// <summary>
        /// Expect to read in a certain number of redist list entries, this is factored out becase we went to test a number of input combinations which will all result in entries returned.
        /// </summary>
        private static List<AssemblyEntry> ExpectRedistEntries(string fullRedistListContentsDuplicates, int numberOfExpectedEntries, int numberofExpectedRemapEntries)
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
            List<AssemblyRemapping> remapEntries = new List<AssemblyRemapping>();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContentsDuplicates);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remapEntries);
                Assert.Equal(0, errors.Count); // "Expected no Errors"
                Assert.Equal(0, errorFileNames.Count); // "Expected no Error file names"
                Assert.Equal(assembliesReadIn.Count, numberOfExpectedEntries);
                Assert.Equal(remapEntries.Count, numberofExpectedRemapEntries);
            }
            finally
            {
                File.Delete(redistFile);
            }

            return assembliesReadIn;
        }

        /// <summary>
        /// Test the basics of reading in the remapping section
        /// </summary>
        [Fact]
        public void TestRemappingSectionBasic()
        {
            string fullRedistListContents =
              "<Remap>" +
                  "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                 "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Equal(0, errors.Count); // "Expected no Errors"
                Assert.Equal(0, errorFileNames.Count); // "Expected no Error file names"
                Assert.Equal(1, remap.Count);

                AssemblyRemapping pair = remap[0];
                Assert.True(pair.From.Name.Equals("System.Xml", StringComparison.OrdinalIgnoreCase));
                Assert.True(pair.To.Name.Equals("Remapped", StringComparison.OrdinalIgnoreCase));
                Assert.True(pair.From.Retargetable);
                Assert.False(pair.To.Retargetable);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If there are multiple "To" elements under the "From" element then pick the first one.
        /// </summary>
        [Fact]
        public void MultipleToElementsUnderFrom()
        {
            string fullRedistListContents =
              "<Remap>" +
                  "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "<To AssemblyName='RemappedSecond' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                 "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Equal(0, errors.Count); // "Expected no Errors"
                Assert.Equal(0, errorFileNames.Count); // "Expected no Error file names"
                Assert.Equal(1, remap.Count);

                AssemblyRemapping pair = remap.First<AssemblyRemapping>();
                Assert.True(pair.From.Name.Equals("System.Xml", StringComparison.OrdinalIgnoreCase));
                Assert.True(pair.To.Name.Equals("Remapped", StringComparison.OrdinalIgnoreCase));
                Assert.True(pair.From.Retargetable);
                Assert.False(pair.To.Retargetable);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If there are two from tags which map to the same "To" element then we still need two entries.
        /// </summary>
        [Fact]
        public void DifferentFromsToSameTo()
        {
            string fullRedistListContents =
              "<Remap>" +
                  "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                   "<From AssemblyName='System.Core' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                    "</From>" +
                 "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Equal(0, errors.Count); // "Expected no Errors"
                Assert.Equal(0, errorFileNames.Count); // "Expected no Error file names"
                Assert.Equal(2, remap.Count);

                foreach (AssemblyRemapping pair in remap)
                {
                    Assert.True(pair.To.Name.Equals("Remapped", StringComparison.OrdinalIgnoreCase));
                    Assert.False(pair.To.Retargetable);
                }
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If there are two identical entries then pick the first one
        /// </summary>
        [Fact]
        public void DuplicateEntries()
        {
            string fullRedistListContents =
              "<Remap>" +
                  "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                   "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped2' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                    "</From>" +
                 "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Equal(0, errors.Count); // "Expected no Errors"
                Assert.Equal(0, errorFileNames.Count); // "Expected no Error file names"
                Assert.Equal(1, remap.Count);


                AssemblyRemapping pair = remap.First<AssemblyRemapping>();
                Assert.True(pair.To.Name.Equals("Remapped", StringComparison.OrdinalIgnoreCase));
                Assert.False(pair.To.Retargetable);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Test if the remapping section is empty
        /// </summary>
        [Fact]
        public void EmptyRemapping()
        {
            string fullRedistListContents = "<Remap/>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Equal(0, errors.Count); // "Expected no Errors"
                Assert.Equal(0, errorFileNames.Count); // "Expected no Error file names"
                Assert.Equal(0, remap.Count);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Test if the we have a "from" element but no "to" element. We expect that to be ignored
        /// </summary>
        [Fact]
        public void FromElementButNoToElement()
        {
            string fullRedistListContents =
        "<Remap>" +
         "<From AssemblyName='System.Core' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'/>" +
         "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
         "</From>" +
        "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Equal(0, errors.Count); // "Expected no Errors"
                Assert.Equal(0, errorFileNames.Count); // "Expected no Error file names"
                Assert.Equal(1, remap.Count);

                AssemblyRemapping pair = remap.First<AssemblyRemapping>();
                Assert.True(pair.From.Name.Equals("System.Xml", StringComparison.OrdinalIgnoreCase));
                Assert.True(pair.To.Name.Equals("Remapped", StringComparison.OrdinalIgnoreCase));
                Assert.True(pair.From.Retargetable);
                Assert.False(pair.To.Retargetable);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Test if the we have a "To" element but no "from" element. We expect that to be ignored
        /// </summary>
        [Fact]
        public void ToElementButNoFrom()
        {
            string fullRedistListContents =
        "<Remap>" +
         "<To AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'/>" +
        "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Equal(0, errors.Count); // "Expected no Errors"
                Assert.Equal(0, errorFileNames.Count); // "Expected no Error file names"
                Assert.Equal(0, remap.Count);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }


        /// <summary>
        /// If a relative file name is passed in through the Assemblies parameter and the search paths contains {RawFileName}
        /// then try to resolve directly to that file name and make it a full path.
        /// </summary>
        [Fact]
        public void RawFileNameRelative()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            string testPath = Path.Combine(Path.GetTempPath(), @"RawFileNameRelative");
            string previousCurrentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(testPath);
            Directory.SetCurrentDirectory(testPath);
            try
            {
                t.Assemblies = new ITaskItem[] { new TaskItem(@"..\RawFileNameRelative\System.Xml.dll") };
                t.SearchPaths = new string[] { "{RawFileName}" };
                Execute(t);

                Assert.Equal(1, t.ResolvedFiles.Length);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(testPath))
                {
                    Directory.Delete(testPath);
                }
            }
        }


        /// <summary>
        /// If a relative searchPath is passed in through the search path parameter 
        /// then try to resolve the file but make sure it is a full name
        /// </summary>
        [Fact]
        public void RelativeDirectoryResolver()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            string testPath = Path.Combine(Path.GetTempPath(), @"RawFileNameRelative");
            string previousCurrentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(testPath);
            Directory.SetCurrentDirectory(testPath);
            try
            {
                t.Assemblies = new ITaskItem[] { new TaskItem(@"System.Xml.dll") };
                t.SearchPaths = new string[] { "..\\RawFileNameRelative" };
                Execute(t);

                Assert.Equal(1, t.ResolvedFiles.Length);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(testPath))
                {
                    Directory.Delete(testPath);
                }
            }
        }

        /// <summary>
        /// If a relative file name is passed in through the HintPath then try to resolve directly to that file name and make it a full path.
        /// </summary>
        [Fact]
        public void HintPathRelative()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            string testPath = Path.Combine(Path.GetTempPath(), @"RawFileNameRelative");
            string previousCurrentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(testPath);
            Directory.SetCurrentDirectory(testPath);
            try
            {
                TaskItem taskItem = new TaskItem(AssemblyRef.SystemXml);
                taskItem.SetMetadata("HintPath", @"..\RawFileNameRelative\System.Xml.dll");

                t.Assemblies = new ITaskItem[] { taskItem };
                t.SearchPaths = new string[] { "{HintPathFromItem}" };
                Execute(t);

                Assert.Equal(1, t.ResolvedFiles.Length);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(testPath))
                {
                    Directory.Delete(testPath);
                }
            }
        }
        /// <summary>
        /// Make sure we do not crash if a raw file name is passed in and the specific version metadata is set
        /// </summary>
        [Fact]
        public void RawFileNameWithSpecificVersionFalse()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            ITaskItem taskItem = new TaskItem(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll");
            taskItem.SetMetadata("SpecificVersion", "false");

            t.Assemblies = new ITaskItem[] { taskItem };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
            };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Make sure we do not crash if a raw file name is passed in and the specific version metadata is set
        /// </summary>
        [Fact]
        public void RawFileNameWithSpecificVersionTrue()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            ITaskItem taskItem = new TaskItem(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll");
            taskItem.SetMetadata("SpecificVersion", "true");

            t.Assemblies = new ITaskItem[] { taskItem };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
            };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If the user passed in a file name but no {RawFileName} was specified.
        /// </summary>
        [Fact]
        public void Regress363340_RawFileNameMissing()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll"),
                new TaskItem(@"System.Data")
            };

            t.SearchPaths = new string[]
            {
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion",
            };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Data.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If the reference include looks like a file name rather than a properly formatted reference and a good hint path is provided, 
        /// good means the hintpath points to a file which exists on disk. Then we were getting an exception 
        /// because assemblyName was null and we were comparing the assemblyName from the hintPath to the null assemblyName.
        /// </summary>
        [Fact]
        public void Regress444793()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            TaskItem item = new TaskItem(@"c:\DoesntExist\System.Xml.dll");
            item.SetMetadata("HintPath", @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Data.dll");
            item.SetMetadata("SpecificVersion", "true");
            t.Assemblies = new ITaskItem[] { item };
            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            bool succeeded = Execute(t);
            Assert.True(succeeded);
            engine.AssertLogDoesntContain("MSB4018");

            engine.AssertLogContains
            (
                String.Format(AssemblyResources.GetString("General.MalformedAssemblyName"), "c:\\DoesntExist\\System.Xml.dll")
            );
        }


        /// <summary>
        /// If a file name is passed in through the Assemblies parameter and the search paths contains {RawFileName}
        /// then try to resolve directly to that file name.
        /// </summary>
        [Fact]
        public void RawFileNameDoesntExist()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] { new TaskItem(@"c:\DoesntExist\System.Xml.dll") };
            t.SearchPaths = new string[] { "{RawFileName}" };

            bool succeeded = Execute(t);
            Assert.True(succeeded);
            engine.AssertLogContains
            (
                String.Format(AssemblyResources.GetString("General.MalformedAssemblyName"), "c:\\DoesntExist\\System.Xml.dll")
            );
        }

        /// <summary>
        /// If a candidate file has a different base name, then this should not be a match.
        /// </summary>
        [Fact]
        public void CandidateAssemblyFilesDifferentBaseName()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem("VendorAssembly") };
            t.SearchPaths = new string[] { "{CandidateAssemblyFiles}" };
            t.CandidateAssemblyFiles = new string[] { @"Dlls\ProjectItemAssembly.dll" };

            Execute(t);

            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Given a strong name, resolve it to a location in the GAC if possible.
        /// </summary>
        [Fact]
        public void ResolveToGAC()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] { new TaskItem("System") };
            t.TargetedRuntimeVersion = typeof(Object).Assembly.ImageRuntimeVersion;
            t.SearchPaths = new string[] { "{GAC}" };
            bool succeeded = t.Execute();
            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Given a strong name, resolve it to a location in the GAC if possible.
        /// </summary>
        [Fact]
        public void ResolveToGACSpecificVersion()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            TaskItem item = new TaskItem("System");
            item.SetMetadata("SpecificVersion", "true");
            t.Assemblies = new ITaskItem[] { item };
            t.SearchPaths = new string[] { "{GAC}" };
            t.TargetedRuntimeVersion = new Version("0.5.0.0").ToString();
            bool succeeded = t.Execute();
            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Verify that when we are calculating the search paths for a dependency that we take into account where the parent assembly was resolved from 
        /// for example if the parent assembly was resolved from the GAC or AssemblyFolders then we do not want to look in the parent assembly directory 
        /// instead we want to let the assembly be resolved normally so that the GAC and AF checks will work.
        /// </summary>
        [Fact]
        public void ParentAssemblyResolvedFromAForGac()
        {
            Hashtable parentReferenceFolderHash = new Hashtable();
            List<string> parentReferenceFolders = new List<string>();
            List<Reference> referenceList = new List<Reference>();

            TaskItem taskItem = new TaskItem("Microsoft.VisualStudio.Interopt, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            reference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            reference.FullPath = "c:\\AssemblyFolders\\Microsoft.VisualStudio.Interopt.dll";
            reference.ResolvedSearchPath = "{AssemblyFolders}";

            Reference reference2 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            reference2.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            reference2.FullPath = "c:\\SomeOtherFolder\\Microsoft.VisualStudio.Interopt2.dll";
            reference2.ResolvedSearchPath = "c:\\SomeOtherFolder";

            Reference reference3 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            reference3.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            reference3.FullPath = "c:\\SomeOtherFolder\\Microsoft.VisualStudio.Interopt3.dll";
            reference3.ResolvedSearchPath = "{GAC}";

            referenceList.Add(reference);
            referenceList.Add(reference2);
            referenceList.Add(reference3);

            foreach (Reference parentReference in referenceList)
            {
                ReferenceTable.CalcuateParentAssemblyDirectories(parentReferenceFolderHash, parentReferenceFolders, parentReference);
            }

            Assert.Equal(1, parentReferenceFolders.Count);
            Assert.True(parentReferenceFolders[0].Equals(reference2.ResolvedSearchPath, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// Generate a fake reference which has been resolved from the gac. We will use it to verify the creation of the exclusion list.
        /// </summary>
        /// <returns></returns>
        private ReferenceTable GenerateTableWithAssemblyFromTheGlobalLocation(string location)
        {
            ReferenceTable referenceTable = new ReferenceTable(null, false, false, false, false, new string[0], null, null, null, null, null, null, SystemProcessorArchitecture.None, fileExists, null, null, null, null, null, null, null, null, null, new Version("4.0"), null, null, null, true, false, null, null, false, null, WarnOrErrorOnTargetArchitectureMismatchBehavior.None, false, false);

            AssemblyNameExtension assemblyNameExtension = new AssemblyNameExtension(new AssemblyName("Microsoft.VisualStudio.Interopt, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"));
            TaskItem taskItem = new TaskItem("Microsoft.VisualStudio.Interopt, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            reference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            // "Resolve the assembly from the gac"
            reference.FullPath = "c:\\Microsoft.VisualStudio.Interopt.dll";
            reference.ResolvedSearchPath = location;
            referenceTable.AddReference(assemblyNameExtension, reference);

            assemblyNameExtension = new AssemblyNameExtension(new AssemblyName("Team.System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"));
            taskItem = new TaskItem("Team, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

            reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            reference.MakePrimaryAssemblyReference(taskItem, false, ".dll");

            // "Resolve the assembly from the gac"
            reference.FullPath = "c:\\Team.System.dll";
            reference.ResolvedSearchPath = location;
            referenceTable.AddReference(assemblyNameExtension, reference);
            return referenceTable;
        }

        /// <summary>
        /// Given a reference that resolves to a bad image, we should get a warning and
        /// no reference. We don't want an exception.
        /// </summary>
        [Fact]
        public void ResolveBadImageInPrimary()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;
            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("BadImage")
            };
            t.Assemblies[0].SetMetadata("Private", "true");
            t.SearchPaths = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };
            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };

            Execute(t);

            // There should be no resolved file, because the image was bad.
            Assert.Equal(0, t.ResolvedFiles.Length);

            // There should be no related files either.
            Assert.Equal(0, t.RelatedFiles.Length);
            engine.AssertLogDoesntContain("BadImage.pdb");
            engine.AssertLogDoesntContain("HRESULT");

            // There should have been one warning about the exception.
            Assert.Equal(1, engine.Warnings);
        }

        /// <summary>
        /// Given a reference that resolves to a bad image, we should get a message, no warning and
        /// no reference. We don't want an exception.
        /// </summary>
        [Fact]
        public void ResolveBadImageInSecondary()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(true);
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("DependsOnBadImage")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress563286",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion"
            };
            Execute(t);

            // There should be one resolved file, because the dependency was bad.
            Assert.Equal(1, t.ResolvedFiles.Length);

            // There should be no related files.
            Assert.Equal(0, t.RelatedFiles.Length);
            engine.AssertLogDoesntContain("BadImage.pdb");
            engine.AssertLogDoesntContain("HRESULT");

            // There should have been no warning about the exception because it's only a dependency
            Assert.Equal(0, engine.Warnings);
        }

        /// <summary>
        /// Test the case where the search path, earlier on, contains an assembly that almost matches
        /// but the PKT is wrong.
        /// </summary>
        [Fact]
        public void ResolveReferenceThatHasWrongPKTInEarlierAssembly()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem(AssemblyRef.SystemData) };
            t.SearchPaths = new string[]
            {
                @"c:\MyProject",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion"
            };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Data.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// FX assemblies should not be CopyLocal.
        /// </summary>
        [Fact]
        public void PrimaryFXAssemblyRefIsNotCopyLocal()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] { new TaskItem(AssemblyRef.SystemData) };
            t.SearchPaths = new string[]
            {
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion"
            };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Data.dll", t.ResolvedFiles[0].ItemSpec);
            Assert.Equal("false", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
        }

        /// <summary>
        /// If an item is explictly Private=='true' (as opposed to implicitly when the attribute isn't set at all)
        /// then it should be CopyLocal true even if its in the FX directory
        /// </summary>
        [Fact]
        public void PrivateItemInFrameworksGetsCopyLocalTrue()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            // Create the mocks.
            Microsoft.Build.Shared.FileExists fileExists = new Microsoft.Build.Shared.FileExists(FileExists);
            Microsoft.Build.Shared.DirectoryExists directoryExists = new Microsoft.Build.Shared.DirectoryExists(DirectoryExists);
            Microsoft.Build.Tasks.GetDirectories getDirectories = new Microsoft.Build.Tasks.GetDirectories(GetDirectories);
            Microsoft.Build.Tasks.GetAssemblyName getAssemblyName = new Microsoft.Build.Tasks.GetAssemblyName(GetAssemblyName);
            Microsoft.Build.Tasks.GetAssemblyMetadata getAssemblyMetadata = new Microsoft.Build.Tasks.GetAssemblyMetadata(GetAssemblyMetadata);

            // Also construct a set of assembly names to pass in.
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
            };

            assemblyNames[0].SetMetadata("Private", "true"); // Fx file, but user chose private=true.

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };
            t.SearchPaths = DefaultPaths;
            Execute(t);
            Assert.Equal(@"true", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
        }

        /// <summary>
        /// If we have no framework directories passed in and an assembly is found outside of the GAC then it should be able to be copy local.
        /// </summary>
        [Fact]
        public void NoFrameworkDirectoriesStillCopyLocal()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            // Also construct a set of assembly names to pass in.
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem(@"C:\AssemblyFolder\SomeAssembly.dll"),
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.TargetFrameworkDirectories = new string[] { };
            t.SearchPaths = new string[] { "{RawFileName}" };
            Execute(t);
            Assert.Equal(@"true", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
        }

        /// <summary>
        /// If an item has a bad value for a boolean attribute, report a nice error that indicates which attribute it was.
        /// </summary>
        [Fact]
        public void Regress284485_PrivateItemWithBogusValue()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            // Also construct a set of assembly names to pass in.
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
            };

            assemblyNames[0].SetMetadata("Private", "bogus"); // Fx file, but user chose private=true.

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };
            t.SearchPaths = DefaultPaths;
            Execute(t);

            string message = String.Format(AssemblyResources.GetString("General.InvalidAttributeMetadata"), assemblyNames[0].ItemSpec, "Private", "bogus", "bool");
            Assert.True(
                engine.Log.Contains
                (
                    message
                )
            );
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A
        ///        Depends on D version 1
        ///   References - B
        ///        Depends on D version 2
        /// 
        /// And neither D1 nor D2 are CopyLocal = true. In this case, both dependencies
        /// are kept because this will work in a SxS manner.
        /// </summary>
        [Fact]
        public void ConflictBetweenNonCopyLocalDependencies()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries",
                @"c:\MyLibraries\v1",
                @"c:\MyLibraries\v2"
            };

            Execute(t);

            Assert.Equal(2, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\MyLibraries\v2\D.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\MyLibraries\v1\D.dll")); // "Expected to find assembly, but didn't."
            Assert.Equal(1, t.SuggestedRedirects.Length);
            Assert.True(ContainsItem(t.SuggestedRedirects, @"D, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")); // "Expected to find suggested redirect, but didn't"
            Assert.Equal(1, e.Warnings); // "Should only be one warning for suggested redirects."
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A
        ///        Depends on D version 1
        ///   References - B
        ///        Depends on D version 2
        /// 
        /// And both D1 and D2 are CopyLocal = true. This case is a warning because both
        /// assemblies can't be copied to the output directory.
        /// </summary>
        [Fact]
        public void ConflictBetweenCopyLocalDependencies()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] {
                new TaskItem("A"), new TaskItem("B")
            };

            t.SearchPaths = new string[] {
                @"c:\MyLibraries", @"c:\MyLibraries\v1", @"c:\MyLibraries\v2"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };

            bool result = Execute(t);

            Assert.Equal(1, engine.Warnings); // @"Expected a warning because this is an unresolvable conflict."
            Assert.Equal(1, t.SuggestedRedirects.Length);
            Assert.True(ContainsItem(t.SuggestedRedirects, @"D, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")); // "Expected to find suggested redirect, but didn't"
            Assert.Equal(1, engine.Warnings); // "Should only be one warning for suggested redirects."
            Assert.True(
                engine.Log.Contains
                (
                    String.Format
                    (
                        AssemblyResources.GetString
                        (
                            "ResolveAssemblyReference.ConflictRedirectSuggestion"
                        ),
                        "D, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa",
                        "1.0.0.0",
                        "c:\\MyLibraries\\v1\\D.dll",
                        "2.0.0.0",
                        "c:\\MyLibraries\\v2\\D.dll"
                    )
                )
            );
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   Primary References
        ///         C
        ///         A version 2
        ///         And both A version 2 and C are CopyLocal=true
        ///   References - C
        ///        Depends on A version 1
        ///        Depends on B
        ///   References - B
        ///        Depends on A version 2
        /// 
        /// 
        /// Expect to have some information indicating that C and B depend on two different versions of A and that the primary refrence which caused the problems
        /// are A and C.
        /// </summary>
        [Fact]
        public void ConflictBetweenCopyLocalDependenciesRegress444809()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] {
                new TaskItem("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null"), new TaskItem("C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null")
            };

            t.SearchPaths = new string[] {
                @"c:\Regress444809", @"c:\Regress444809\v2"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };

            bool result = Execute(t);
            ResourceManager resources = new ResourceManager("Microsoft.Build.Tasks.Strings", Assembly.GetExecutingAssembly());

            //Unresolved primary reference with itemspec "A, Version=20.0.0.0, Culture=Neutral, PublicKeyToken=null".
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.ReferenceDependsOn", "A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null", @"c:\Regress444809\A.dll");
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.ReferenceDependsOn", "A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", @"c:\Regress444809\v2\A.dll");
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.PrimarySourceItemsForReference", @"c:\Regress444809\C.dll");
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.PrimarySourceItemsForReference", @"c:\Regress444809\B.dll");
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.PrimarySourceItemsForReference", @"c:\Regress444809\v2\a.dll");
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   Primary References
        ///         A version 20 (Un Resolved)
        ///         B
        ///         D
        ///   References - B
        ///        Depends on A version 2
        ///   References - D
        ///        Depends on A version 20
        /// 
        /// 
        /// Expect to have some information indicating that Primary reference A, Reference B and Reference D conflict.
        /// </summary>
        [Fact]
        public void ConflictBetweenCopyLocalDependenciesRegress444809UnResolvedPrimaryReference()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] {
                new TaskItem("A, Version=20.0.0.0, Culture=Neutral, PublicKeyToken=null"),
                new TaskItem("B, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null"),
                new TaskItem("D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null")
            };

            t.SearchPaths = new string[] {
                @"c:\Regress444809", @"c:\Regress444809\v2"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };

            bool result = Execute(t);

            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.ReferenceDependsOn", "A, Version=20.0.0.0, Culture=Neutral, PublicKeyToken=null", String.Empty);
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.ReferenceDependsOn", "A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", @"c:\Regress444809\v2\A.dll");
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.UnResolvedPrimaryItemSpec", "A, Version=20.0.0.0, Culture=Neutral, PublicKeyToken=null");
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.PrimarySourceItemsForReference", @"c:\Regress444809\D.dll");
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.PrimarySourceItemsForReference", @"c:\Regress444809\B.dll");
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A
        ///        Depends on D version 1
        ///   References - B
        ///        Depends on D version 2
        /// 
        /// And both D1 and D2 are CopyLocal = true. In this case, there is no warning because
        /// AutoUnify is set to true.
        /// </summary>
        [Fact]
        public void ConflictBetweenCopyLocalDependenciesWithAutoUnify()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;
            t.AutoUnify = true;

            t.Assemblies = new ITaskItem[] {
                new TaskItem("A"), new TaskItem("B")
            };

            t.SearchPaths = new string[] {
                @"c:\MyLibraries", @"c:\MyLibraries\v1", @"c:\MyLibraries\v2"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };

            bool result = Execute(t);

            // RAR will now produce suggested redirects even if AutoUnify is on.
            Assert.Equal(1, t.SuggestedRedirects.Length);
            Assert.Equal(0, engine.Warnings); // "Should be no warning for suggested redirects."
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A
        ///        Depends on D version 1
        ///   References - B
        ///        Depends on D version 2
        ///   References - D, version 1
        /// 
        /// Both D1 and D2 are CopyLocal. This is a warning because D1 is a lower version
        /// than D2 so that can't unify. These means that eventually when they're copied 
        /// to the output directory they'll conflict.
        /// </summary>
        [Fact]
        public void ConflictWithBackVersionPrimary()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("A"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries", @"c:\MyLibraries\v2", @"c:\MyLibraries\v1"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };

            bool result = Execute(t);

            Assert.Equal(1, e.Warnings); // @"Expected one warning."

            Assert.Equal(0, t.SuggestedRedirects.Length);
            Assert.Equal(3, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\v1\D.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Same as ConflictWithBackVersionPrimary, except AutoUnify is true.
        /// Even when AutoUnify is set we should see a warning since the binder will not allow
        /// an older version to satisfy a reference to a newer version.
        /// </summary>
        [Fact]
        public void ConflictWithBackVersionPrimaryWithAutoUnify()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("A"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.AutoUnify = true;

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries", @"c:\MyLibraries\v2", @"c:\MyLibraries\v1"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };

            bool result = Execute(t);

            Assert.Equal(1, e.Warnings); // @"Expected one warning."

            Assert.Equal(0, t.SuggestedRedirects.Length);
            Assert.Equal(3, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\v1\D.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A
        ///        Depends on D version 1
        ///   References - B
        ///        Depends on D version 2
        ///   References - D, version 2
        /// 
        /// Both D1 and D2 are CopyLocal. This is not an error because D2 is a higher version
        /// than D1 so that can unify. D2 should be output as a Primary and D1 should be output
        /// as a dependency.
        /// </summary>
        [Fact]
        public void ConflictWithForeVersionPrimary()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("A"),
                new TaskItem("D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[] {
                @"c:\MyLibraries", @"c:\MyLibraries\v2", @"c:\MyLibraries\v1"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };

            bool result = Execute(t);

            Assert.True(result); // @"Expected a success because this conflict is solvable."
            Assert.Equal(3, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\v2\D.dll")); // "Expected to find assembly, but didn't."
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
        }


        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - D, version 1
        ///   References - D, version 2
        /// 
        /// Both D1 and D2 are CopyLocal. This is an error because both D1 and D2 can't be copied to 
        /// the output directory. 
        /// </summary>
        [Fact]
        public void ConflictBetweenBackAndForeVersionsCopyLocal()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries", @"c:\MyLibraries\v2", @"c:\MyLibraries\v1"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };

            bool result = Execute(t);

            Assert.Equal(2, e.Warnings); // @"Expected a warning because this is an unresolvable conflict."
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\v2\D.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\v1\D.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - D, version 1
        ///   References - D, version 2
        /// 
        /// Neither D1 nor D2 are CopyLocal. This is a solveable conflict because D2 has a higher version
        /// than D1 and there won't be an output directory conflict.
        /// </summary>
        [Fact]
        public void ConflictBetweenBackAndForeVersionsNotCopyLocal()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[] {
                @"c:\MyLibraries", @"c:\MyLibraries\v2", @"c:\MyLibraries\v1"
            };

            bool result = Execute(t);

            Assert.True(result); // @"Expected success because this conflict is solvable."
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\v2\D.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\v1\D.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A
        ///        Depends on D version 1, PKT=XXXX
        ///   References - C
        ///        Depends on D version 1, PKT=YYYY
        /// 
        /// We can't tell which should win because the PKTs are different. This should be an error.
        /// </summary>
        [Fact]
        public void ConflictingDependenciesWithNonMatchingNames()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("C")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries", @"c:\MyLibraries\v1", @"c:\RogueLibraries\v1"
            };

            bool result = Execute(t);
            Assert.True(result); // "Execute should have failed because of insoluble conflict."
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A
        ///        Depends on D version 1, PKT=XXXX
        ///   References - C
        ///        Depends on D version 1, PKT=YYYY
        ///   References - D version 1, PKT=XXXX
        /// 
        /// D, PKT=XXXX should win because its referenced in the project.
        /// 
        /// </summary>
        [Fact]
        public void ConflictingDependenciesWithNonMatchingNamesAndHardReferenceInProject()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("C"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries", @"c:\MyLibraries\v1", @"c:\RogueLibraries\v1"
            };

            Execute(t);

            Assert.Equal(3, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\v1\D.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// A reference with a bogus version is provided. However, the user has chosen 
        /// SpecificVersion='false' so we match the first one we come across.
        /// </summary>
        [Fact]
        public void SpecificVersionFalse()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem(@"System.XML, Version=9.9.9999.9, Culture=neutral, PublicKeyToken=abababababababab")
            };

            t.Assemblies[0].SetMetadata("SpecificVersion", "false");

            t.SearchPaths = DefaultPaths;
            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.XML.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// A reference with a bogus version is provided and the user has chosen SpecificVersion=true.
        /// In this case, since there is no specific version that can be matched, no reference is returned.
        /// </summary>
        [Fact]
        public void SpecificVersionTrue()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] {
                new TaskItem(@"System.XML, Version=9.9.9999.9, Culture=neutral, PublicKeyToken=abababababababab")
            };

            t.Assemblies[0].SetMetadata("SpecificVersion", "true");

            t.SearchPaths = DefaultPaths;
            Execute(t);

            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// A reference with a bogus version is provided and the user has left off SpecificVersion.
        /// In this case assume SpecificVersion=true implicitly. 
        /// </summary>
        [Fact]
        public void SpecificVersionAbsent()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[] {
                new TaskItem(@"System.XML, Version=9.9.9999.9, Culture=neutral, PublicKeyToken=abababababababab")
            };

            t.SearchPaths = DefaultPaths;
            Execute(t);

            Assert.Equal(0, t.ResolvedFiles.Length);
        }


        /// <summary>
        /// Unresolved primary references should result in warnings.
        /// </summary>
        [Fact]
        public void Regress199998()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine m = new MockEngine();
            t.BuildEngine = m;

            t.Assemblies = new ITaskItem[]
            {
                // An assembly that is unresolvable because it doesn't exist.
                new TaskItem(@"System.XML, Version=9.9.9999.9, Culture=neutral, PublicKeyToken=abababababababab")
            };

            t.SearchPaths = DefaultPaths;
            Execute(t);

            Assert.Equal(0, t.ResolvedFiles.Length);
            // One warning for the un-resolved reference and one warning saying you are trying to target an assembly higher than the current target
            // framework.
            Assert.Equal(1, m.Warnings);
        }


        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has an <ExecutableExtension>.exe</ExecutableExtension> tag.
        /// - Both a.exe and a.dll exist on disk.
        /// Expected:
        /// - The resulting assembly returned should be a.exe
        /// Rationale:
        /// The user browsed to an .exe, so that's what we should give them.
        /// </summary>
        [Fact]
        public void ExecutableExtensionEXE()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.Assemblies[0].SetMetadata("ExecutableExtension", ".eXe");

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries",
                @"c:\MyExecutableLibraries"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyExecutableLibraries\a.exe")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has an <ExecutableExtension>.dll</ExecutableExtension> tag.
        /// - Both a.exe and a.dll exist on disk.
        /// Expected:
        /// - The resulting assembly returned should be a.dll
        /// Rationale:
        /// The user browsed to a .dll, so that's what we should give them.
        /// </summary>
        [Fact]
        public void ExecutableExtensionDLL()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.Assemblies[0].SetMetadata("ExecutableExtension", ".DlL");

            t.SearchPaths = new string[]
            {
                @"c:\MyExecutableLibraries",
                @"c:\MyLibraries"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\a.DlL")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has no <ExecutableExtension></ExecutableExtension> tag.
        /// - Both a.exe and a.dll exist on disk.
        /// - A.dll is first in the search order.
        /// Expected:
        /// - The resulting assembly returned should be a.dll
        /// Rationale:
        /// Without an ExecutableExtension the first assembly out of .dll,.exe wins.
        /// </summary>
        [Fact]
        public void ExecutableExtensionDefaultDLLFirst()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries",
                @"c:\MyExecutableLibraries"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\a.DlL")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has no <ExecutableExtension></ExecutableExtension> tag.
        /// - Both a.exe and a.dll exist on disk.
        /// - A.exe is first in the search order.
        /// Expected:
        /// - The resulting assembly returned should be a.exe
        /// Rationale:
        /// Without an ExecutableExtension the first assembly out of .dll,.exe wins.
        /// </summary>
        [Fact]
        public void ExecutableExtensionDefaultEXEFirst()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyExecutableLibraries",
                @"c:\MyLibraries"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyExecutableLibraries\A.exe")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has <SpecificVersion>true</SpecificVersion> tag.
        /// - An assembly with a strong fusion name "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" exists first in the search order.
        /// - An assembly with a weak fusion name "A, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" is second in the search order.
        /// Expected:
        /// - This is an unresolved reference.
        /// Rationale:
        /// If specific version is true, but the reference is a simple name like "A", then there is no way to do a specific version match.
        /// This is a corner case. Other solutions that might have been just as good:
        /// - Fall back to SpecificVersion=false behavior.
        /// - Only match "A, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null". Note that all of our default VS projects have at least a version number.
        /// </summary>
        [Fact]
        public void SimpleNameWithSpecificVersionTrue()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };
            t.Assemblies[0].SetMetadata("SpecificVersion", "true");

            t.SearchPaths = new string[]
            {
                @"c:\MyStronglyNamed",
                @"c:\MyLibraries"
            };

            Execute(t);

            Assert.Equal(1, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has <SpecificVersion>true</SpecificVersion> tag.
        /// - An assembly with a strong fusion name "A, PKT=..., Version=..., Culture=..." exists first in the search order.
        /// - An assembly with a weak fusion name "A, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" is second in the search order.
        /// Expected:
        /// - The resulting assembly returned should be the strongly named a.dll.
        /// Rationale:
        /// If specific version is false, then we should match the first "A" that we find.
        /// </summary>
        [Fact]
        public void SimpleNameWithSpecificVersionFalse()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };
            t.Assemblies[0].SetMetadata("SpecificVersion", "false");

            t.SearchPaths = new string[]
            {
                @"c:\MyStronglyNamed",
                @"c:\MyLibraries"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyStronglyNamed\A.dll")); // "Expected to find assembly, but didn't."
        }


        /// <summary>
        /// Consider this situation:
        /// 
        /// App
        ///   References - D, version 1, IrreleventKeyValue=poo.
        /// 
        /// There's plenty of junk that might end up in a fusion name that have nothing to do with 
        /// assembly resolution. Make sure we can tolerate this for primary references.
        /// </summary>
        [Fact]
        public void IrrelevantAssemblyNameElement()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa, IrreleventKeyValue=poo"),
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries", @"c:\MyLibraries\v2", @"c:\MyLibraries\v1"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion" };

            bool result = Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\v1\D.dll")); // "Expected to find assembly, but didn't."
        }


        /// <summary>
        /// Regress EVERETT QFE 626
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A (Private=undefined)
        ///        Depends on D 
        ///             Depends on E
        ///   References - D (Private=false)
        /// 
        /// - Reference A does not have a Private attribute, but resolves to CopyLocal=true.
        /// - Reference D has explicit Private=false.
        /// - D would normally be CopyLocal=true.
        /// - E would normally be CopyLocal=true.
        /// 
        /// Expected:
        /// - D should be CopyLocal=false because the of the matching Reference D which has explicit private=false.
        /// - E should be CopyLocal=false because it's a dependency of D which has explicit private=false.
        /// 
        /// Rationale:
        /// This is QFE 626. If the user has set "Copy Local" to "false" in VS (means Private=false)
        /// then even if this turns out to be a dependency too, we still shouldn't copy.
        /// 
        /// </summary>
        [Fact]
        public void RegressQFE626()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("D")
            };
            t.Assemblies[1].SetMetadata("Private", "false");

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries", @"c:\MyLibraries\v1", @"c:\MyLibraries\v1\E"
            };
            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };

            Execute(t);

            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length); // Not 2 because D is treated as a primary reference.
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\MyLibraries\v1\E\E.dll")); // "Expected to find assembly, but didn't."
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(0, engine.Errors);

            foreach (ITaskItem item in t.ResolvedDependencyFiles)
            {
                if (0 == String.Compare(item.ItemSpec, @"c:\MyLibraries\v1\E\E.dll", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal("false", item.GetMetadata("CopyLocal"));
                }
            }
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A (private=false)
        ///        Depends on D v1
        ///             Depends on E
        ///   References - B (private=true)
        ///        Depends on D v2
        ///             Depends on E
        /// 
        /// Reference A is explicitly Private=false.
        /// Reference B is explicitly Private=true.
        /// Dependencies D and E would normally be CopyLocal=true.
        /// 
        /// Expected:
        /// - D will be CopyLocal=false because it's dependency of A, which is private=false.
        /// - E will be CopyLocal=true because all source primary references aren't private=false.
        /// 
        /// Rationale:
        /// Dependencies will be CopyLocal=false if all source primary references are Private=false.
        /// 
        /// </summary>
        [Fact]
        public void Regress265054()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };
            t.Assemblies[0].SetMetadata("Private", "false");
            t.Assemblies[1].SetMetadata("Private", "true");

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries", @"c:\MyLibraries\v1", @"c:\MyLibraries\v2", @"c:\MyLibraries\v1\E"
            };
            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };

            Execute(t);

            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(3, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\MyLibraries\v1\D.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\MyLibraries\v1\E\E.dll")); // "Expected to find assembly, but didn't."

            foreach (ITaskItem item in t.ResolvedDependencyFiles)
            {
                if (0 == String.Compare(item.ItemSpec, @"c:\MyLibraries\v1\D.dll", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal("false", item.GetMetadata("CopyLocal"));
                }

                if (0 == String.Compare(item.ItemSpec, @"c:\MyLibraries\v1\E\E.dll", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal("true", item.GetMetadata("CopyLocal"));
                }
            }
        }

        /// <summary>
        /// Here's how you get into this situation:
        /// 
        /// App
        ///   References - A 
        ///   References - B 
        ///        Depends on A
        /// 
        ///    And, the following conditions.
        ///     Primary "A" has no explicit Version (i.e. it's a simple name)
        ///        Primary "A" *is not* resolved.
        ///        Dependency "A" *is* resolved.
        /// 
        /// Expected result:
        /// * No exceptions.
        /// * Build error about unresolved primary reference.
        /// 
        /// </summary>
        [Fact]
        public void Regress312873_UnresolvedPrimaryWithResolveDependency()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B"),

                // We need a one more "A" because the bug was in a Compare function
                // called by .Sort. We need enough items to guarantee that A with null version 
                // will be on the left side of a compare.
                new TaskItem("A")
};

            t.Assemblies[1].SetMetadata("HintPath", @"C:\Regress312873\b.dll");
            t.Assemblies[2].SetMetadata("HintPath", @"C:\Regress312873-2\a.dll");

            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            Execute(t);
        }

        /// <summary>
        /// We weren't handling scatter assemblies.
        /// 
        /// App
        ///   References - A 
        /// 
        ///    And, the following conditions.
        ///     Primary "A" has has two scatter files "M1" and "M2"
        /// 
        /// Expected result:
        /// * M1 and M2 should be output in ScatterFiles and CopyLocal.
        /// 
        /// </summary>
        [Fact]
        public void Regress275161_ScatterAssemblies()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress275161\a.dll");

            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };

            Execute(t);

            Assert.True(ContainsItem(t.ScatterFiles, @"C:\Regress275161\m1.netmodule")); //                 "Expected to find scatter file m1."


            Assert.True(ContainsItem(t.ScatterFiles, @"C:\Regress275161\m2.netmodule")); //                 "Expected to find scatter file m2."


            Assert.True(ContainsItem(t.CopyLocalFiles, @"C:\Regress275161\m1.netmodule")); //                 "Expected to find scatter file m1 in CopyLocalFiles."


            Assert.True(ContainsItem(t.CopyLocalFiles, @"C:\Regress275161\m2.netmodule")); //                 "Expected to find scatter file m2 in CopyLocalFiles."
        }

        /// <summary>
        /// We weren't handling scatter assemblies.
        /// 
        /// App
        ///   References - A 
        ///        Depends on B v1.0.0.0
        ///   References - B v2.0.0.0
        ///        
        /// 
        ///    And, the following conditions.
        ///    * All assemblies are resolved.
        /// * All assemblies are CopyLocal=true.
        /// * Notice the conflict between versions of B.
        /// 
        /// Expected result:
        /// * During conflict resolution, B v2.0.0.0 should win.
        /// * B v1.0.0.0 should still be listed in dependencies (there's not a strong case for this either way)
        /// * B v1.0.0.0 should be CopyLocal='false'
        /// 
        /// </summary>
        [Fact]
        public void Regress317975_LeftoverLowerVersion()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
};

            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress317975\a.dll");
            t.Assemblies[1].SetMetadata("HintPath", @"C:\Regress317975\v2\b.dll");

            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };

            Execute(t);

            foreach (ITaskItem i in t.ResolvedDependencyFiles)
            {
                Assert.Equal(0, String.Compare(i.GetMetadata("CopyLocal"), "false", StringComparison.OrdinalIgnoreCase));
            }

            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"C:\Regress317975\B.dll")); //                 "Expected to find lower version listed in dependencies."
        }

        /// <summary>
        /// Mscorlib is special in that it doesn't always have complete metadata. For example,
        /// GetAssemblyName can return null. This was confusing the {RawFileName} resolution path,
        /// which is fairly different from the other code paths.
        /// 
        /// App
        ///   References - "c:\path-to-mscorlib\mscorlib.dll" (Current FX)
        /// 
        /// Expected result:
        /// * Even though mscorlib.dll doesn't have an assembly name, we should be able to return
        ///   a result.
        /// 
        /// NOTES:
        /// * This test works because path-to-mscorlib is the same as the path to the FX folder.
        ///   Because of this, the hard-cache is used rather than actually calling GetAssemblyName
        ///   on mscorlib.dll. This isn't going to work in cases where mscorlib is from an FX other
        ///   than the current target. See the Part2 for a test that covers this other case.
        /// 
        /// </summary>
        [Fact]
        public void Regress313086_Part1_MscorlibAsRawFilename()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem(typeof(object).Module.FullyQualifiedName.ToLower())
};

            t.SearchPaths = new string[]
            {
                @"{RawFileName}"
            };

            t.TargetFrameworkDirectories = new string[] { Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName) };

            t.Execute();

            Assert.Equal(1, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Mscorlib is special in that it doesn't always have complete metadata. For example,
        /// GetAssemblyName can return null. This was confusing the {RawFileName} resolution path,
        /// which is fairly different from the other code paths.
        /// 
        /// App
        ///   References - "c:\path-to-mscorlib\mscorlib.dll" (non-Current FX)
        /// 
        /// Expected result:
        /// * Even though mscorlib.dll doesn't have an assembly name, we should be able to return
        ///   a result.
        /// 
        /// NOTES:
        /// * This test is covering the case where mscorlib.dll is coming from somewhere besides
        ///   the main (ie Whidbey) FX.
        /// 
        /// </summary>
        [Fact]
        public void Regress313086_Part2_MscorlibAsRawFilename()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem(@"c:\Regress313086\mscorlib.dll")
};

            t.SearchPaths = new string[]
            {
                @"{RawFileName}"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };

            Execute(t);

            Assert.Equal(1, t.ResolvedFiles.Length);
        }


        /// <summary>
        /// If a directory path is passed into AssemblyFiles, then we should warn and continue on.
        /// </summary>
        [Fact]
        public void Regress284466_DirectoryIntoAssemblyFiles()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\MyComponents\v1.0\UnifyMe.dll"),
                        new TaskItem(Path.GetTempPath())
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            t.SearchPaths = DefaultPaths;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));
            Assert.True(
                engine.Log.Contains
                (
                    String.Format(AssemblyResources.GetString("General.ExpectedFileGotDirectory"), Path.GetTempPath())
                )
            );
        }

        /// <summary>
        /// If a relative assemblyFile is passed in resolve it as a full path.
        /// </summary>
        [Fact]
        public void RelativeAssemblyFiles()
        {
            string testPath = Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles");
            string previousCurrentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(testPath);
            Directory.SetCurrentDirectory(testPath);
            try
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"..\RelativeAssemblyFiles\System.Xml.dll")
                    };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(testPath))
                {
                    Directory.Delete(testPath);
                }
            }
        }


        /// <summary>
        /// Behave gracefully if a referenced assembly is inaccessible to the user.
        /// </summary>
        [Fact]
        public void Regress316906_UnauthorizedAccessViolation_PrimaryReferenceIsInaccessible()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };
            t.Assemblies[0].SetMetadata("SpecificVersion", "false");

            t.SearchPaths = new string[]
            {
                @"c:\MyInaccessible"
            };

            Execute(t);

            Assert.Equal(1, e.Warnings); // "One warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Behave gracefully if a referenced assembly is inaccessible to the user.
        /// In this case, the file is still resolved because it was passed in directly.
        /// There's no way to determine dependencies however.
        /// </summary>
        [Fact]
        public void Regress316906_UnauthorizedAccessViolation_PrimaryFileIsInaccessible()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.AssemblyFiles = new ITaskItem[]
            {
                new TaskItem(@"c:\MyInaccessible\A.dll")
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
        }


        /// <summary>
        /// Behave gracefully if a referenced assembly is inaccessible to the user.
        /// </summary>
        [Fact]
        public void Regress316906_UnauthorizedAccessViolation_PrimaryAsRawFileIsInaccessible()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem(@"c:\MyInaccessible\A.dll")
            };
            t.SearchPaths = new string[] { "{RawFileName}" };


            Execute(t);

            Assert.Equal(1, e.Warnings); // "One warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);
        }



        /// <summary>
        /// If there's a SearhPath like {Registry:,,} then still behave nicely.
        /// </summary>
        [Fact]
        public void Regress269704_MissingRegistryElements()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };
            t.Assemblies[0].SetMetadata("SpecificVersion", "false");

            t.SearchPaths = new string[]
            {
                @"{Registry:,,}",
                @"c:\MyAssemblyDoesntExistHere"
            };

            Execute(t);

            Assert.Equal(1, e.Warnings); // "No warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// 1.  Create a C# classlibrary, and build it.
        /// 2.  Go to disk, and rename ClassLibrary1.dll (or whatever it was) to Foo.dll
        /// 3.  Create a C# console application.
        /// 4.  In the console app, add a File reference to Foo.dll.
        /// 5.  Build the console app.
        /// 
        /// RESULTS (before bugfix):
        /// ========================
        /// MSBUILD : warning : Couldn't resolve this reference.  Could not locate assembly "ClassLibrary1"
        /// 
        /// EXPECTED (after bugfix):
        /// ========================
        /// We think it might be reasonable for the ResolveAssemblyReference task to correctly resolve 
        /// this reference, especially given the fact that the HintPath was provided in the project file.
        /// </summary>
        [Fact]
        public void Regress276548_AssemblyNameDifferentThanFusionName()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };
            t.Assemblies[0].SetMetadata
            (
                "HintPath",
                @"c:\MyNameMismatch\Foo.dll"
            );

            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            Execute(t);


            Assert.Equal(0, e.Warnings); // "One warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// When very long paths are passed in we should be robust.
        /// </summary>
        [Fact]
        public void Regress314573_VeryLongPaths()
        {
            string veryLongPath = @"C:\" + new String('a', 260);
            string veryLongFile = veryLongPath + "\\A.dll";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")                    // Resolved by HintPath        
            };
            t.Assemblies[0].SetMetadata
            (
                "HintPath",
                veryLongFile
            );

            t.SearchPaths = new string[]
            {
                "{HintPathFromItem}"
            };

            t.AssemblyFiles = new ITaskItem[]
            {
                new TaskItem(veryLongFile)            // Resolved as File Reference
            };

            Execute(t);


            Assert.Equal(1, e.Warnings); // "One warning expected in this scenario." // Couldn't find dependencies for {HintPathFromItem}-resolved item.
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);  // This test used to have 1 here. But that was because the mock GetAssemblyName was not accurately throwing an exception for non-existent files.
        }


        /// <summary>
        /// Need to be robust in the face of assembly names with special characters.
        /// </summary>
        [Fact]
        public void Regress265003_EscapedCharactersInFusionName()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("\\=A\\=, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089"), // Characters that should be escaped in fusion names: \ , " ' = 
                new TaskItem("__\\'ASP\\'dw0024ry")
            };

            t.Assemblies[0].SetMetadata("SpecificVersion", "false");    // Important to this bug.
            t.Assemblies[1].SetMetadata("HintPath", @"c:\MyEscapedName\__'ASP'dw0024ry.dll");
            t.TargetFrameworkDirectories = new string[] { Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName) };


            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}",
                @"{HintPathFromItem}",
                @"c:\MyEscapedName"
            };

            Execute(t);


            Assert.Equal(0, e.Warnings); // "One warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(2, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// If we're given bogus Include (one with characters that would normally need escaping) but we also 
        /// have a hintpath, then go ahead and resolve anyway because we know what the path should be.
        /// </summary>
        [Fact]
        public void Regress284081_UnescapedCharactersInFusionNameWithHintPath()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("__'ASP'dw0024ry")    // Would normally require quoting for the tick marks.
            };

            t.Assemblies[0].SetMetadata("HintPath", @"c:\MyEscapedName\__'ASP'dw0024ry.dll");

            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                @"c:\MyProject",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion",
                @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
                "{AssemblyFolders}",
                "{HintPathFromItem}"
            };

            Execute(t);


            Assert.Equal(0, e.Warnings); // "No warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// Everett supported assembly names that had .dll at the end.
        /// </summary>
        [Fact]
        public void Regress366322_ReferencesWithFileExtensions()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A.dll")       // User really meant a fusion name here.
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\a.DlL")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Support for multiple framework directories.
        /// </summary>
        [Fact]
        public void Regress366814_MultipleFrameworksFolders()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\boguslocation", @"c:\MyLibraries" };
            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}",
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyLibraries\a.DlL")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// If the App.Config file has a bad .XML then handle it gracefully.
        /// (i.e. no exception is thrown from the task.
        /// </summary>
        [Fact]
        public void Regress271273_BogusAppConfig()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"C:\MyComponents\v1.0\UnifyMe.dll")
                    };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
            (
                "        <dependentAssembly\n" +        // Intentionally didn't close this XML tag.
                "        </dependentAssembly>\n"
            );

            try
            {
                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                Execute(t);
            }
            finally
            {
                // Cleanup.
                File.Delete(appConfigFile);
            }
        }

        /// <summary>
        /// The user might pass in a HintPath that has a trailing slash. Need to not crash.
        /// 
        /// </summary>
        [Fact]
        public void Regress354669_HintPathWithTrailingSlash()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress354669\");


            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                @"c:\MyProject",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion",
                @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
                "{AssemblyFolders}",
                "{HintPathFromItem}"
            };
            Execute(t);
        }

        /// <summary>
        /// The user might pass in a HintPath that has a trailing slash. Need to not crash.
        /// 
        ///    Assembly A
        ///     References: C, version 2
        ///
        ///    Assembly B
        ///     References: C, version 1
        ///
        /// There is an App.Config file that redirects all versions of C to V2.
        /// Assemblies A and B are both located via their HintPath.
        /// 
        /// </summary>        
        [Fact]
        public void Regress339786_CrossVersionsWithAppConfig()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("A"),
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress339786\FolderB\B.dll");
            t.Assemblies[1].SetMetadata("HintPath", @"C:\Regress339786\FolderA\A.dll");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
            (
            "        <dependentAssembly>\n" +
            "            <assemblyIdentity name='C' PublicKeyToken='null' culture='neutral' />\n" +
            "            <bindingRedirect oldVersion='0.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
            "        </dependentAssembly>\n"
            );
            t.AppConfigFile = appConfigFile;

            try
            {
                t.SearchPaths = new string[]
                {
                    "{HintPathFromItem}"
                };
                Execute(t);
            }
            finally
            {
                File.Delete(appConfigFile);
            }

            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
        }

        /// <summary>
        /// An older LKG of the CLR could throw a FileLoadException if it doesn't recognize
        /// the assembly. We need to support this for dogfooding purposes.
        /// </summary>
        [Fact]
        public void Regress_DogfoodCLRThrowsFileLoadException()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("DependsMyFileLoadExceptionAssembly")
            };

            t.SearchPaths = new string[]
            {
                @"c:\OldClrBug"
            };
            Execute(t);
        }


        /// <summary>
        /// There was a bug in which any file mentioned in the InstalledAssemblyTables was automatically
        /// considered to be a file present in the framework directory. This assumption was originally true, 
        /// but became false when Crystal Reports started putting their assemblies in this table.
        /// </summary>
        [Fact]
        public void Regress407623_RedistListDoesNotImplyPresenceInFrameworks()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("CrystalReportsAssembly")
            };

            t.Assemblies[0].SetMetadata("SpecificVersion", "false");    // Important to this bug.
            t.TargetFrameworkDirectories = new string[] { @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx" };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}",        // Assembly is not here.
                @"c:\Regress407623"                    // Assembly is here.
            };

            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='CrystalReports-Redist' >" +
                        "<File AssemblyName='CrystalReportsAssembly' Version='2.0.3600.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='2.0.40824.0' InGAC='true' />" +
                    "</FileList >"
                );

                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistFile) };

                Execute(t);
            }
            finally
            {
                File.Delete(redistFile);
            }

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Regress407623\CrystalReportsAssembly.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// If an invalid file name is passed to InstalledAssemblyTables we expect a warning even if no other redist lists are passed.
        /// </summary>
        [Fact]
        public void InvalidCharsInInstalledAssemblyTable()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("SomeAssembly")
            };


            t.TargetFrameworkDirectories = new string[] { @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx" };
            t.InstalledAssemblyTables = new TaskItem[] { new TaskItem("asdfasdfasjr390rjfiogatg~~!@@##$%$%%^&**()") };

            Execute(t);
            e.AssertLogContains("MSB3250");
        }

        /// <summary>
        /// Here's how you get into this situation:
        /// 
        /// App
        ///   References - Microsoft.Build.Engine
        ///     Hintpath = C:\Regress435487\microsoft.build.engine.dll
        /// 
        ///    And, the following conditions.
        ///     microsoft.build.engine.dll has the redistlist InGac=true flag set.
        /// 
        /// Expected result:
        /// * For the assembly to be CopyLocal=true
        /// 
        /// </summary>
        [Fact]
        public void Regress435487_FxFileResolvedByHintPathShouldByCopyLocal()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build.Engine")
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress435487\microsoft.build.engine.dll");


            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}",
                @"{TargetFrameworkDirectory}"
            };
            t.TargetFrameworkDirectories = new string[] { @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx" };

            string redistFile = FileUtilities.GetTemporaryFile();

            try
            {
                File.Delete(redistFile);

                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='MyFancy-Redist' >" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='0.0.0.0' PublicKeyToken='null' Culture='Neutral' FileVersion='2.0.40824.0' InGAC='true' />" +
                    "</FileList >"
                );

                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistFile) };

                Execute(t);
            }
            finally
            {
                File.Delete(redistFile);
            }

            Assert.Equal(t.ResolvedFiles[0].GetMetadata("CopyLocal"), "true"); // "Expected CopyLocal==true."
        }

        /// <summary>
        /// Verify when doing partial name matching with the assembly name that we also correctly do the partial name matching when trying to find
        /// assemblies from the redist list. 
        /// </summary>
        [Fact]
        public void PartialNameMatchingFromRedist()
        {
            string redistFile = FileUtilities.GetTemporaryFile();

            try
            {
                File.Delete(redistFile);

                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='MyFancy-Redist' >" +
                        // Simple name match where everything is the same except for version
                        "<File AssemblyName='A' Version='1.0.0.0' PublicKeyToken='a5d015c7d5a0b012' Culture='de-DE' FileVersion='2.0.40824.0' InGAC='true' />" +
                        "<File AssemblyName='A' Version='2.0.0.0' PublicKeyToken='a5d015c7d5a0b012' Culture='neutral' FileVersion='2.0.40824.0' InGAC='true' />" +
                        "<File AssemblyName='A' Version='3.0.0.0' PublicKeyToken='null' Culture='de-DE' FileVersion='2.0.40824.0' InGAC='true' />" +
                    "</FileList >"
                );

                AssemblyName v1 = new AssemblyName("A, Culture=de-DE, PublicKeyToken=a5d015c7d5a0b012, Version=1.0.0.0");
                AssemblyName v2 = new AssemblyName("A, Culture=Neutral, PublicKeyToken=a5d015c7d5a0b012, Version=2.0.0.0");
                AssemblyName v3 = new AssemblyName("A, Culture=de-DE, PublicKeyToken=null, Version=3.0.0.0");

                AssemblyNameExtension Av1 = new AssemblyNameExtension(v1);
                AssemblyNameExtension Av2 = new AssemblyNameExtension(v2);
                AssemblyNameExtension Av3 = new AssemblyNameExtension(v3);


                AssemblyTableInfo assemblyTableInfo = new AssemblyTableInfo(redistFile, "MyFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { assemblyTableInfo });
                InstalledAssemblies installedAssemblies = new InstalledAssemblies(redistList);

                AssemblyNameExtension assemblyName = new AssemblyNameExtension("A");
                AssemblyNameExtension foundAssemblyName = FrameworkPathResolver.GetHighestVersionInRedist(installedAssemblies, assemblyName);
                Assert.True(foundAssemblyName.Equals(Av3));

                assemblyName = new AssemblyNameExtension("A, PublicKeyToken=a5d015c7d5a0b012");
                foundAssemblyName = FrameworkPathResolver.GetHighestVersionInRedist(installedAssemblies, assemblyName);
                Assert.True(foundAssemblyName.Equals(Av2));

                assemblyName = new AssemblyNameExtension("A, Culture=de-DE");
                foundAssemblyName = FrameworkPathResolver.GetHighestVersionInRedist(installedAssemblies, assemblyName);
                Assert.True(foundAssemblyName.Equals(Av3));

                assemblyName = new AssemblyNameExtension("A, PublicKeyToken=a5d015c7d5a0b012, Culture=de-DE");
                foundAssemblyName = FrameworkPathResolver.GetHighestVersionInRedist(installedAssemblies, assemblyName);
                Assert.True(foundAssemblyName.Equals(Av1));

                assemblyName = new AssemblyNameExtension("A, Version=17.0.0.0, PublicKeyToken=a5d015c7d5a0b012, Culture=de-DE");
                foundAssemblyName = FrameworkPathResolver.GetHighestVersionInRedist(installedAssemblies, assemblyName);
                Assert.True(foundAssemblyName.Equals(assemblyName));
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        [Fact]
        public void Regress46599_BogusInGACValueForAssemblyInRedistList()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build.Engine"),
                new TaskItem("System.Xml")
            };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}"
            };
            t.TargetFrameworkDirectories = new string[] { @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx" };

            FileExists cachedFileExists = fileExists;
            GetAssemblyName cachedGetAssemblyName = getAssemblyName;
            string redistFile = CreateGenericRedistList();

            bool success = false;
            try
            {
                fileExists = new FileExists(delegate (string path)
                {
                    if (String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\Microsoft.Build.Engine.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\System.Xml.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return false;
                });
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistFile) };

                success = Execute(t);
            }
            finally
            {
                fileExists = cachedFileExists;
                getAssemblyName = cachedGetAssemblyName;
                File.Delete(redistFile);
            }

            Assert.True(success); // "Expected no errors."
            Assert.Equal(2, t.ResolvedFiles.Length); // "Expected two resolved assemblies."
        }

        [Fact]
        public void VerifyFrameworkFileMetadataFiles()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                // In framework directory and redist, should have metadata
                new TaskItem("Microsoft.Build.Engine"),
                new TaskItem("System.Xml"),
                // In framework directory, should have metadata
                new TaskItem("B"),
                // Not in framework directory but in redist, should have metadata
                new TaskItem("C"),
                // Not in framework directory and not in redist, should not have metadata
                new TaskItem("D")
            };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}",
                @"c:\Somewhere\"
            };
            t.TargetFrameworkDirectories = new string[] { @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx" };

            FileExists cachedFileExists = fileExists;
            GetAssemblyName cachedGetAssemblyName = getAssemblyName;

            // Create a redist list which will contains both of the assemblies to search for
            string redistListContents =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                         "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                         "<File AssemblyName='C' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                    "</FileList >";

            string redistFile = FileUtilities.GetTemporaryFile();
            File.WriteAllText(redistFile, redistListContents);

            bool success = false;
            try
            {
                fileExists = new FileExists(delegate (string path)
                {
                    if (String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\Microsoft.Build.Engine.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\System.Xml.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\B.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, @"c:\somewhere\c.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, @"c:\somewhere\d.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return false;
                });

                getAssemblyName = new GetAssemblyName(delegate (string path)
                {
                    if (String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\B.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    }

                    if (String.Equals(path, @"c:\somewhere\d.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        return new AssemblyNameExtension("D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    }

                    return null;
                });
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistFile) };

                success = Execute(t);
            }
            finally
            {
                fileExists = cachedFileExists;
                getAssemblyName = cachedGetAssemblyName;
                File.Delete(redistFile);
            }

            Assert.True(success); // "Expected no errors."
            Assert.Equal(5, t.ResolvedFiles.Length); // "Expected two resolved assemblies."
            Assert.True(t.ResolvedFiles.Where(Item => Item.GetMetadata("OriginalItemSpec").Equals("Microsoft.Build.Engine", StringComparison.OrdinalIgnoreCase)).First().GetMetadata("FrameworkFile").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles.Where(Item => Item.GetMetadata("OriginalItemSpec").Equals("System.Xml", StringComparison.OrdinalIgnoreCase)).First().GetMetadata("FrameworkFile").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles.Where(Item => Item.GetMetadata("OriginalItemSpec").Equals("B", StringComparison.OrdinalIgnoreCase)).First().GetMetadata("FrameworkFile").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedFiles.Where(Item => Item.GetMetadata("OriginalItemSpec").Equals("C", StringComparison.OrdinalIgnoreCase)).First().GetMetadata("FrameworkFile").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.ResolvedFiles.Where(Item => Item.GetMetadata("OriginalItemSpec").Equals("D", StringComparison.OrdinalIgnoreCase)).First().GetMetadata("FrameworkFile").Length);
        }

        /// <summary>
        /// Create a redist file which is used by many different tests
        /// </summary>
        /// <returns>Path to the redist list</returns>
        private static string CreateGenericRedistList()
        {
            // Create a redist list which will contains both of the assemblies to search for
            string redistListContents =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                    "</FileList >";

            string tempFile = FileUtilities.GetTemporaryFile();
            File.WriteAllText(tempFile, redistListContents);
            return tempFile;
        }


        [Fact]
        public void GetRedistListPathsFromDisk_ThrowsArgumentNullException()
        {
            bool caughtArgumentNullException = false;

            try
            {
                RedistList.GetRedistListPathsFromDisk(null);
            }
            catch (ArgumentNullException)
            {
                caughtArgumentNullException = true;
            }

            Assert.True(caughtArgumentNullException); // "Public method RedistList.GetRedistListPathsFromDisk should throw ArgumentNullException when its argument is null!"
        }

        /// <summary>
        /// Test the case where the redist list is empty and we pass in an empty set of white lists
        /// We should return null as there is no point generating a white list if there is nothing to subtract from.
        /// ResolveAssemblyReference will see this as null and log a warning indicating no redist assemblies were found therefore no black list could be 
        /// generated
        /// </summary>
        [Fact]
        public void RedistListGenerateBlackListEmptyAssemblyInfoNoRedistAssemblies()
        {
            RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[0]);
            List<Exception> whiteListErrors = new List<Exception>();
            List<string> whiteListErrorFileNames = new List<string>();
            Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[0], whiteListErrors, whiteListErrorFileNames);
            Assert.Null(blackList); // "Should return null if the AssemblyTableInfo is empty and the redist list is empty"
        }

        /// <summary>
        /// Verify that when we go to generate a black list but there were no subset list files passed in that we get NO black list genreated as there is nothing to subtract.
        /// Nothing meaning, we dont have any matching subset list files to say there are no good files.
        /// </summary>
        [Fact]
        public void RedistListGenerateBlackListEmptyAssemblyInfoWithRedistAssemblies()
        {
            string redistFile = CreateGenericRedistList();
            try
            {
                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[0], whiteListErrors, whiteListErrorFileNames);


                // Since there were no white list expect the black list to return null
                Assert.Equal(0, blackList.Count); // "Expected to have no assemblies in the black list"
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Test the case where the subset lists cannot be read. The expectation is that the black list will be empty as we have no proper white lists to compare it to.
        /// </summary>
        [Fact]
        public void RedistListGenerateBlackListNotFoundSubsetFiles()
        {
            string redistFile = CreateGenericRedistList();
            try
            {
                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();

                Hashtable blackList = redistList.GenerateBlackList(
                                                                   new AssemblyTableInfo[]
                                                                                         {
                                                                                           new AssemblyTableInfo("c:\\RandomDirectory.xml", "TargetFrameworkDirectory"),
                                                                                           new AssemblyTableInfo("c:\\AnotherRandomDirectory.xml", "TargetFrameworkDirectory")
                                                                                          },
                                                                                          whiteListErrors,
                                                                                          whiteListErrorFileNames
                                                                   );

                // Since there were no white list expect the black list to return null
                Assert.Equal(0, blackList.Count); // "Expected to have no assemblies in the black list"
                Assert.Equal(2, whiteListErrors.Count); // "Expected there to be two errors in the whiteListErrors, one for each missing file"
                Assert.Equal(2, whiteListErrorFileNames.Count); // "Expected there to be two errors in the whiteListErrorFileNames, one for each missing file"
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Test the case where there is random goo in the subsetList file. Expect the file to not be read in and a warning indicating the file was skipped due to a read error. 
        /// This should also cause the white list to be empty as the badly formatted file was the only whitelist subset file.
        /// </summary>
        [Fact]
        public void RedistListGenerateBlackListGarbageSubsetListFiles()
        {
            string redistFile = CreateGenericRedistList();
            string garbageSubsetFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.WriteAllText
                (
                    garbageSubsetFile,
                    "RandomGarbage, i am a bad file with random goo rather than anything important"
                 );

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(garbageSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFileNames);

                Assert.Equal(0, blackList.Count); // "Expected to have no assemblies in the black list"
                Assert.Equal(1, whiteListErrors.Count); // "Expected there to be an error in the whiteListErrors"
                Assert.Equal(1, whiteListErrorFileNames.Count); // "Expected there to be an error in the whiteListErrorFileNames"
                Assert.False(((Exception)whiteListErrors[0]).Message.Contains("MSB3257")); // "Expect to not have the null redist warning"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(garbageSubsetFile);
            }
        }

        /// <summary>
        /// Inputs:
        ///     Redist list which has entries and has a redist name
        ///     Subset list which has no redist name but has entries
        /// 
        /// Expected:
        ///     Expect a warning that a redist list or subset list has no redist name. 
        ///     There should be no black list generated as no sub set lists were read in.
        /// 
        /// Rational:
        ///     If we have no redist name to compare to the redist list redist name we cannot subtract the lists correctly.
        /// </summary>
        [Fact]
        public void RedistListNoSubsetListName()
        {
            string redistFile = CreateGenericRedistList();
            string subsetFile = FileUtilities.GetTemporaryFile();
            try
            {
                string subsetListContents =
                   "<FileList>" +
                       "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                   "</FileList >";
                File.WriteAllText(subsetFile, subsetListContents);



                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(subsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFileNames);

                // If the names do not match then i expect there to be no black list items
                Assert.Equal(0, blackList.Count); // "Expected to have no assembly in the black list"
                Assert.Equal(1, whiteListErrors.Count); // "Expected there to be one error in the whiteListErrors"
                Assert.Equal(1, whiteListErrorFileNames.Count); // "Expected there to be one error in the whiteListErrorFileNames"
                string message = ResourceUtilities.FormatResourceString("ResolveAssemblyReference.NoSubSetRedistListName", subsetFile);
                Assert.True(((Exception)whiteListErrors[0]).Message.Contains(message)); // "Expected assertion to contain correct error code"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(subsetFile);
            }
        }

        /// <summary>
        /// Inputs:
        ///     Redist list which has entries but no redist name
        ///     Subset list which has a redist name and entries
        /// 
        /// Expected:
        ///     Expect no black list to be generated and no warnigns to be emitted
        ///     
        /// Rational:
        ///     Since the redist list name is null or empty we have no way of matching any subset list up to it.
        /// </summary>
        [Fact]
        public void RedistListNullkRedistListName()
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            string subsetFile = FileUtilities.GetTemporaryFile();
            try
            {
                string subsetListContents =
                   "<FileList Redist='MyRedistListFile'>" +
                       "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                   "</FileList >";
                File.WriteAllText(subsetFile, subsetListContents);

                string redistListContents =
                  "<FileList>" +
                      "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                       "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "</FileList >";
                File.WriteAllText(redistFile, redistListContents);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(subsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFileNames);

                // If the names do not match then i expect there to be no black list items
                Assert.Equal(0, blackList.Count); // "Expected to have no assembly in the black list"
                Assert.Equal(0, whiteListErrors.Count); // "Expected there to be no errors in the whiteListErrors"
                Assert.Equal(0, whiteListErrorFileNames.Count); // "Expected there to be no errors in the whiteListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(subsetFile);
            }
        }

        /// <summary>
        /// Inputs:
        ///     Redist list which has entries and has a redist name
        ///     Subset list which has entries but has a different redist name than the redist list
        /// 
        /// Expected:
        ///     There should be no black list generated as no sub set lists with matching names were found.
        /// 
        /// Rational:
        ///     If the redist name does not match then that subset list should not be subtracted from the redist list. 
        ///     We only add assemblies to the black list if there is a corosponding white list even if it is empty to inform us what assemblies are good and which are not.
        /// </summary>
        [Fact]
        public void RedistListDifferentNameToSubSet()
        {
            string redistFile = CreateGenericRedistList();
            string subsetFile = FileUtilities.GetTemporaryFile();
            try
            {
                string subsetListContents =
                   "<FileList Redist='IAMREALLYREALLYDIFFERNT' >" +
                       "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                   "</FileList >";
                File.WriteAllText(subsetFile, subsetListContents);



                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(subsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFileNames);

                // If the names do not match then i expect there to be no black list items
                Assert.Equal(0, blackList.Count); // "Expected to have no assembly in the black list"
                Assert.Equal(0, whiteListErrors.Count); // "Expected there to be no error in the whiteListErrors"
                Assert.Equal(0, whiteListErrorFileNames.Count); // "Expected there to be no error in the whiteListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(subsetFile);
            }
        }

        /// <summary>
        /// Test the case where the subset list has the same name as the redist list but it has no entries In this case
        /// the black list should contain ALL redist list entries because there are no white list files to remove from the black list.
        /// </summary>
        [Fact]
        public void RedistListEmptySubsetMatchingName()
        {
            string redistFile = CreateGenericRedistList();
            string subsetFile = FileUtilities.GetTemporaryFile();
            try
            {
                string subsetListContents =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                   "</FileList >";
                File.WriteAllText(subsetFile, subsetListContents);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(subsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFileNames);

                // If the names do not match then i expect there to be no black list items
                Assert.Equal(2, blackList.Count); // "Expected to have two assembly in the black list"
                Assert.Equal(0, whiteListErrors.Count); // "Expected there to be no error in the whiteListErrors"
                Assert.Equal(0, whiteListErrorFileNames.Count); // "Expected there to be no error in the whiteListErrorFileNames"

                ArrayList whiteListErrors2 = new ArrayList();
                ArrayList whiteListErrorFileNames2 = new ArrayList();
                Hashtable blackList2 = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFileNames);
                Assert.Same(blackList, blackList2);
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(subsetFile);
            }
        }

        /// <summary>
        /// Test the case where, no redist assemblies are read in. 
        /// In this case no blacklist can be generated. 
        /// We should get a warning informing us that we could not create a black list.
        /// </summary>
        [Fact]
        public void RedistListNoAssembliesinRedistList()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine.dll");
            string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");
            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build.Engine"),
                new TaskItem("System.Xml")
            };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}"
            };

            string redistListPath = FileUtilities.GetTemporaryFile();
            string subsetListPath = FileUtilities.GetTemporaryFile();
            File.WriteAllText(subsetListPath, _xmlOnlySubset);
            try
            {
                File.WriteAllText
                (
                    redistListPath,
                   "RANDOMBOOOOOGOOGOOG"
                );

                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.InstalledAssemblySubsetTables = new ITaskItem[] { new TaskItem(subsetListPath) };

                Execute(t);
                MockEngine engine = (MockEngine)t.BuildEngine;
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.NoRedistAssembliesToGenerateExclusionList"));
            }
            finally
            {
                File.Delete(redistListPath);
                File.Delete(subsetListPath);
            }
        }

        /// <summary>
        /// Test the case where the subset list is a subset of the redist list. Make sure that 
        /// even though there are two files in the redist list that only one shows up in the black list.
        /// </summary>
        [Fact]
        public void RedistListGenerateBlackListGoodListsSubsetIsSubsetOfRedist()
        {
            string redistFile = CreateGenericRedistList(); ;
            string goodSubsetFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.WriteAllText(goodSubsetFile, _engineOnlySubset);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFileNames);

                Assert.Equal(1, blackList.Count); // "Expected to have one assembly in the black list"
                Assert.True(blackList.ContainsKey("System.Xml, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b03f5f7f11d50a3a")); // "Expected System.xml to be in the black list"
                Assert.Equal(0, whiteListErrors.Count); // "Expected there to be no error in the whiteListErrors"
                Assert.Equal(0, whiteListErrorFileNames.Count); // "Expected there to be no error in the whiteListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Test the case where we generate a black list based on a set of subset file paths, and then ask for 
        /// another black list using the same file paths. We expect to get the exact same Hashtable out
        /// as it should be pulled from the cache.
        /// </summary>
        [Fact]
        public void RedistListGenerateBlackListVerifyBlackListCache()
        {
            string redistFile = CreateGenericRedistList();
            string goodSubsetFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.WriteAllText(goodSubsetFile, _engineOnlySubset);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFileNames);

                // Since there were no white list expect the black list to return null
                Assert.Equal(1, blackList.Count); // "Expected to have one assembly in the black list"
                Assert.True(blackList.ContainsKey("System.Xml, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b03f5f7f11d50a3a")); // "Expected System.xml to be in the black list"
                Assert.Equal(0, whiteListErrors.Count); // "Expected there to be no error in the whiteListErrors"
                Assert.Equal(0, whiteListErrorFileNames.Count); // "Expected there to be no error in the whiteListErrorFileNames"

                List<Exception> whiteListErrors2 = new List<Exception>();
                List<string> whiteListErrorFileNames2 = new List<string>();
                Hashtable blackList2 = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors2, whiteListErrorFileNames2);
                Assert.Same(blackList, blackList2);
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Test the case where the white list and the redist list are identical
        /// In this case the black list should be empty.
        /// 
        /// We are also in a way testing the combining of subset files as we read in one assembly from two 
        /// different subset lists while the redist list already contains both assemblies.
        /// </summary>
        [Fact]
        public void RedistListGenerateBlackListGoodListsSubsetIsSameAsRedistList()
        {
            string redistFile = CreateGenericRedistList();
            string goodSubsetFile = FileUtilities.GetTemporaryFile();
            string goodSubsetFile2 = FileUtilities.GetTemporaryFile();
            try
            {
                File.WriteAllText(goodSubsetFile, _engineOnlySubset);
                File.WriteAllText(goodSubsetFile2, _xmlOnlySubset);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo2 = new AssemblyTableInfo(goodSubsetFile2, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });

                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo, subsetListInfo2 }, whiteListErrors, whiteListErrorFileNames);
                // Since there were no white list expect the black list to return null
                Assert.Equal(0, blackList.Count); // "Expected to have no assemblies in the black list"
                Assert.Equal(0, whiteListErrors.Count); // "Expected there to be no error in the whiteListErrors"
                Assert.Equal(0, whiteListErrorFileNames.Count); // "Expected there to be no error in the whiteListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Test the case where the white list is a superset of the redist list. 
        /// This means there are more assemblies in the white list than in the black list. 
        /// 
        /// The black list should be empty.
        /// </summary>
        [Fact]
        public void RedistListGenerateBlackListGoodListsSubsetIsSuperSet()
        {
            string redistFile = CreateGenericRedistList();
            string goodSubsetFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.WriteAllText
                (
                    goodSubsetFile,
                  "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                       "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' />" +
                       "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                       "<File AssemblyName='System.Data' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "</FileList >"
                 );

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFileNames);

                // Since there were no white list expect the black list to return null
                Assert.Equal(0, blackList.Count); // "Expected to have no assemblies in the black list"
                Assert.Equal(0, whiteListErrors.Count); // "Expected there to be no error in the whiteListErrors"
                Assert.Equal(0, whiteListErrorFileNames.Count); // "Expected there to be no error in the whiteListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Check to see if comparing the assemblies in the redist list to the ones in the subset 
        /// list are case sensitive or not, they should not be case sensitive.
        /// </summary>
        [Fact]
        public void RedistListGenerateBlackListGoodListsCheckCaseInsensitive()
        {
            string redistFile = CreateGenericRedistList();
            string goodSubsetFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.WriteAllText(goodSubsetFile, _engineAndXmlSubset.ToUpperInvariant());

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFileNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFileNames);

                // Since there were no white list expect the black list to return null
                Assert.Equal(0, blackList.Count); // "Expected to have no assemblies in the black list"
                Assert.Equal(0, whiteListErrors.Count); // "Expected there to be no error in the whiteListErrors"
                Assert.Equal(0, whiteListErrorFileNames.Count); // "Expected there to be no error in the whiteListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Verify that when we go to generate a black list but there were no subset list files passed in that we get NO black list genreated as there is nothing to subtract.
        /// Nothing meaning, we dont have any matching subset list files to say there are no good files.
        /// </summary>
        [Fact]
        public void RedistListGenerateBlackListGoodListsMultipleIdenticalAssembliesInRedistList()
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            string goodSubsetFile = FileUtilities.GetTemporaryFile();
            try
            {
                // Create a redist list which will contains both of the assemblies to search for
                string redistListContents =
                        "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                             "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                             "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "</FileList >";

                File.WriteAllText(redistFile, redistListContents);
                File.WriteAllText(goodSubsetFile, _engineAndXmlSubset);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> whiteListErrors = new List<Exception>();
                List<string> whiteListErrorFilesNames = new List<string>();
                Hashtable blackList = redistList.GenerateBlackList(new AssemblyTableInfo[] { subsetListInfo }, whiteListErrors, whiteListErrorFilesNames);

                // Since there were no white list expect the black list to return null
                Assert.Equal(0, blackList.Count); // "Expected to have no assemblies in the black list"
                Assert.Equal(0, whiteListErrors.Count); // "Expected there to be no error in the whiteListErrors"
                Assert.Equal(0, whiteListErrorFilesNames.Count); // "Expected there to be no error in the whiteListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Test the case where the framework directory is passed in as null
        /// </summary>
        [Fact]
        public void SubsetListFinderNullFrameworkDirectory()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                SubsetListFinder finder = new SubsetListFinder(new string[0]);
                finder.GetSubsetListPathsFromDisk(null);
            }
           );
        }
        /// <summary>
        /// Test the case where the subsetsToSearchFor are passed in as null
        /// </summary>
        [Fact]
        public void SubsetListFinderNullSubsetToSearchFor()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                SubsetListFinder finder = new SubsetListFinder(null);
            }
           );
        }
        /// <summary>
        /// Test the case where the subsetsToSearchFor are an empty array
        /// </summary>
        [Fact]
        public void SubsetListFinderEmptySubsetToSearchFor()
        {
            SubsetListFinder finder = new SubsetListFinder(new string[0]);
            string[] returnArray = finder.GetSubsetListPathsFromDisk("FrameworkDirectory");
            Assert.Equal(0, returnArray.Length); // "Expected the array returned to be 0 lengh"
        }


        /// <summary>
        /// Verify that the method will not crash if there are empty string array elements, and that when we call the 
        /// method twice with the same set of SubsetToSearchFor and TargetFrameworkDirectory that we get the exact same array back.
        /// </summary>
        [Fact]
        public void SubsetListFinderVerifyEmptyInSubsetsToSearchForAndCaching()
        {
            // Verify the program will not crach when an empty string is passed in and that when we call the method twice that we get the 
            // exact same array of strings back.
            SubsetListFinder finder = new SubsetListFinder(new string[] { "Clent", string.Empty, "Bar" });
            string[] returnArray = finder.GetSubsetListPathsFromDisk("FrameworkDirectory");
            string[] returnArray2 = finder.GetSubsetListPathsFromDisk("FrameworkDirectory");

            Assert.True(Object.ReferenceEquals(returnArray, returnArray2)); // "Expected the string arrays to be the exact same reference"
            // Verify that if i call the method again with a different target framework directory that I get a different array back
            string[] returnArray3 = finder.GetSubsetListPathsFromDisk("FrameworkDirectory2");
            Assert.False(Object.ReferenceEquals(returnArray2, returnArray3)); // "Expected the string arrays to not be the exact same reference"
        }

        /// <summary>
        /// Verify when we have valid subset files and their names are in the subsets to search for that we correctly find the files
        /// </summary>
        [Fact]
        public void SubsetListFinderSubsetExists()
        {
            string frameworkDirectory = Path.Combine(ObjectModelHelpers.TempProjectDir, "SubsetListsTestExists");
            string subsetDirectory = Path.Combine(frameworkDirectory, SubsetListFinder.SubsetListFolder);
            string clientXml = Path.Combine(subsetDirectory, "Client.xml");
            string fooXml = Path.Combine(subsetDirectory, "Foo.xml");

            try
            {
                Directory.CreateDirectory(subsetDirectory);
                File.WriteAllText(clientXml, "Random File Contents");
                File.WriteAllText(fooXml, "Random File Contents");
                SubsetListFinder finder = new SubsetListFinder(new string[] { "Client", "Foo" });
                string[] returnArray = finder.GetSubsetListPathsFromDisk(frameworkDirectory);
                Assert.True(returnArray[0].Contains("Client.xml")); // "Expected first element to contain Client.xml"
                Assert.True(returnArray[1].Contains("Foo.xml")); // "Expected first element to contain Foo.xml"
                Assert.Equal(2, returnArray.Length); // "Expected there to be two elements in the array"
            }
            finally
            {
                Directory.Delete(frameworkDirectory, true);
            }
        }

        /// <summary>
        /// Verify that if there are files of the correct name but of the wrong extension that they are not found.
        /// </summary>
        [Fact]
        public void SubsetListFinderNullSubsetExistsButNotXml()
        {
            string frameworkDirectory = Path.Combine(ObjectModelHelpers.TempProjectDir, "SubsetListsTestExistsNotXml");
            string subsetDirectory = Path.Combine(frameworkDirectory, SubsetListFinder.SubsetListFolder);
            string clientXml = Path.Combine(subsetDirectory, "Clent.Notxml");
            string fooXml = Path.Combine(subsetDirectory, "Foo.Notxml");

            try
            {
                Directory.CreateDirectory(subsetDirectory);
                File.WriteAllText(clientXml, "Random File Contents");
                File.WriteAllText(fooXml, "Random File Contents");
                SubsetListFinder finder = new SubsetListFinder(new string[] { "Client", "Foo" });
                string[] returnArray = finder.GetSubsetListPathsFromDisk(frameworkDirectory);
                Assert.Equal(0, returnArray.Length); // "Expected there to be two elements in the array"
            }
            finally
            {
                Directory.Delete(frameworkDirectory, true);
            }
        }

        [Fact]
        public void IgnoreDefaultInstalledAssemblyTables()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build.Engine"),
                new TaskItem("System.Xml")
            };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}"
            };
            t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };

            string implicitRedistListContents =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                    "</FileList >";
            string implicitRedistListPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("v3.5\\RedistList\\ImplicitList.xml", implicitRedistListContents);
            string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine");

            string explicitRedistListContents =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                    "</FileList >";
            string explicitRedistListPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("v3.5\\RedistList\\ExplicitList.xml", explicitRedistListContents);
            string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");

            t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(explicitRedistListPath) };

            // Only the explicitly specified redist list should be used
            t.IgnoreDefaultInstalledAssemblyTables = true;

            FileExists cachedFileExists = fileExists;
            GetAssemblyName cachedGetAssemblyName = getAssemblyName;

            fileExists = new FileExists(delegate (string path)
            {
                if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            });

            getAssemblyName = new GetAssemblyName(delegate (string path)
            {
                if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }
                else if (String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }

                return null;
            });

            bool success;
            try
            {
                success = Execute(t);
            }
            finally
            {
                fileExists = cachedFileExists;
                getAssemblyName = cachedGetAssemblyName;
            }

            Assert.True(success); // "Expected no errors."
            Assert.Equal(1, t.ResolvedFiles.Length); // "Expected one resolved assembly."
            Assert.True(t.ResolvedFiles[0].ItemSpec.Contains("System.Xml")); // "Expected System.Xml to resolve."
        }

        /// <summary>
        /// A null black list should be the same as an empty one.
        /// </summary>
        [Fact]
        public void ReferenceTableNullBlackList()
        {
            TaskLoggingHelper log = new TaskLoggingHelper(new ResolveAssemblyReference());
            ReferenceTable referenceTable = MakeEmptyReferenceTable(log);
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;

            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            table.Add(engineAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));
            table.Add(xmlAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));

            referenceTable.MarkReferencesForExclusion(null);
            referenceTable.RemoveReferencesMarkedForExclusion(false, String.Empty);
            Dictionary<AssemblyNameExtension, Reference> table2 = referenceTable.References;
            Assert.False(Object.ReferenceEquals(table, table2)); // "Expected hashtable to be a different instance"
            Assert.Equal(2, table2.Count); // "Expected there to be two elements in the hashtable"
            Assert.True(table2.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"
            Assert.True(table2.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
        }

        /// <summary>
        /// Test the case where the blacklist is empty.
        /// </summary>
        [Fact]
        public void ReferenceTableEmptyBlackList()
        {
            TaskLoggingHelper log = new TaskLoggingHelper(new ResolveAssemblyReference());
            ReferenceTable referenceTable = MakeEmptyReferenceTable(log);
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;

            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            table.Add(engineAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));
            table.Add(xmlAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));

            referenceTable.MarkReferencesForExclusion(new Hashtable());
            referenceTable.RemoveReferencesMarkedForExclusion(false, String.Empty);
            Dictionary<AssemblyNameExtension, Reference> table2 = referenceTable.References;
            Assert.False(Object.ReferenceEquals(table, table2)); // "Expected hashtable to be a different instance"
            Assert.Equal(2, table2.Count); // "Expected there to be two elements in the hashtable"
            Assert.True(table2.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"
            Assert.True(table2.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
        }

        /// <summary>
        /// Verify the case where there are primary references in the reference table which are also in the black list
        /// </summary>
        [Fact]
        public void ReferenceTablePrimaryItemInBlackList()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveAssemblyReference rar = new ResolveAssemblyReference();
            rar.BuildEngine = mockEngine;

            ReferenceTable referenceTable = MakeEmptyReferenceTable(rar.Log);
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;

            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            reference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            table.Add(engineAssemblyName, reference);
            table.Add(xmlAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));

            Hashtable blackList = new Hashtable(StringComparer.OrdinalIgnoreCase);
            blackList[engineAssemblyName.FullName] = null;
            string[] targetFrameworks = new string[] { "Client", "Web" };
            string subSetName = ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null);

            referenceTable.MarkReferencesForExclusion(blackList);
            referenceTable.RemoveReferencesMarkedForExclusion(false, subSetName);

            Dictionary<AssemblyNameExtension, Reference> table2 = referenceTable.References;
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailedToResolveReferenceBecausePrimaryAssemblyInExclusionList", taskItem.ItemSpec, subSetName);
            Assert.False(Object.ReferenceEquals(table, table2)); // "Expected hashtable to be a different instance"
            Assert.Equal(1, table2.Count); // "Expected there to be one elements in the hashtable"
            Assert.False(table2.ContainsKey(engineAssemblyName)); // "Expected to not find the engineAssemblyName in the referenceList"
            Assert.True(table2.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
            mockEngine.AssertLogContains(warningMessage);
        }

        /// <summary>
        /// Verify the case where there are primary references in the reference table which are also in the black list
        /// </summary>
        [Fact]
        public void ReferenceTablePrimaryItemInBlackListSpecificVersionTrue()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveAssemblyReference rar = new ResolveAssemblyReference();
            rar.BuildEngine = mockEngine;

            ReferenceTable referenceTable = MakeEmptyReferenceTable(rar.Log);
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;

            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            taskItem.SetMetadata("SpecificVersion", "true");
            reference.MakePrimaryAssemblyReference(taskItem, true, ".dll");
            table.Add(engineAssemblyName, reference);
            table.Add(xmlAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));

            Hashtable blackList = new Hashtable(StringComparer.OrdinalIgnoreCase);
            blackList[engineAssemblyName.FullName] = null;
            string[] targetFrameworks = new string[] { "Client", "Web" };
            string subSetName = ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null);
            referenceTable.MarkReferencesForExclusion(blackList);
            referenceTable.RemoveReferencesMarkedForExclusion(false, subSetName);

            Dictionary<AssemblyNameExtension, Reference> table2 = referenceTable.References;
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailedToResolveReferenceBecausePrimaryAssemblyInExclusionList", taskItem.ItemSpec, subSetName);
            Assert.False(Object.ReferenceEquals(table, table2)); // "Expected hashtable to be a different instance"
            Assert.Equal(2, table2.Count); // "Expected there to be two elements in the hashtable"
            Assert.True(table2.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"
            Assert.True(table2.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
            mockEngine.AssertLogDoesntContain(warningMessage);
        }

        /// <summary>
        /// Verify the generation of the targetFrameworkSubSetName
        /// </summary>
        [Fact]
        public void TestGenerateFrameworkName()
        {
            string[] targetFrameworks = new string[] { "Client" };
            Assert.True(string.Equals("Client", ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null), StringComparison.OrdinalIgnoreCase));

            targetFrameworks = new string[] { "Client", "Framework" };
            Assert.True(string.Equals("Client, Framework", ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null), StringComparison.OrdinalIgnoreCase));

            targetFrameworks = new string[0];
            Assert.True(String.IsNullOrEmpty(ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null)));

            targetFrameworks = null;
            Assert.True(String.IsNullOrEmpty(ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null)));

            ITaskItem[] installedSubSetTable = new ITaskItem[] { new TaskItem("c:\\foo\\Client.xml") };
            Assert.True(string.Equals("Client", ResolveAssemblyReference.GenerateSubSetName(null, installedSubSetTable), StringComparison.OrdinalIgnoreCase));

            installedSubSetTable = new ITaskItem[] { new TaskItem("c:\\foo\\Client.xml"), new TaskItem("D:\\foo\\bar\\Framework.xml") };
            Assert.True(string.Equals("Client, Framework", ResolveAssemblyReference.GenerateSubSetName(null, installedSubSetTable), StringComparison.OrdinalIgnoreCase));

            installedSubSetTable = new ITaskItem[] { new TaskItem("c:\\foo\\Client.xml"), new TaskItem("D:\\foo\\bar\\Framework2\\"), new TaskItem("D:\\foo\\bar\\Framework"), new TaskItem("Nothing") };
            Assert.True(string.Equals("Client, Framework, Nothing", ResolveAssemblyReference.GenerateSubSetName(null, installedSubSetTable), StringComparison.OrdinalIgnoreCase));

            installedSubSetTable = new ITaskItem[0];
            Assert.True(String.IsNullOrEmpty(ResolveAssemblyReference.GenerateSubSetName(null, installedSubSetTable)));

            installedSubSetTable = null;
            Assert.True(String.IsNullOrEmpty(ResolveAssemblyReference.GenerateSubSetName(null, installedSubSetTable)));


            targetFrameworks = new string[] { "Client", "Framework" };
            installedSubSetTable = new ITaskItem[] { new TaskItem("c:\\foo\\Mouse.xml"), new TaskItem("D:\\foo\\bar\\Man.xml") };
            Assert.True(string.Equals("Client, Framework, Mouse, Man", ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, installedSubSetTable), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify the case where we just want to remove the references before conflict resolution and not print out the warning.
        /// </summary>
        [Fact]
        public void ReferenceTablePrimaryItemInBlackListRemoveOnlyNoWarn()
        {
            MockEngine mockEngine = new MockEngine();
            ResolveAssemblyReference rar = new ResolveAssemblyReference();
            rar.BuildEngine = mockEngine;

            ReferenceTable referenceTable = MakeEmptyReferenceTable(rar.Log);
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;

            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            reference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            table.Add(engineAssemblyName, reference);
            table.Add(xmlAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));

            Hashtable blackList = new Hashtable(StringComparer.OrdinalIgnoreCase);
            blackList[engineAssemblyName.FullName] = null;
            referenceTable.MarkReferencesForExclusion(blackList);
            referenceTable.RemoveReferencesMarkedForExclusion(true, String.Empty);

            Dictionary<AssemblyNameExtension, Reference> table2 = referenceTable.References;
            string subSetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailedToResolveReferenceBecausePrimaryAssemblyInExclusionList", taskItem.ItemSpec, subSetName);
            Assert.False(Object.ReferenceEquals(table, table2)); // "Expected hashtable to be a different instance"
            Assert.Equal(1, table2.Count); // "Expected there to be one elements in the hashtable"
            Assert.False(table2.ContainsKey(engineAssemblyName)); // "Expected to not find the engineAssemblyName in the referenceList"
            Assert.True(table2.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
            Assert.True(String.IsNullOrEmpty(mockEngine.Log));
        }

        /// <summary>
        /// Testing case  enginePrimary -> dataDependencyReference->sqlDependencyReference : sqlDependencyReference is in black list
        /// expect to see one dependency warning message
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInBlackList()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Hashtable blackList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            dataDependencyReference.FullPath = "FullPath";
            sqlDependencyReference.MakeDependentAssemblyReference(dataDependencyReference);
            sqlDependencyReference.AddError(new Exception("CouldNotResolveSQLDependency"));
            xmlPrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.MakeDependentAssemblyReference(enginePrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);
            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName }, out blackList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            VerifyReferenceTable(referenceTable, mockEngine, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, new string[] { warningMessage });
        }


        /// <summary>
        /// Testing case  enginePrimary -> dataDependencyReference->sqlDependencyReference  
        /// and enginePrimary->sqlDependencyReference: sqlDependencyReference is in black list  
        /// and systemxml->enginePrimary
        /// expect to see one dependency warning message
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInBlackList2()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Hashtable blackList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            ITaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            dataDependencyReference.FullPath = "FullPath";
            sqlDependencyReference.FullPath = "FullPath";
            xmlPrimaryReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(dataDependencyReference);
            xmlPrimaryReference.MakeDependentAssemblyReference(enginePrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);
            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName }, out blackList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            VerifyReferenceTable(referenceTable, mockEngine, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, new string[] { warningMessage });
        }

        /// <summary>
        /// Testing case  enginePrimary->XmlPrimary with XMLPrimary in the BL
        /// </summary>
        [Fact]
        public void ReferenceTablePrimaryToPrimaryDependencyWithOneInBlackList()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Hashtable blackList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            Reference xmlPrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);

            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";

            ITaskItem taskItem2 = new TaskItem("System.Xml");
            xmlPrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            // Make engine depend on xml primary when xml primary is a primary reference as well
            xmlPrimaryReference.AddSourceItems(enginePrimaryReference.GetSourceItems());
            xmlPrimaryReference.AddDependee(enginePrimaryReference);


            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, null, null, xmlAssemblyName, enginePrimaryReference, null, null, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { xmlAssemblyName }, out blackList);
            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, xmlAssemblyName.FullName, subsetName);
            string warningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailedToResolveReferenceBecausePrimaryAssemblyInExclusionList", taskItem2.ItemSpec, subsetName);
            mockEngine.AssertLogContains(warningMessage);
            mockEngine.AssertLogContains(warningMessage2);

            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.False(table.ContainsKey(xmlAssemblyName)); // "Expected to not find the xmlAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(engineAssemblyName)); // "Expected to not find the engineAssemblyName in the referenceList"
        }

        /// <summary>
        /// Testing case  enginePrimary->XmlPrimary->dataDependency with dataDependency in the BL
        /// </summary>
        [Fact]
        public void ReferenceTablePrimaryToPrimaryToDependencyWithOneInBlackList()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Hashtable blackList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            Reference xmlPrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            Reference dataDependencyReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);

            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";

            ITaskItem taskItem2 = new TaskItem("System.Xml");
            xmlPrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            // Make engine depend on xml primary when xml primary is a primary reference as well
            xmlPrimaryReference.AddSourceItems(enginePrimaryReference.GetSourceItems());
            xmlPrimaryReference.AddDependee(enginePrimaryReference);


            dataDependencyReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(xmlPrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, null, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, null, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { dataAssemblyName }, out blackList);


            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, dataAssemblyName.FullName, subsetName);
            string warningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem2.ItemSpec, dataAssemblyName.FullName, subsetName);
            mockEngine.AssertLogContains(warningMessage);
            mockEngine.AssertLogContains(warningMessage2);

            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.False(table.ContainsKey(xmlAssemblyName)); // "Expected to not find the xmlAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(engineAssemblyName)); // "Expected to not find the engineAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(dataAssemblyName)); // "Expected to not find the dataAssemblyName in the referenceList"
        }

        /// <summary>
        /// Testing case  enginePrimary -> dataDependencyReference->sqlDependencyReference 
        /// and xmlPrimary->sqlDependencyReference: sqlDependencyReference is in black list 
        /// expect to see one dependency warning message
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInBlackList3()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Hashtable blackList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            ITaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            ITaskItem taskItem2 = new TaskItem("System.Xml");
            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            dataDependencyReference.FullPath = "FullPath";
            xmlPrimaryReference.FullPath = "FullPath";
            sqlDependencyReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(xmlPrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(dataDependencyReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName }, out blackList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            string warningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem2.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            VerifyReferenceTable(referenceTable, mockEngine, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, new string[] { warningMessage, warningMessage2 });
        }


        /// <summary>
        /// Testing case  enginePrimary -> dataDependencyReference->sqlDependencyReference 
        /// and xmlPrimary->dataDependencyReference: sqlDependencyReference is in black list
        /// expect to see one dependency warning message
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInBlackList4()
        {
            ReferenceTable referenceTable = new ReferenceTable(null, false, false, false, false, new string[0], null, null, null, null, null, null, SystemProcessorArchitecture.None, fileExists, null, null, null, null, null, null, null, null, null, new Version("4.0"), null, null, null, true, false, null, null, false, null, WarnOrErrorOnTargetArchitectureMismatchBehavior.None, false, false);
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Hashtable blackList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            ITaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            ITaskItem taskItem2 = new TaskItem("System.Xml");
            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.FullPath = "FullPath";
            dataDependencyReference.FullPath = "FullPath";
            sqlDependencyReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            dataDependencyReference.MakeDependentAssemblyReference(xmlPrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(dataDependencyReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName }, out blackList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            string warningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem2.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            VerifyReferenceTable(referenceTable, mockEngine, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, new string[] { warningMessage, warningMessage2 });
        }

        /// <summary>
        /// Testing case  enginePrimary -> dataDependencyReference->sqlDependencyReference 
        /// enginePrimary -> dataDependencyReference
        /// xmlPrimaryReference ->DataDependency
        /// dataDependencyReference and sqlDependencyReference are in black list
        /// expect to see two dependency warning messages in the enginePrimaryCase and one in the xmlPrimarycase
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInBlackList5()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Hashtable blackList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            ITaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            ITaskItem taskItem2 = new TaskItem("System.Xml");
            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.FullPath = "FullPath";
            dataDependencyReference.FullPath = "FullPath";
            sqlDependencyReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            dataDependencyReference.MakeDependentAssemblyReference(xmlPrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName, dataAssemblyName }, out blackList);


            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            string warningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, dataAssemblyName.FullName, subsetName);
            string warningMessage3 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem2.ItemSpec, dataAssemblyName.FullName, subsetName);

            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.Equal(0, table.Count); // "Expected there to be two elements in the hashtable"
            Assert.False(table.ContainsKey(sqlclientAssemblyName)); // "Expected to not find the sqlclientAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(dataAssemblyName)); // "Expected to not to find the dataAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
            Assert.False(table.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"

            string[] warningMessages = new string[] { warningMessage, warningMessage2, warningMessage3 };
            foreach (string message in warningMessages)
            {
                Console.Out.WriteLine("WarningMessageToAssert:" + message);
                mockEngine.AssertLogContains(message);
            }
            table.Clear();
        }


        /// <summary>
        /// Testing case  
        /// enginePrimary -> dataDependencyReference   also enginePrimary->sqlDependencyReference   specific version = true on the primary
        /// xmlPrimaryReference ->dataDependencyReference specific version = false on the primary
        /// dataDependencyReference and sqlDependencyReference is in the black list.
        /// Expect to see one dependency warning messages xmlPrimarycase and no message for enginePrimary
        /// Also expect to resolve all files except for xmlPrimaryReference
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInBlackListPrimaryWithSpecificVersion()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Hashtable blackList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            ITaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            taskItem.SetMetadata("SpecificVersion", "true");

            ITaskItem taskItem2 = new TaskItem("System.Xml");
            taskItem2.SetMetadata("SpecificVersion", "false");

            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, true, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.FullPath = "FullPath";
            dataDependencyReference.FullPath = "FullPath";
            sqlDependencyReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            dataDependencyReference.MakeDependentAssemblyReference(xmlPrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName, dataAssemblyName }, out blackList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem2.ItemSpec, dataAssemblyName.FullName, subsetName);
            string notExpectedwarningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, dataAssemblyName.FullName, subsetName);
            string notExpectedwarningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);

            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.Equal(3, table.Count); // "Expected there to be three elements in the hashtable"
            Assert.True(table.ContainsKey(sqlclientAssemblyName)); // "Expected to find the sqlclientAssemblyName in the referenceList"
            Assert.True(table.ContainsKey(dataAssemblyName)); // "Expected to find the dataAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(xmlAssemblyName)); // "Expected not to find the xmlssemblyName in the referenceList"
            Assert.True(table.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"

            string[] warningMessages = new string[] { warningMessage };
            foreach (string message in warningMessages)
            {
                Console.Out.WriteLine("WarningMessageToAssert:" + message);
                mockEngine.AssertLogContains(message);
            }

            mockEngine.AssertLogDoesntContain(notExpectedwarningMessage);
            mockEngine.AssertLogDoesntContain(notExpectedwarningMessage2);
            table.Clear();
        }

        private static ReferenceTable MakeEmptyReferenceTable(TaskLoggingHelper log)
        {
            ReferenceTable referenceTable = new ReferenceTable(null, false, false, false, false, new string[0], null, null, null, null, null, null, SystemProcessorArchitecture.None, fileExists, null, null, null, null, null, null, null, null, null, new Version("4.0"), null, log, null, true, false, null, null, false, null, WarnOrErrorOnTargetArchitectureMismatchBehavior.None, false, false);
            return referenceTable;
        }

        /// <summary>
        /// Verify the correct references are still in the references table and that references which are in the black list are not in the references table
        /// Also verify any expected warning messages are seen in the log.
        /// </summary>
        private static void VerifyReferenceTable(ReferenceTable referenceTable, MockEngine mockEngine, AssemblyNameExtension engineAssemblyName, AssemblyNameExtension dataAssemblyName, AssemblyNameExtension sqlclientAssemblyName, AssemblyNameExtension xmlAssemblyName, string[] warningMessages)
        {
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.Equal(0, table.Count); // "Expected there to be zero elements in the hashtable"

            if (warningMessages != null)
            {
                foreach (string warningMessage in warningMessages)
                {
                    Console.Out.WriteLine("WarningMessageToAssert:" + warningMessages);
                    mockEngine.AssertLogContains(warningMessage);
                }
            }

            table.Clear();
        }

        /// <summary>
        /// Make sure we get an argument null exception when the profileName is set to null
        /// </summary>
        [Fact]
        public void TestProfileNameNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference rar = new ResolveAssemblyReference();
                rar.ProfileName = null;
            }
           );
        }
        /// <summary>
        /// Make sure we get an argument null exception when the ProfileFullFrameworkFolders is set to null
        /// </summary>
        [Fact]
        public void TestProfileFullFrameworkFoldersFoldersNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference rar = new ResolveAssemblyReference();
                rar.FullFrameworkFolders = null;
            }
           );
        }
        /// <summary>
        /// Make sure we get an argument null exception when the ProfileFullFrameworkAssemblyTables is set to null
        /// </summary>
        [Fact]
        public void TestProfileFullFrameworkAssemblyTablesNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference rar = new ResolveAssemblyReference();
                rar.FullFrameworkAssemblyTables = null;
            }
           );
        }
        /// <summary>
        /// Verify that setting a subset and a profile at the same time will cause an error to be logged and rar to return false
        /// </summary>
        [Fact]
        public void TestProfileAndSubset1()
        {
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            InitializeRARwithMockEngine(out mockEngine, out rar);

            rar.TargetFrameworkSubsets = new string[] { "Client" };
            rar.ProfileName = "Client";
            rar.FullFrameworkFolders = new string[] { "Client" };
            Assert.False(rar.Execute());
            mockEngine.AssertLogContains(rar.Log.FormatResourceString("ResolveAssemblyReference.CannotSetProfileAndSubSet"));
        }

        /// <summary>
        /// Verify that setting a subset and a profile at the same time will cause an error to be logged and rar to return false
        /// </summary>
        [Fact]
        public void TestProfileAndSubset2()
        {
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            InitializeRARwithMockEngine(out mockEngine, out rar);

            rar.InstalledAssemblySubsetTables = new ITaskItem[] { new TaskItem("Client.xml") };
            rar.ProfileName = "Client";
            rar.FullFrameworkFolders = new string[] { "Client" };
            Assert.False(rar.Execute());
            mockEngine.AssertLogContains(rar.Log.FormatResourceString("ResolveAssemblyReference.CannotSetProfileAndSubSet"));
        }

        /// <summary>
        /// Verify setting certain combinations of Profile parameters will case an error to be logged and rar to fail execution.
        /// 
        /// Test the case where the profile name is not set and ProfileFullFrameworkFolders is set.
        ///</summary>
        [Fact]
        public void TestProfileParameterCombinations()
        {
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            InitializeRARwithMockEngine(out mockEngine, out rar);
            rar.ProfileName = "Client";
            Assert.False(rar.Execute());
            mockEngine.AssertLogContains(rar.Log.FormatResourceString("ResolveAssemblyReference.MustSetProfileNameAndFolderLocations"));
        }

        /// <summary>
        /// Verify when the frameworkdirectory metadata is not set on the ProfileFullFrameworkAssemblyTables that an 
        /// error is logged and rar fails.
        ///</summary>
        [Fact]
        public void TestFrameworkDirectoryMetadata()
        {
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            InitializeRARwithMockEngine(out mockEngine, out rar);
            TaskItem item = new TaskItem("Client.xml");
            rar.ProfileName = "Client";
            rar.FullFrameworkAssemblyTables = new ITaskItem[] { item };
            Assert.False(rar.Execute());
            mockEngine.AssertLogContains(rar.Log.FormatResourceString("ResolveAssemblyReference.FrameworkDirectoryOnProfiles", item.ItemSpec));
        }

        private static void InitializeRARwithMockEngine(out MockEngine mockEngine, out ResolveAssemblyReference rar)
        {
            mockEngine = new MockEngine();
            rar = new ResolveAssemblyReference();
            rar.BuildEngine = mockEngine;
        }

        /// <summary>
        /// Add a set of references and their names to the reference table.
        /// </summary>
        private static void AddReferencesToReferenceTable(ReferenceTable referenceTable, AssemblyNameExtension engineAssemblyName, AssemblyNameExtension dataAssemblyName, AssemblyNameExtension sqlclientAssemblyName, AssemblyNameExtension xmlAssemblyName, Reference enginePrimaryReference, Reference dataDependencyReference, Reference sqlDependencyReference, Reference xmlPrimaryReference)
        {
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            if (enginePrimaryReference != null)
            {
                table.Add(engineAssemblyName, enginePrimaryReference);
            }

            if (dataDependencyReference != null)
            {
                table.Add(dataAssemblyName, dataDependencyReference);
            }
            if (sqlDependencyReference != null)
            {
                table.Add(sqlclientAssemblyName, sqlDependencyReference);
            }

            if (xmlPrimaryReference != null)
            {
                table.Add(xmlAssemblyName, xmlPrimaryReference);
            }
        }

        /// <summary>
        /// Initialize the mock engine so we can look at the warning messages, also put the assembly name which is to be in the black list into the black list.
        /// Call remove references so that we can then validate the results.
        /// </summary>
        private void InitializeMockEngine(out ReferenceTable referenceTable, out MockEngine mockEngine, out ResolveAssemblyReference rar)
        {
            mockEngine = new MockEngine();
            rar = new ResolveAssemblyReference();
            rar.BuildEngine = mockEngine;

            referenceTable = MakeEmptyReferenceTable(rar.Log);
        }


        /// <summary>
        ///Initialize the black list and use it to remove references from the reference table
        /// </summary>
        private void InitializeExclusionList(ReferenceTable referenceTable, AssemblyNameExtension[] assembliesForBlackList, out Hashtable blackList)
        {
            blackList = new Hashtable(StringComparer.OrdinalIgnoreCase);
            foreach (AssemblyNameExtension assemblyName in assembliesForBlackList)
            {
                blackList[assemblyName.FullName] = null;
            }

            referenceTable.MarkReferencesForExclusion(blackList);
            referenceTable.RemoveReferencesMarkedForExclusion(false, "Client");
        }

        /// <summary>
        /// Before each test to validate the references are correctly removed from the reference table we need to make new instances of them
        /// </summary>
        /// <param name="enginePrimaryReference"></param>
        /// <param name="dataDependencyReference"></param>
        /// <param name="sqlDependencyReference"></param>
        /// <param name="xmlPrimaryReference"></param>
        private static void GenerateNewReferences(out Reference enginePrimaryReference, out Reference dataDependencyReference, out Reference sqlDependencyReference, out Reference xmlPrimaryReference)
        {
            enginePrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            dataDependencyReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            sqlDependencyReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            xmlPrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
        }

        /// <summary>
        /// This test will verify the IgnoreDefaultInstalledSubsetTables property on the RAR task.
        /// The property determines whether or not RAR will search the target framework directories under the subsetList folder for 
        /// xml files matching the client subset names passed into the TargetFrameworkSubset property.
        /// 
        /// The default for the property is false, when the value is false RAR will search the SubsetList folder under the TargetFramework directories 
        /// for the xml files with names in the TargetFrameworkSubset property.  When the value is true, RAR will not search the SubsetList directory. The only 
        /// way to specify a TargetFrameworkSubset is to pass one to the InstalledAssemblySubsetTables property.
        /// </summary>
        [Fact]
        public void IgnoreDefaultInstalledSubsetTables()
        {
            string redistListPath = CreateGenericRedistList();
            string subsetListClientPath = string.Empty;
            string explicitSubsetListPath = string.Empty;

            try
            {
                subsetListClientPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("v3.5\\SubsetList\\Client.xml", _engineOnlySubset);
                explicitSubsetListPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("v3.5\\SubsetList\\ExplicitList.xml", _xmlOnlySubset);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine();
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}" };

                // This is a TargetFrameworkSubset that would be searched by RAR if IgnoreDefaultINstalledAssemblySubsetTables does not work.
                t.TargetFrameworkSubsets = new string[] { "Client" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.InstalledAssemblySubsetTables = new ITaskItem[] { new TaskItem(explicitSubsetListPath) };
                t.IgnoreDefaultInstalledAssemblySubsetTables = true;

                string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");

                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);

                Assert.True(success); // "Expected no errors."
                Assert.Equal(1, t.ResolvedFiles.Length); // "Expected one resolved assembly."
                Assert.True(t.ResolvedFiles[0].ItemSpec.Contains("System.Xml")); // "Expected System.Xml to resolve."
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// Generate helper delegates for returning the file existence and the assembly name.
        /// Also run the rest and return the result.
        /// </summary>
        private bool GenerateHelperDelegatesAndExecuteTask(ResolveAssemblyReference t, string microsoftBuildEnginePath, string systemXmlPath)
        {
            FileExists cachedFileExists = fileExists;
            GetAssemblyName cachedGetAssemblyName = getAssemblyName;
            fileExists = new FileExists(delegate (string path)
            {
                if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            });

            getAssemblyName = new GetAssemblyName(delegate (string path)
            {
                if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }
                else if (String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }

                return null;
            });

            bool success;
            try
            {
                success = Execute(t);
            }
            finally
            {
                fileExists = cachedFileExists;
                getAssemblyName = cachedGetAssemblyName;
            }
            return success;
        }

        /// <summary>
        /// Test the case where there are no client subset names passed in but an InstalledDefaultSubsetTable 
        /// is passed in. We expect to use that.
        /// </summary>
        [Fact]
        public void NoClientSubsetButInstalledSubTables()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine();
                // These are the assemblies we are going to try and resolve
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                // Only the explicitly specified redist list should be used
                t.TargetFrameworkSubsets = new string[0];

                // Create a subset list which should be read in
                string explicitSubsetListContents =
                        "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "</FileList >";

                string explicitSubsetListPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("v3.5\\SubsetList\\ExplicitList.xml", explicitSubsetListContents);
                t.InstalledAssemblySubsetTables = new ITaskItem[] { new TaskItem(explicitSubsetListPath) };
                t.IgnoreDefaultInstalledAssemblySubsetTables = true;

                string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");
                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);

                Assert.True(success); // "Expected no errors."
                Assert.Equal(1, t.ResolvedFiles.Length); // "Expected one resolved assembly."
                Assert.True(t.ResolvedFiles[0].ItemSpec.Contains("System.Xml")); // "Expected System.Xml to resolve."
                MockEngine engine = ((MockEngine)t.BuildEngine);
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.UsingExclusionList"));
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// Verify the case where the installedSubsetTables are null
        /// </summary>
        [Fact]
        public void NullInstalledSubsetTables()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference reference = new ResolveAssemblyReference();
                reference.InstalledAssemblySubsetTables = null;
            }
           );
        }
        /// <summary>
        /// Verify the case where the targetFrameworkSubsets are null
        /// </summary>
        [Fact]
        public void NullTargetFrameworkSubsets()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference reference = new ResolveAssemblyReference();
                reference.TargetFrameworkSubsets = null;
            }
           );
        }
        /// <summary>
        /// Verify the case where the FulltargetFrameworkSubsetNames are null
        /// </summary>
        [Fact]
        public void NullFullTargetFrameworkSubsetNames()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference reference = new ResolveAssemblyReference();
                reference.FullTargetFrameworkSubsetNames = null;
            }
           );
        }
        /// <summary>
        /// Test the case where a non existent subset list path is used and no additional subsets are passed in.
        /// </summary>
        [Fact]
        public void FakeSubsetListPathsNoAdditionalSubsets()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine();
                // These are the assemblies we are going to try and resolve
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}" };

                t.TargetFrameworkSubsets = new string[] { "NOTTOEXIST" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };

                // Only the explicitly specified redist list should be used
                t.IgnoreDefaultInstalledAssemblyTables = true;

                string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");
                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);
                Assert.True(success); // "Expected no errors."
                MockEngine engine = ((MockEngine)t.BuildEngine);
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.UsingExclusionList"));
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.NoSubsetsFound"));
                Assert.Equal(2, t.ResolvedFiles.Length); // "Expected one resolved assembly."
                Assert.True(t.ResolvedFiles[1].ItemSpec.Contains("System.Xml")); // "Expected System.Xml to resolve."
                Assert.True(t.ResolvedFiles[0].ItemSpec.Contains("Microsoft.Build.Engine")); // "Expected Microsoft.Build.Engine to resolve."
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// This test will verify when the full client name is passed in and it appears in the TargetFrameworkSubsetList, that the
        /// black list is not used.
        /// </summary>
        [Fact]
        public void ResolveAssemblyReferenceVerifyFullClientName()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine();

                // These are the assemblies we are going to try and resolve
                t.Assemblies = new ITaskItem[] { new TaskItem("System.Xml") };

                // This is a TargetFrameworkSubset that would be searched by RAR if IgnoreDefaultINstalledAssemblySubsetTables does not work.
                t.TargetFrameworkSubsets = new string[] { "Client", "Full" };
                t.FullTargetFrameworkSubsetNames = new string[] { "Full" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                t.IgnoreDefaultInstalledAssemblyTables = true;

                Execute(t);
                MockEngine engine = (MockEngine)t.BuildEngine;
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.NoExclusionListBecauseofFullClientName", "Full"));
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }


        /// <summary>
        /// This test will verify when the full client name is passed in and it appears in the TargetFrameworkSubsetList, that the
        /// black list is not used.
        /// </summary>
        [Fact]
        public void ResolveAssemblyReferenceVerifyFullClientNameWithSubsetTables()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine();
                // These are the assemblies we are going to try and resolve
                t.Assemblies = new ITaskItem[] { new TaskItem("System.Xml") };

                // This is a TargetFrameworkSubset that would be searched by RAR if IgnoreDefaultINstalledAssemblySubsetTables does not work.
                t.TargetFrameworkSubsets = new string[] { "Client", "Full" };
                t.FullTargetFrameworkSubsetNames = new string[] { "Full" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.IgnoreDefaultInstalledAssemblySubsetTables = true;
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                t.InstalledAssemblySubsetTables = new ITaskItem[] { new TaskItem(@"C:\LocationOfSubset.xml") };
                t.IgnoreDefaultInstalledAssemblyTables = true;

                Execute(t);

                MockEngine engine = (MockEngine)t.BuildEngine;
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.NoExclusionListBecauseofFullClientName", "Full"));
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }


        /// <summary>
        /// This test will verify when the full client name is passed in and it appears in the TargetFrameworkSubsetList, that the
        /// black list is not used.
        /// </summary>
        [Fact]
        public void ResolveAssemblyReferenceVerifyFullClientNameNoTablesPassedIn()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine();
                // These are the assemblies we are going to try and resolve
                t.Assemblies = new ITaskItem[] { new TaskItem("System.Xml") };

                // This is a TargetFrameworkSubset that would be searched by RAR if IgnoreDefaultINstalledAssemblySubsetTables does not work.
                t.TargetFrameworkSubsets = new string[] { "Client", "Full" };
                t.FullTargetFrameworkSubsetNames = new string[] { "Full" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.IgnoreDefaultInstalledAssemblySubsetTables = true;
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                t.IgnoreDefaultInstalledAssemblyTables = true;

                Execute(t);

                MockEngine engine = (MockEngine)t.BuildEngine;
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.NoExclusionListBecauseofFullClientName", "Full"));
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// Verify the correct references are still in the references table and that references which are in the black list are not in the references table
        /// Also verify any expected warning messages are seen in the log.
        /// </summary>
        private static void VerifyReferenceTable(ReferenceTable referenceTable, MockEngine mockEngine, AssemblyNameExtension engineAssemblyName, AssemblyNameExtension dataAssemblyName, AssemblyNameExtension sqlclientAssemblyName, AssemblyNameExtension xmlAssemblyName, string warningMessage, string warningMessage2)
        {
            IDictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.Equal(3, table.Count); // "Expected there to be three elements in the hashtable"
            Assert.False(table.ContainsKey(sqlclientAssemblyName)); // "Expected to not find the sqlclientAssemblyName in the referenceList"
            Assert.True(table.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
            Assert.True(table.ContainsKey(dataAssemblyName)); // "Expected to find the dataAssemblyName in the referenceList"
            Assert.True(table.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"
            if (warningMessage != null)
            {
                mockEngine.AssertLogContains(warningMessage);
            }
            if (warningMessage2 != null)
            {
                mockEngine.AssertLogContains(warningMessage2);
            }
            table.Clear();
        }

        /// <summary>
        /// Generate helper delegates for returning the file existence and the assembly name.
        /// Also run the rest and return the result.
        /// </summary>
        private bool GenerateHelperDelegatesAndExecuteTask(ResolveAssemblyReference t)
        {
            FileExists cachedFileExists = fileExists;
            GetAssemblyName cachedGetAssemblyName = getAssemblyName;
            string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine.dll");
            string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");
            fileExists = new FileExists(delegate (string path)
{
    if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }
    return false;
});

            getAssemblyName = new GetAssemblyName(delegate (string path)
            {
                if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }
                else if (String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }

                return null;
            });

            bool success;
            try
            {
                success = Execute(t);
            }
            finally
            {
                fileExists = cachedFileExists;
                getAssemblyName = cachedGetAssemblyName;
            }
            return success;
        }

        [Fact]
        public void DoNotAssumeFilesDescribedByRedistListExistOnDisk()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = new MockEngine();

                t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build.Engine"),
                new TaskItem("System.Xml")
            };

                t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}"
            };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine");
                string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");

                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };

                FileExists cachedFileExists = fileExists;
                GetAssemblyName cachedGetAssemblyName = getAssemblyName;

                // Note that Microsoft.Build.Engine.dll does not exist
                fileExists = new FileExists(delegate (string path)
                {
                    if (String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase) || path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return false;
                });

                getAssemblyName = new GetAssemblyName(delegate (string path)
                {
                    if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    }
                    else if (String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    }

                    return null;
                });

                bool success;
                try
                {
                    success = Execute(t);
                }
                finally
                {
                    fileExists = cachedFileExists;
                    getAssemblyName = cachedGetAssemblyName;
                }

                Assert.True(success); // "Expected no errors."
                Assert.Equal(1, t.ResolvedFiles.Length); // "Expected one resolved assembly."
                Assert.True(t.ResolvedFiles[0].ItemSpec.Contains("System.Xml")); // "Expected System.Xml to resolve."
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// Here's how you get into this situation:
        /// 
        /// App
        ///   References - A
        /// 
        ///    And, the following conditions.
        ///     $(ReferencePath) = c:\apath;:
        /// 
        /// Expected result:
        /// * Invalid paths should be ignored.
        /// 
        /// </summary>
        [Fact]
        public void Regress397129_HandleInvalidDirectoriesAndFiles_Case1()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.SearchPaths = new string[]
            {
                @"c:\apath",
                @":"
            };

            Execute(t); // Expect no exception.
        }

        /// <summary>
        /// Here's how you get into this situation:
        /// 
        /// App
        ///   References - A 
        ///        Hintpath=||invalidpath||
        /// 
        /// Expected result:
        /// * No exceptions.
        /// 
        /// </summary>
        [Fact]
        public void Regress397129_HandleInvalidDirectoriesAndFiles_Case2()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.Assemblies[0].SetMetadata("HintPath", @"||invalidpath||");


            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            Execute(t);
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - Microsoft.Office.Interop.Excel
        ///        Depends on Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c
        /// 
        ///   References - MS.Internal.Test.Automation.Office.Excel 
        ///        Depends on Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=94de0004b6e3fcc5
        /// 
        /// Notice that the two primaries have dependencies that only differ by PKT. Suggested redirects should
        /// only happen if the two assemblies differ by nothing but version.
        /// </summary>
        [Fact]
        public void Regress313747_FalseSuggestedRedirectsWhenAssembliesDifferOnlyByPkt()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Office.Interop.Excel"),
                new TaskItem("MS.Internal.Test.Automation.Office.Excel")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress313747",
            };

            Execute(t);

            Assert.Equal(0, t.SuggestedRedirects.Length);
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// (1) Primary reference A v 2.0.0.0 is found.
        /// (2) Primary reference B is found.
        /// (3) Primary reference B depends on A v 1.0.0.0 
        /// (4) Dependency A v 1.0.0.0 is not found.
        /// (5) App.Config does not contain a binding redirect from A v 1.0.0.0 -> 2.0.0.0
        /// 
        /// We need to warn and suggest an app.config entry because the runtime environment will require a binding
        /// redirect to function. Without a binding redirect, loading B will cause A.V1 to try to load. It won't be
        /// there and there won't be a binding redirect to point it at 2.0.0.0.
        /// </summary>
        [Fact]
        public void Regress442570_MissingBackVersionShouldWarn()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress442570",
            };

            Execute(t);

            // Expect a suggested redirect plus a warning
            Assert.Equal(1, t.SuggestedRedirects.Length);
            Assert.Equal(1, e.Warnings);
        }


        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A
        ///        Depends on B
        ///        Will be found by hintpath.
        ///   References -B
        ///        No hintpath 
        ///        Exists in A.dll's folder.
        /// 
        /// B.dll should be unresolved even though its in A's folder because primary resolution needs to work
        /// without looking at dependencies because of the load-time perf scenarios don't look at dependencies.
        /// We must be consistent between primaries resolved with FindDependencies=true and FindDependencies=false.
        [Fact]
        public void ByDesignRelatedTo454863_PrimaryReferencesDontResolveToParentFolders()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };
            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress454863\A.dll");

            t.SearchPaths = new string[]
            {
                "{HintPathFromItem}"
            };

            Execute(t);

            Assert.True(ContainsItem(t.ResolvedFiles, @"C:\Regress454863\A.dll")); // "Expected A.dll to be resolved."
            Assert.False(ContainsItem(t.ResolvedFiles, @"C:\Regress454863\B.dll")); // "Expected B.dll to be *not* be resolved."
        }

        [Fact]
        public void Regress393931_AllowAlternateAssemblyExtensions_Case1()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };


            t.SearchPaths = new string[]
            {
                @"C:\Regress393931"
            };
            t.AllowedAssemblyExtensions = new string[]
            {
                ".metaData_dll"
            };

            Execute(t);

            // Expect a suggested redirect plus a warning
            Assert.True(ContainsItem(t.ResolvedFiles, @"C:\Regress393931\A.metadata_dll")); // "Expected A.dll to be resolved."
        }

        /// <summary>
        /// Allow alternate extension values to be passed in.
        /// </summary>
        [Fact]
        public void Regress393931_AllowAlternateAssemblyExtensions()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };


            t.SearchPaths = new string[]
            {
                @"C:\Regress393931"
            };
            t.AllowedAssemblyExtensions = new string[]
            {
                ".metaData_dll"
            };

            Execute(t);

            // Expect a suggested redirect plus a warning
            Assert.True(ContainsItem(t.ResolvedFiles, @"C:\Regress393931\A.metadata_dll")); // "Expected A.dll to be resolved."
        }


        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A
        ///        Depends on D version 1 (but PKT=null)
        ///   References - B
        ///        Depends on D version 2 (but PKT=null)
        /// 
        /// There should be no suggested redirect because only strongly named assemblies can have
        /// binding redirects.
        /// </summary>
        [Fact]
        public void Regress387218_UnificationRequiresStrongName()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress387218",
                @"c:\Regress387218\v1",
                @"c:\Regress387218\v2"
            };

            Execute(t);

            Assert.Equal(2, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Regress387218\v2\D.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Regress387218\v1\D.dll")); // "Expected to find assembly, but didn't."
            Assert.Equal(0, t.SuggestedRedirects.Length);
            Assert.Equal(0, e.Warnings); // "Should only be no warning about suggested redirects."
        }

        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   References - A
        ///        Depends on D version 1 (but Culture=fr)
        ///   References - B
        ///        Depends on D version 2 (but Culture=en)
        /// 
        /// There should be no suggested redirect because assemblies with different cultures cannot unify.
        /// </summary>
        [Fact]
        public void Regress390219_UnificationRequiresSameCulture()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress390219",
                @"c:\Regress390219\v1",
                @"c:\Regress390219\v2"
            };

            Execute(t);

            Assert.Equal(2, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Regress390219\v2\D.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Regress390219\v1\D.dll")); // "Expected to find assembly, but didn't."
            Assert.Equal(0, t.SuggestedRedirects.Length);
            Assert.Equal(0, e.Warnings); // "Should only be no warning about suggested redirects."
        }


        [Fact]
        public void SGenDependeicies()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.Assemblies = new TaskItem[]
            {
                new TaskItem("mycomponent"),
                new TaskItem("mycomponent2")
            };

            t.AssemblyFiles = new TaskItem[]
            {
                new TaskItem(@"c:\SGenDependeicies\mycomponent.dll"),
                new TaskItem(@"c:\SGenDependeicies\mycomponent2.dll")
            };

            t.SearchPaths = new string[]
            {
                @"c:\SGenDependeicies"
            };

            t.FindSerializationAssemblies = true;

            Execute(t);

            Assert.True(t.FindSerializationAssemblies); // "Expected to find serialization assembly."
            Assert.True(ContainsItem(t.SerializationAssemblyFiles, @"c:\SGenDependeicies\mycomponent.XmlSerializers.dll")); // "Expected to find serialization assembly, but didn't."
            Assert.True(ContainsItem(t.SerializationAssemblyFiles, @"c:\SGenDependeicies\mycomponent2.XmlSerializers.dll")); // "Expected to find serialization assembly, but didn't."
        }


        /// <summary>
        /// Consider this dependency chain:
        /// 
        /// App
        ///   Has project reference to c:\Regress315619\A\MyAssembly.dll
        ///   Has project reference to c:\Regress315619\B\MyAssembly.dll
        /// 
        /// These two project references have different versions. Important: PKT is null.
        /// </summary>
        [Fact]
        public void Regress315619_TwoWeaklyNamedPrimariesIsInsoluble()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine();
            t.BuildEngine = e;

            t.AssemblyFiles = new ITaskItem[]
            {
                new TaskItem(@"c:\Regress315619\A\MyAssembly.dll"),
                new TaskItem(@"c:\Regress315619\B\MyAssembly.dll")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress315619\A",
                @"c:\Regress315619\B"
            };

            Execute(t);

            e.AssertLogContains
            (
                String.Format(AssemblyResources.GetString("ResolveAssemblyReference.ConflictUnsolvable"), @"MyAssembly, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", "MyAssembly, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null")
            );
        }

        /// <summary>
        /// This is a fix to help ClickOnce folks correctly display information about which
        /// redist components can be deployed.
        /// 
        /// Two new attributes are added to resolved references:
        /// (1) IsRedistRoot (bool) -- The flag from the redist *.xml file. If there is no 
        /// flag in the file then there will be no flag on the resulting item. This flag means
        /// "I am the UI representative for this entire redist". ClickOnce will use this to hide
        /// all other redist items and to show only this item.
        /// 
        /// (2) Redist (string) -- This the the value of FileList Redist from the *.xml file.
        /// This string means "I am the unique name of this entire redist". 
        /// 
        /// </summary>
        [Fact]
        public void ForwardRedistRoot()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("MyRedistRootAssembly"),
                new TaskItem("MyOtherAssembly"),
                new TaskItem("MyThirdAssembly")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyRedist"
            };

            string redistFile = FileUtilities.GetTemporaryFile();

            try
            {
                File.Delete(redistFile);
                File.WriteAllText
(
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File IsRedistRoot='true' AssemblyName='MyRedistRootAssembly' Version='0.0.0.0' PublicKeyToken='null' Culture='Neutral' FileVersion='2.0.40824.0' InGAC='true'/>" +
                        "<File IsRedistRoot='false' AssemblyName='MyOtherAssembly' Version='0.0.0.0' PublicKeyToken='null' Culture='Neutral' FileVersion='2.0.40824.0' InGAC='true'/>" +
                        "<File AssemblyName='MyThirdAssembly' Version='0.0.0.0' PublicKeyToken='null' Culture='Neutral' FileVersion='2.0.40824.0' InGAC='true'/>" +
                    "</FileList >"
                );

                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistFile) };

                Execute(t);
            }
            finally
            {
                File.Delete(redistFile);
            }

            Assert.Equal(3, t.ResolvedFiles.Length); // "Expected three assemblies to be found."
            Assert.Equal("true", t.ResolvedFiles[1].GetMetadata("IsRedistRoot"));
            Assert.Equal("false", t.ResolvedFiles[0].GetMetadata("IsRedistRoot"));
            Assert.Equal("", t.ResolvedFiles[2].GetMetadata("IsRedistRoot"));

            Assert.Equal("Microsoft-Windows-CLRCoreComp", t.ResolvedFiles[0].GetMetadata("Redist"));
            Assert.Equal("Microsoft-Windows-CLRCoreComp", t.ResolvedFiles[1].GetMetadata("Redist"));
            Assert.Equal("Microsoft-Windows-CLRCoreComp", t.ResolvedFiles[2].GetMetadata("Redist"));
        }

        /// <summary>
        /// helper for  TargetFrameworkFiltering
        /// </summary>
        private int RunTargetFrameworkFilteringTest(string projectTargetFramework)
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = new MockEngine();
            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B"),
                new TaskItem("C")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyLibraries"
            };

            t.Assemblies[1].SetMetadata("RequiredTargetFramework", "3.0");
            t.Assemblies[2].SetMetadata("RequiredTargetFramework", "3.5");
            t.TargetFrameworkVersion = projectTargetFramework;

            Execute(t);

            int set = 0;
            foreach (ITaskItem item in t.ResolvedFiles)
            {
                int mask = 0;
                if (item.ItemSpec.EndsWith(@"\A.dll"))
                {
                    mask = 1;
                }
                else if (item.ItemSpec.EndsWith(@"\B.dll"))
                {
                    mask = 2;
                }
                else if (item.ItemSpec.EndsWith(@"\C.dll"))
                {
                    mask = 4;
                }
                Assert.NotEqual(0, mask); // "Unexpected assembly in resolved list."
                Assert.Equal(0, (mask & set)); // "Assembly found twice in resolved list."
                set = set | mask;
            }
            return set;
        }

        /// <summary>
        /// Make sure the reverse assembly name comparer correctly sorts the assembly names in reverse order
        /// </summary>
        [Fact]
        public void ReverseAssemblyNameExtensionComparer()
        {
            IComparer sortByVersionDescending = new RedistList.SortByVersionDescending();
            AssemblyEntry a1 = new AssemblyEntry("Microsoft.Build.Engine", "1.0.0.0", "b03f5f7f11d50a3a", "neutral", true, true, "Foo", "none", true);
            AssemblyEntry a2 = new AssemblyEntry("Microsoft.Build.Engine", "2.0.0.0", "b03f5f7f11d50a3a", "neutral", true, true, "Foo", "none", false);
            AssemblyEntry a3 = new AssemblyEntry("Microsoft.Build.Engine", "3.0.0.0", "b03f5f7f11d50a3a", "neutral", true, true, "Foo", "none", true);
            AssemblyEntry a4 = new AssemblyEntry("A", "3.0.0.0", "b03f5f7f11d50a3a", "neutral", true, true, "Foo", "none", true);
            AssemblyEntry a5 = new AssemblyEntry("B", "3.0.0.0", "b03f5f7f11d50a3a", "neutral", true, true, "Foo", "none", true);

            // Verify versions sort correctly when simple name is same
            Assert.Equal(0, sortByVersionDescending.Compare(a1, a1));
            Assert.Equal(1, sortByVersionDescending.Compare(a1, a2));
            Assert.Equal(1, sortByVersionDescending.Compare(a1, a3));
            Assert.Equal(-1, sortByVersionDescending.Compare(a2, a1));
            Assert.Equal(1, sortByVersionDescending.Compare(a2, a3));

            // Verify the names sort alphabetically
            Assert.Equal(-1, sortByVersionDescending.Compare(a4, a5));
        }

        /// <summary>
        /// Check the Filtering based on Target Framework.
        /// </summary>
        [Fact]
        public void TargetFrameworkFiltering()
        {
            int resultSet = 0;
            resultSet = RunTargetFrameworkFilteringTest("3.0");
            Assert.Equal(resultSet, 0x3); // "Expected assemblies A & B to be found."

            resultSet = RunTargetFrameworkFilteringTest("3.5");
            Assert.Equal(resultSet, 0x7); // "Expected assemblies A, B & C to be found."

            resultSet = RunTargetFrameworkFilteringTest(null);
            Assert.Equal(resultSet, 0x7); // "Expected assemblies A, B & C to be found."

            resultSet = RunTargetFrameworkFilteringTest("2.0");
            Assert.Equal(resultSet, 0x1); // "Expected only assembly A to be found."
        }

        /// <summary>
        /// Verify the when a simple name is asked for that the assemblies are returned in sorted order by version.
        /// </summary>
        [Fact]
        public void VerifyGetSimpleNamesIsSorted()
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                        "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                        "<File AssemblyName='System' Version='3.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                        "<File AssemblyName='System' Version='100.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                        "<File AssemblyName='System' Version='1.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                        "<File AssemblyName='System' Version='2.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >"
                );

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyEntry[] entryArray = redist.FindAssemblyNameFromSimpleName("System");
                Assert.Equal(6, entryArray.Length);
                AssemblyNameExtension a1 = new AssemblyNameExtension(entryArray[0].FullName);
                AssemblyNameExtension a2 = new AssemblyNameExtension(entryArray[1].FullName);
                AssemblyNameExtension a3 = new AssemblyNameExtension(entryArray[2].FullName);
                AssemblyNameExtension a4 = new AssemblyNameExtension(entryArray[3].FullName);
                AssemblyNameExtension a5 = new AssemblyNameExtension(entryArray[4].FullName);
                AssemblyNameExtension a6 = new AssemblyNameExtension(entryArray[5].FullName);

                Assert.True(a1.Version.Equals(new Version("100.0.0.0")), "Expect to find version 100.0.0.0 but instead found:" + a1.Version);
                Assert.True(a2.Version.Equals(new Version("10.0.0.0")), "Expect to find version 10.0.0.0 but instead found:" + a2.Version);
                Assert.True(a3.Version.Equals(new Version("4.0.0.0")), "Expect to find version 4.0.0.0 but instead found:" + a3.Version);
                Assert.True(a4.Version.Equals(new Version("3.0.0.0")), "Expect to find version 3.0.0.0 but instead found:" + a4.Version);
                Assert.True(a5.Version.Equals(new Version("2.0.0.0")), "Expect to find version 2.0.0.0 but instead found:" + a5.Version);
                Assert.True(a6.Version.Equals(new Version("1.0.0.0")), "Expect to find version 1.0.0.0 but instead found:" + a6.Version);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If the assembly was found in a redis list which does not have the correct redist name , Microsoft-Windows-CLRCoreComp then we should not consider it a framework assembly.
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListNonWindowsRedistName()
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >"
                );

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.False(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If the assembly was found in a redis list which does have the correct redist name , Microsoft-Windows-CLRCoreComp then we should consider it a framework assembly.
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListWindowsRedistName()
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Something' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >"
                );

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If the assembly was found in a redis list which does have the correct redist name , Microsoft-Windows-CLRCoreComp then we should consider it a framework assembly taking into account including partial matching
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListPartialMatches()
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >"
                );

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);

                a1 = new AssemblyNameExtension("System, Culture=Neutral, PublicKeyToken='b77a5c561934e089'");
                inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);

                a1 = new AssemblyNameExtension("System, PublicKeyToken='b77a5c561934e089'");
                inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);

                a1 = new AssemblyNameExtension("System");
                inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }
        /// <summary>
        /// Verify when we ask if an assembly is in the redist list we get the right answer.
        /// The version should not be compared
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListDiffVersion()
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >"
                );

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=5.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }


        /// <summary>
        /// Verify when we ask if an assembly is in the redist list we get the right answer.
        /// The public key is significant and should make the match not work
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListDiffPublicKey()
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >"
                );

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=5.0.0.0, Culture=Neutral, PublicKeyToken='b67a5c561934e089'");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.False(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Verify when we ask if an assembly is in the redist list we get the right answer.
        /// The Culture is significant and should make the match not work
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListDiffCulture()
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='FR-fr' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >"
                );

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b67a5c561934e089'");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.False(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Verify when we ask if an assembly is in the redist list we get the right answer.
        /// The SimpleName is significant and should make the match not work
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListDiffSimpleName()
        {
            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                (
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >"
                );

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("Something, Version=10.0.0.0, Culture=Neutral, PublicKeyToken='b77a5c561934e089'");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.False(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Verify when a p2p (assemblies in the AssemblyFiles property) are passed to rar that we properly un-resolve them if they depend on references which are in the black list for the profile.
        /// </summary>
        [Fact]
        public void Verifyp2pAndProfile()
        {
            // Create a generic redist list with system.xml and microsoft.build.engine.
            string profileRedistList = String.Empty;
            string fullRedistList = String.Empty;
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "Verifyp2pAndProfile");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles\\Client");

            string fullRedistListContents =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='System' Version='9.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='Neutral'/>" +
            "</FileList >";

            try
            {
                GenerateRedistAndProfileXmlLocations(fullRedistListContents, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine();
                t.BuildEngine = e;
                t.AssemblyFiles = new ITaskItem[] { new TaskItem(@"c:\MyComponents\misc\DependsOn9Also.dll") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}", fullFrameworkDirectory };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.FullFrameworkFolders = new string[] { fullFrameworkDirectory };
                t.ProfileName = "Client";
                t.TargetFrameworkMoniker = ".Net Framework, Version=v4.0";

                bool success = Execute(t, false);
                Assert.True(success); // "Expected no errors."
                Assert.Equal(0, t.ResolvedFiles.Length); // "Expected no resolved assemblies."
                string warningMessage = t.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", @"c:\MyComponents\misc\DependsOn9Also.dll", "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.TargetFrameworkMoniker);
                e.AssertLogContains(warningMessage);
            }
            finally
            {
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    Directory.Delete(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// Verify when a p2p (assemblies in the AssemblyFiles property) are passed to rar that we properly resolve them if they depend on references which are in the black list for the profile but have specific version set to true.
        /// </summary>
        [Fact]
        public void Verifyp2pAndProfile2()
        {
            // Create a generic redist list with system.xml and microsoft.build.engine.
            string profileRedistList = String.Empty;
            string fullRedistList = String.Empty;
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "Verifyp2pAndProfile");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles\\Client");

            string fullRedistListContents =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='System' Version='9.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='Neutral'/>" +
            "</FileList >";

            try
            {
                GenerateRedistAndProfileXmlLocations(fullRedistListContents, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine();
                t.BuildEngine = e;
                TaskItem item = new TaskItem(@"c:\MyComponents\misc\DependsOn9Also.dll");
                item.SetMetadata("SpecificVersion", "true");
                t.AssemblyFiles = new ITaskItem[] { item };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}", fullFrameworkDirectory };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.FullFrameworkFolders = new string[] { fullFrameworkDirectory };
                t.ProfileName = "Client";

                bool success = Execute(t);
                Assert.True(success); // "Expected no errors."
                Assert.Equal(1, t.ResolvedFiles.Length); // "Expected no resolved assemblies."
                string warningMessage = t.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", @"c:\MyComponents\misc\DependsOn9Also.dll", "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "Client");
                e.AssertLogDoesntContain(warningMessage);
            }
            finally
            {
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    Directory.Delete(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// Verify when a profile is used that assemblies not in the profile are excluded or have metadata attached to indicate there are dependencies 
        /// which are not in the profile.
        /// </summary>
        [Fact]
        public void VerifyClientProfileRedistListAndProfileList()
        {
            // Create a generic redist list with system.xml and microsoft.build.engine.
            string profileRedistList = String.Empty;
            string fullRedistList = String.Empty;
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "VerifyClientProfileRedistListAndProfileList");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles\\Client");
            try
            {
                GenerateRedistAndProfileXmlLocations(_fullRedistListContents, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine();
                t.BuildEngine = e;
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}", fullFrameworkDirectory };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.FullFrameworkFolders = new string[] { fullFrameworkDirectory };
                t.ProfileName = "Client";

                string microsoftBuildEnginePath = Path.Combine(fullFrameworkDirectory, "Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(targetFrameworkDirectory, "System.Xml.dll");

                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);
                Assert.True(success); // "Expected no errors."
                Assert.Equal(1, t.ResolvedFiles.Length); // "Expected one resolved assembly."
                Assert.True(t.ResolvedFiles[0].ItemSpec.Contains("Microsoft.Build.Engine")); // "Expected Engine to resolve."
                e.AssertLogContains("MSB3252");
            }
            finally
            {
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    Directory.Delete(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// Verify when a profile is used that assemblies not in the profile are excluded or have metadata attached to indicate there are dependencies 
        /// which are not in the profile.
        /// 
        /// Make sure the ProfileFullFrameworkAssemblyTable parameter works.
        /// </summary>
        [Fact]
        public void VerifyClientProfileRedistListAndProfileList2()
        {
            // Create a generic redist list with system.xml and microsoft.build.engine.
            string profileRedistList = String.Empty;
            string fullRedistList = String.Empty;
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "VerifyClientProfileRedistListAndProfileList2");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles\\Client");
            try
            {
                GenerateRedistAndProfileXmlLocations(_fullRedistListContents, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine();
                t.BuildEngine = e;
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}", fullFrameworkDirectory };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;

                ITaskItem item = new TaskItem(fullRedistList);
                item.SetMetadata("FrameworkDirectory", Path.GetDirectoryName(fullRedistList));
                t.FullFrameworkAssemblyTables = new ITaskItem[] { item };
                t.ProfileName = "Client";

                string microsoftBuildEnginePath = Path.Combine(fullFrameworkDirectory, "Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(targetFrameworkDirectory, "System.Xml.dll");

                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);
                Assert.True(success); // "Expected no errors."
                Assert.Equal(1, t.ResolvedFiles.Length); // "Expected one resolved assembly."
                Assert.True(t.ResolvedFiles[0].ItemSpec.Contains("Microsoft.Build.Engine")); // "Expected Engine to resolve."
                e.AssertLogContains("MSB3252");
            }
            finally
            {
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    Directory.Delete(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// When targeting a profile make sure that we do not resolve the assembly if we reference something from the full framework which is in the GAC.
        /// This will cover the same where we are referencing a full framework assembly.
        /// </summary>
        [Fact]
        public void VerifyAssemblyInGacButNotInProfileIsNotResolved()
        {
            // Create a generic redist list with system.xml and microsoft.build.engine.
            string profileRedistList = String.Empty;
            string fullRedistList = String.Empty;
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "VerifyAssemblyInGacButNotInProfileIsNotResolved");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles\\Client");
            useFrameworkFileExists = true;
            string fullRedistListContents =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='System' Version='9.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='Neutral'/>" +
            "</FileList >";

            try
            {
                GenerateRedistAndProfileXmlLocations(fullRedistListContents, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine();
                t.BuildEngine = e;
                TaskItem item = new TaskItem(@"DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
                t.Assemblies = new ITaskItem[] { item };
                t.SearchPaths = new string[] { @"c:\MyComponents\4.0Component\", "{GAC}" };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.FullFrameworkFolders = new string[] { fullFrameworkDirectory };
                t.LatestTargetFrameworkDirectories = new string[] { fullFrameworkDirectory };
                t.ProfileName = "Client";
                t.TargetFrameworkMoniker = ".NETFramework, Version=4.0";

                bool success = Execute(t, false);
                Console.Out.WriteLine(e.Log);
                Assert.True(success); // "Expected no errors."
                Assert.Equal(0, t.ResolvedFiles.Length); // "Expected no files to resolved."
                string warningMessage = t.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", "DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089", "SysTem, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.TargetFrameworkMoniker);
                e.AssertLogContains(warningMessage);
            }
            finally
            {
                useFrameworkFileExists = false;
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    Directory.Delete(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// Make sure when reading in the full framework redist list or when reading in the white list xml files. 
        /// Errors in reading the file should be logged as warnings and no assemblies should be excluded.
        /// 
        /// </summary>
        [Fact]
        public void VerifyProfileErrorsAreLogged()
        {
            // Create a generic redist list with system.xml and microsoft.build.engine.
            string profileRedistList = String.Empty;
            string fullRedistList = String.Empty;
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "VerifyProfileErrorsAreLogged");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles\\Client");
            try
            {
                string fullRedistListContentsErrors =
                  "<FileList Redist='Microsoft-Windows-CLRCoreComp'>" +
                        "File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' >" +
                        "File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' >" +
                   "";

                GenerateRedistAndProfileXmlLocations(fullRedistListContentsErrors, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine();
                t.BuildEngine = e;
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}", fullFrameworkDirectory };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;

                ITaskItem item = new TaskItem(fullRedistList);
                item.SetMetadata("FrameworkDirectory", Path.GetDirectoryName(fullRedistList));
                t.FullFrameworkAssemblyTables = new ITaskItem[] { item };
                t.ProfileName = "Client";

                string microsoftBuildEnginePath = Path.Combine(fullFrameworkDirectory, "Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(targetFrameworkDirectory, "System.Xml.dll");

                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);
                Assert.True(success); // "Expected errors."
                Assert.Equal(2, t.ResolvedFiles.Length); // "Expected two resolved assembly."
                e.AssertLogContains("MSB3263");
            }
            finally
            {
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    Directory.Delete(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// Generate the full framework and profile redist list directories and files
        /// </summary>
        private static void GenerateRedistAndProfileXmlLocations(string fullRedistContents, string profileListContents, out string profileRedistList, out string fullRedistList, string fullFrameworkDirectory, string targetFrameworkDirectory)
        {
            fullRedistList = Path.Combine(fullFrameworkDirectory, "RedistList\\FrameworkList.xml");
            string redistDirectory = Path.GetDirectoryName(fullRedistList);
            if (Directory.Exists(redistDirectory))
            {
                Directory.Delete(redistDirectory);
            }

            Directory.CreateDirectory(redistDirectory);

            File.WriteAllText(fullRedistList, fullRedistContents);

            profileRedistList = Path.Combine(targetFrameworkDirectory, "RedistList\\FrameworkList.xml");

            redistDirectory = Path.GetDirectoryName(profileRedistList);
            if (Directory.Exists(redistDirectory))
            {
                Directory.Delete(redistDirectory);
            }

            Directory.CreateDirectory(redistDirectory);

            File.WriteAllText(profileRedistList, profileListContents);
        }
    }

    /// <summary>
    /// Unit tests for the ResolveAssemblyReference GlobalAssemblyCache.
    /// </summary>
    sealed public class GlobalAssemblyCacheTests : ResolveAssemblyReferenceTestFixture
    {
        private const string system4 = "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
        private const string system2 = "System, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
        private const string system1 = "System, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
        private const string systemNotStrong = "System, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null";

        private const string system4Path = "c:\\clr4\\System.dll";
        private const string system2Path = "c:\\clr2\\System.dll";
        private const string system1Path = "c:\\clr2\\System1.dll";

        private GetAssemblyRuntimeVersion _runtimeVersion = new GetAssemblyRuntimeVersion(MockGetRuntimeVersion);
        private GetPathFromFusionName _getPathFromFusionName = new GetPathFromFusionName(MockGetPathFromFusionName);
        private GetGacEnumerator _gacEnumerator = new GetGacEnumerator(MockAssemblyCacheEnumerator);

        /// <summary>
        /// Verify when the GAC enumerator returns
        /// 
        /// System, Version=4.0.0.0  Runtime=4.0xxxx
        /// System, Version=2.0.0.0  Runtime=2.0xxxx
        /// System, Version=1.0.0.0  Runtime=2.0xxxx
        /// 
        /// And we target 2.0 runtime that we get the Version 2.0.0.0 system.
        /// 
        /// This test two aspects. First that we get the correct runtime, second that we get the highest version for that assembly in the runtime.
        /// </summary>
        [Fact]
        public void VerifySimpleNamev2057020()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("2.0.57027"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, false);
            Assert.NotNull(path);
            Assert.True(path.Equals(system2Path, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// Verify when the GAC enumerator returns
        /// 
        /// System, Version=4.0.0.0  Runtime=4.0xxxx
        /// System, Version=2.0.0.0  Runtime=2.0xxxx
        /// System, Version=1.0.0.0  Runtime=2.0xxxx
        /// 
        /// And we target 2.0 runtime that we get the Version 2.0.0.0 system.
        /// 
        /// Verify that by setting the wants sspecific version to true that we will return the highest version when only the simple name is used. 
        /// Essentially specific version for the gac resolver means do not filter by runtime.
        /// </summary>
        [Fact]
        public void VerifySimpleNamev2057020SpecificVersion()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("2.0.0"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            Assert.NotNull(path);
            Assert.True(path.Equals(system4Path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when the GAC enumerator returns
        /// 
        /// System, Version=2.0.0.0  Runtime=2.0xxxx
        /// System, Version=1.0.0.0  Runtime=2.0xxxx
        /// 
        /// And we target 2.0 runtime that we get the Version 2.0.0.0 system.
        /// 
        /// Verify that by setting the wants sspecific version to true that we will return the highest version when only the simple name is used. 
        /// Essentially specific version for the gac resolver means do not filter by runtime.
        /// </summary>
        [Fact]
        public void VerifyFusionNamev2057020SpecificVersion()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, Version=2.0.0.0");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("2.0.0"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            Assert.NotNull(path);
            Assert.True(path.Equals(system2Path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when the GAC enumerator returns
        /// 
        /// System, Version=4.0.0.0  Runtime=4.0xxxx
        /// System, Version=2.0.0.0  Runtime=2.0xxxx
        /// System, Version=1.0.0.0  Runtime=2.0xxxx
        /// 
        /// And we target 4.0 runtime that we get the Version 4.0.0.0 system.
        /// 
        /// This test two aspects. First that we get the correct runtime, second that we get the highest version for that assembly in the runtime.
        /// </summary>
        [Fact]
        public void VerifySimpleNamev40()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("4.0.0"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, false);
            Assert.NotNull(path);
            Assert.True(path.Equals(system4Path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when the GAC enumerator returns
        /// 
        /// System, Version=4.0.0.0  Runtime=4.0xxxx
        /// System, Version=2.0.0.0  Runtime=2.0xxxx
        /// System, Version=1.0.0.0  Runtime=2.0xxxx
        /// 
        /// And we target 4.0 runtime that we get the Version 4.0.0.0 system.
        /// 
        /// Verify that by setting the wants sspecific version to true that we will return the highest version when only the simple name is used. 
        /// Essentially specific version for the gac resolver means do not filter by runtime.
        /// </summary>
        [Fact]
        public void VerifySimpleNamev40SpecificVersion()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("4.0.0"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            Assert.NotNull(path);
            Assert.True(path.Equals(system4Path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when the GAC enumerator returns
        /// 
        /// System, Version=4.0.0.0  Runtime=4.0xxxx
        /// 
        /// 
        /// Verify that by setting the wants sspecific version to true that we will return the highest version when only the simple name is used. 
        /// Essentially specific version for the gac resolver means do not filter by runtime.
        /// </summary>
        [Fact]
        public void VerifyFusionNamev40SpecificVersion()
        {
            // We want to pass a very generic name to get the correct gac entries.
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, Version=4.0.0.0");


            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _runtimeVersion, new Version("4.0.0.0"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            Assert.NotNull(path);
            Assert.True(path.Equals(system4Path, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when a assembly name is passed in which has the public key explicitly set to null that we return null as the assembly cannot be in the gac. 
        /// </summary>
        [Fact]
        public void VerifyEmptyPublicKeyspecificVersion()
        {
            Assert.Throws<FileLoadException>(() =>
            {
                AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=");
                string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, getRuntimeVersion, new Version("2.0.50727"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            }
           );
        }

        /// <summary>
        /// Verify when a assembly name is passed in which has the public key explicitly set to null that we return null as the assembly cannot be in the gac. 
        /// </summary>
        [Fact]
        public void VerifyNullPublicKey()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=null");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, getRuntimeVersion, new Version("2.0.50727"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, false);
            Assert.Null(path);
        }

        /// <summary>
        /// Verify when a assembly name is passed in which has the public key explicitly set to null that we return null as the assembly cannot be in the gac. 
        /// </summary>
        [Fact]
        public void VerifyNullPublicKeyspecificVersion()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=null");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, getRuntimeVersion, new Version("2.0.50727"), false, new FileExists(MockFileExists), _getPathFromFusionName, _gacEnumerator, true);
            Assert.Null(path);
        }


        /// <summary>
        /// When a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
        /// this was causing the GAC (api's) to crash.
        /// </summary>
        [Fact]
        public void VerifyProcessorArchitectureDoesNotCrash()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, getRuntimeVersion, new Version("2.0.50727"), false, new FileExists(MockFileExists), _getPathFromFusionName, null /* use the real gac enumerator*/, false);
            Assert.Null(path);
        }

        /// <summary>
        /// When a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
        /// this was causing the GAC (api's) to crash.
        /// </summary>
        [Fact]
        public void VerifyProcessorArchitectureDoesNotCrashSpecificVersion()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, getRuntimeVersion, new Version("2.0.50727"), false, new FileExists(MockFileExists), _getPathFromFusionName, null /* use the real gac enumerator*/, true);
            Assert.Null(path);
        }

        /// <summary>
        /// See bug 648678,  when a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
        /// this was causing the GAC (api's) to crash.
        /// </summary>
        [Fact]
        public void VerifyProcessorArchitectureDoesNotCrashFullFusionName()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, getRuntimeVersion, new Version("2.0.50727"), true, new FileExists(MockFileExists), _getPathFromFusionName, null /* use the real gac enumerator*/, false);
            Assert.Null(path);
        }

        /// <summary>
        /// When a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
        /// this was causing the GAC (api's) to crash.
        /// </summary>
        [Fact]
        public void VerifyProcessorArchitectureDoesNotCrashFullFusionNameSpecificVersion()
        {
            AssemblyNameExtension fusionName = new AssemblyNameExtension("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, getRuntimeVersion, new Version("2.0.50727"), true, new FileExists(MockFileExists), _getPathFromFusionName, null /* use the real gac enumerator*/, true);
            Assert.Null(path);
        }


        // System.Runtime dependency calculation tests

        // No dependency
        [Fact]
        public void SystemRuntimeDepends_No_Build()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Regular"),
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\SystemRuntime\Regular.dll");

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;
            Assert.True(t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "false", StringComparison.OrdinalIgnoreCase)); //                 "Expected no System.Runtime dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "false", StringComparison.OrdinalIgnoreCase)); //                 "Expected no System.Runtime dependency found during intellibuild."
        }


        // Direct dependency
        [Fact]
        public void SystemRuntimeDepends_Yes()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("System.Runtime"),
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\SystemRuntime\System.Runtime.dll");

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;

            Assert.True(t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during intellibuild."
        }

        // Indirect dependency
        [Fact]
        public void SystemRuntimeDepends_Yes_Indirect()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine();

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Portable"),
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\SystemRuntime\Portable.dll");

            t.SearchPaths = DefaultPaths;

            // build mode
            t.FindDependencies = true;

            Assert.True(t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during build."

            // intelli build mode
            t.FindDependencies = false;
            Assert.True(t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader));

            Assert.True(string.Equals(t.DependsOnSystemRuntime, "true", StringComparison.OrdinalIgnoreCase)); //                 "Expected System.Runtime dependency found during intellibuild."
        }

        #region HelperDelegates

        private static string MockGetRuntimeVersion(string path)
        {
            if (path.Equals(system1Path, StringComparison.OrdinalIgnoreCase))
            {
                return "v2.0.50727";
            }

            if (path.Equals(system4Path, StringComparison.OrdinalIgnoreCase))
            {
                return "v4.0.0";
            }

            if (path.Equals(system2Path, StringComparison.OrdinalIgnoreCase))
            {
                return "v2.0.50727";
            }

            return String.Empty;
        }

        private bool MockFileExists(string path)
        {
            return true;
        }

        private static string MockGetPathFromFusionName(string strongName)
        {
            if (strongName.Equals(system1, StringComparison.OrdinalIgnoreCase))
            {
                return system1Path;
            }

            if (strongName.Equals(system2, StringComparison.OrdinalIgnoreCase))
            {
                return system2Path;
            }

            if (strongName.Equals(systemNotStrong, StringComparison.OrdinalIgnoreCase))
            {
                return system2Path;
            }

            if (strongName.Equals(system4, StringComparison.OrdinalIgnoreCase))
            {
                return system4Path;
            }

            return String.Empty;
        }

        private static IEnumerable<AssemblyNameExtension> MockAssemblyCacheEnumerator(string strongName)
        {
            List<string> listOfAssemblies = new List<string>();

            if (strongName.StartsWith("System, Version=2.0.0.0", StringComparison.OrdinalIgnoreCase))
            {
                listOfAssemblies.Add(system2);
            }
            else if (strongName.StartsWith("System, Version=4.0.0.0", StringComparison.OrdinalIgnoreCase))
            {
                listOfAssemblies.Add(system4);
            }
            else
            {
                listOfAssemblies.Add(system1);
                listOfAssemblies.Add(system2);
                listOfAssemblies.Add(system4);
            }
            return new MockEnumerator(listOfAssemblies);
        }

        internal class MockEnumerator : IEnumerable<AssemblyNameExtension>
        {
            private List<string> _assembliesToEnumerate = null;
            private List<string>.Enumerator _enumerator;

            public MockEnumerator(List<string> assembliesToEnumerate)
            {
                _assembliesToEnumerate = assembliesToEnumerate;

                _enumerator = assembliesToEnumerate.GetEnumerator();
            }


            public IEnumerator<AssemblyNameExtension> GetEnumerator()
            {
                foreach (string assembly in _assembliesToEnumerate)
                {
                    yield return new AssemblyNameExtension(assembly);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return (IEnumerator)GetEnumerator();
            }
        }

        #endregion
    }
}
