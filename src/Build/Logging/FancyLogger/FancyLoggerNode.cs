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
        public FancyLoggerNode? Parent;
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
        public FancyLoggerNode? Find(string id)
        {
            // If self
            if(Id == id) return this;
            // If no children
            if(Children.Count == 0) return null;
            // Iterate
            foreach (var child in Children)
            {
                FancyLoggerNode? node = child.Value.Find(id);
                if (node != null) return node;
            }
            return null;
        }

        public void Add(FancyLoggerNode node)
        {
            Children.Add(node.Id, node);
            node.Depth = Depth + 1;
            node.Parent = this;
        }

        public int GetLastLineIndex()
        {
            // If no line, return -1
            if (Line == null) return -1;
            // Get line index and id
            int lastLineIndex = FancyLoggerBuffer.GetLineIndexById(Line.Id);
            int lastLineId = Line.Id;
            if (lastLineIndex == -1) return -1;
            // Get max of children
            foreach (var child in Children)
            {
                int childLastLineIndex = child.Value.GetLastLineIndex();
                if (childLastLineIndex > lastLineIndex)
                {
                    lastLineIndex = childLastLineIndex;
                    lastLineId = child.Value.Line!.Id;
                }
            }
            return lastLineIndex;
        }

        public void Write()
        {
            if (Line == null) { return; }
            // Adjust identation
            Line.IdentationLevel = Depth - 1;
            // If line not in the buffer, add
            if (FancyLoggerBuffer.GetLineIndexById(Line.Id) == -1)
            {
                // Get parent last line index
                if (Parent != null)
                {
                    int parentLastLineId = Parent.GetLastLineIndex();
                    if (parentLastLineId == -1) throw new Exception("Oops something went wrong");
                    Line.Text += $"  --> {parentLastLineId}";
                    // FancyLoggerBuffer.WriteNewLineAfter(Line, parentLastLineId);
                    FancyLoggerBuffer.WriteNewLineAfterIndex(Line, parentLastLineId);
                }
            }
        }

        public void Collapse()
        {
            foreach (var child in Children)
            {
                if (child.Value.Line == null) continue;
                FancyLoggerBuffer.HideLine(child.Value.Line.Id);
                child.Value.Collapse();
            }
        }

        public void Expand()
        {
            foreach (var child in Children)
            {
                if (child.Value.Line == null) continue;
                FancyLoggerBuffer.UnhideLine(child.Value.Line.Id);
                child.Value.Expand();
            }
        }

        /*public void Collapse(bool isRoot)
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
            if (isRoot) return;
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
        }*/
    }
}
