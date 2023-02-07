// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildEngine
{
    internal interface ITaskRegistry
    {
        void RegisterTask(UsingTask usingTask, Expander expander, EngineLoggingServices loggingServices, BuildEventContext context);
        bool GetRegisteredTask(string taskName, string taskProjectFile, XmlNode taskNode, bool exactMatchRequired, EngineLoggingServices loggingServices, BuildEventContext context, out LoadedType taskClass);
        void Clear();
    }
}
