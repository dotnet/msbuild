// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace Microsoft.DotNet.Watcher.Tools
{
    [EventSource(Name = "HotReload")]
    class HotReloadEventSource : EventSource
    {

        public enum StartType
        {
            Main,
            StaticHandler,
            CompilationHandler,
            ScopedCssHandler
        }

        public class Keywords
        {
            public const EventKeywords Perf = (EventKeywords)1;
        }

        [Event(1, Message = "Hot reload started for {0}", Level = EventLevel.Informational, Keywords = Keywords.Perf)]
        public void HotReloadStart(StartType handlerType) { WriteEvent(1, handlerType); }

        [Event(2, Message = "Hot reload finished for {0}", Level = EventLevel.Informational, Keywords = Keywords.Perf)]
        public void HotReloadEnd(StartType handlerType) { WriteEvent(2, handlerType); }

        public static readonly HotReloadEventSource Log = new HotReloadEventSource();
    }
}