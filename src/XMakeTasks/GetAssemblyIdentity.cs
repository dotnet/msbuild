// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Sniffs input files for their assembly identities, and outputs a set of items with the identity information.
    /// </summary>
    /// <comment>
    ///  Input:  Assembly Include="foo.exe"
    ///  Output: Identity Include="Foo, Version=1.0.0.0", Name="Foo, Version="1.0.0.0"
    /// </comment>
    public class GetAssemblyIdentity : TaskExtension
    {
        private ITaskItem[] _assemblyFiles;
        private ITaskItem[] _assemblies;

        [Required]
        public ITaskItem[] AssemblyFiles
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentNull(_assemblyFiles, "assemblyFiles");
                return _assemblyFiles;
            }
            set { _assemblyFiles = value; }
        }

        [Output]
        public ITaskItem[] Assemblies
        {
            get { return _assemblies; }
            set { _assemblies = value; }
        }

        private static string ByteArrayToHex(Byte[] a)
        {
            if (a == null)
                return null;
            StringBuilder s = new StringBuilder(a.Length);
            foreach (Byte b in a)
                s.Append(b.ToString("X02", CultureInfo.InvariantCulture));
            return s.ToString();
        }

        public override bool Execute()
        {
            ArrayList list = new ArrayList();
            foreach (ITaskItem item in AssemblyFiles)
            {
                AssemblyName an;
                try
                {
                    an = AssemblyName.GetAssemblyName(item.ItemSpec);
                }
                catch (BadImageFormatException e)
                {
                    Log.LogErrorWithCodeFromResources("GetAssemblyIdentity.CouldNotGetAssemblyName", item.ItemSpec, e.Message);
                    continue;
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    Log.LogErrorWithCodeFromResources("GetAssemblyIdentity.CouldNotGetAssemblyName", item.ItemSpec, e.Message);
                    continue;
                }

                ITaskItem newItem = new TaskItem(an.FullName);
                newItem.SetMetadata("Name", an.Name);
                if (an.Version != null)
                    newItem.SetMetadata("Version", an.Version.ToString());
                if (an.GetPublicKeyToken() != null)
                    newItem.SetMetadata("PublicKeyToken", ByteArrayToHex(an.GetPublicKeyToken()));
                if (an.CultureInfo != null)
                    newItem.SetMetadata("Culture", an.CultureInfo.ToString());
                item.CopyMetadataTo(newItem);
                list.Add(newItem);
            }
            Assemblies = (ITaskItem[])list.ToArray(typeof(ITaskItem));
            return !Log.HasLoggedErrors;
        }
    }
}
