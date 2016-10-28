// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Abstractions;

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Internal.PatternContexts
{
    internal abstract class PatternContext<TFrame> : IPatternContext
    {
        private Stack<TFrame> _stack = new Stack<TFrame>();
        protected TFrame Frame;

        public virtual void Declare(Action<IPathSegment, bool> declare) { }

        public abstract PatternTestResult Test(FileInfoBase file);

        public abstract bool Test(DirectoryInfoBase directory);

        public abstract void PushDirectory(DirectoryInfoBase directory);

        public virtual void PopDirectory()
        {
            Frame = _stack.Pop();
        }

        protected void PushDataFrame(TFrame frame)
        {
            _stack.Push(Frame);
            Frame = frame;
        }

        protected bool IsStackEmpty()
        {
            return _stack.Count == 0;
        }
    }
}