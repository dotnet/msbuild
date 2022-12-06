// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.FancyLogger
{
    // TODO: Maybe remove
    public enum FancyLoggerNodeType
    {
        None,
        Build,
        Project,
        Target,
        Task,
        Message,
        Warning,
        Error
    }

    public class FancyLoggerNode
    {
        public string Id;
        public Dictionary<string, FancyLoggerNode> Children = new Dictionary<string, FancyLoggerNode>();
        public FancyLoggerBufferLine? Line;
        public int Depth = 0;
        public FancyLoggerNode(string id)
        {
            Id = id;
        }
        public FancyLoggerNode(int id, FancyLoggerNodeType type)
        {
            switch (type)
            {
                case FancyLoggerNodeType.Build:
                    Id = $"build-{id}";
                    break;
                case FancyLoggerNodeType.Project:
                    Id = $"project-{id}";
                    break;
                case FancyLoggerNodeType.Target:
                    Id = $"target-{id}";
                    break;
                case FancyLoggerNodeType.Task:
                    Id = $"task-{id}";
                    break;
                case FancyLoggerNodeType.Message:
                    Id = $"message-{id}";
                    break;
                case FancyLoggerNodeType.Warning:
                    Id = $"warning-{id}";
                    break;
                case FancyLoggerNodeType.Error:
                    Id = $"error-{id}";
                    break;
                default:
                    Id = id.ToString(); break;
            }
        }


        public void Collapse(bool isRoot)
        {
            // Children
            foreach (var child in Children)
            {
                child.Value.Collapse(false);
            }
            // Self
            if (!isRoot) Line?.Hide();
        }

        public void Expand(bool isRoot)
        {
            foreach (var child in Children)
            {
                child.Value.Expand(false);
            }
            // Self
            if (!isRoot) Line?.Unhide();
        }
        public int GetRootLineId()
        {
            if (Line == null) return -1;
            return FancyLoggerBuffer.GetLineIndexById(Line.Id);
        }
        public int GetLastLineId()
        {
            if (Line == null) return -1;
            if (Children.Count == 0) return FancyLoggerBuffer.GetLineIndexById(Line.Id);
            int lastLineId = -1;
            int lastLineIndex = -1;
            foreach (var child in Children)
            {
                int lineIndex = child.Value.GetLastLineId();
                if (lineIndex > lastLineIndex)
                {
                    lastLineIndex = lineIndex;
                    lastLineId = Line.Id;
                }
            }
            return lastLineId;
        }
    }
}
