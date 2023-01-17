// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

#nullable disable

using System;

namespace Microsoft.Build.Framework
{
    [Serializable]
    public class AssemblyLoadBuildEventArgs : BuildMessageEventArgs // or LazyFormattedBuildEventArgs?
    {
        public AssemblyLoadBuildEventArgs()
        { }

        public AssemblyLoadBuildEventArgs(
            string assemblyName,
            string assemblyPath,
            Guid mvid,
            string message,
            string helpKeyword = null,
            string senderName = null,
            MessageImportance importance = MessageImportance.Low)
            : base(message, helpKeyword, senderName, importance/*, DateTime.UtcNow, assemblyName, assemblyPath, mvid*/)
        {
            AssemblyName = assemblyName;
            AssemblyPath = assemblyPath;
            MVID = mvid;
        }

        public string AssemblyName { get; private set; }
        public string AssemblyPath { get; private set; }
        public Guid MVID { get; private set; }
    }
}
