// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
