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
        None = 0,
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
        public int Id;
        public FancyLoggerNodeType Type; // TODO: Maybe remove
        public BuildEventArgs? Args = null;
        public Dictionary<int, FancyLoggerNode> Children = new();

        public FancyLoggerNode(int id, FancyLoggerNodeType type)
        {
            Id = id;
            Type = type;
        }
        public FancyLoggerNode(int id, FancyLoggerNodeType type, BuildEventArgs? args) : this(id, type) { Args = args; }

        public void Add(FancyLoggerNode node)
        {
            Children.Add(node.Id, node);
        }
        public void Add(int id, FancyLoggerNodeType type)
        {
            FancyLoggerNode node = new FancyLoggerNode(id, type);
            Add(node);
        }
        public FancyLoggerNode? Find(int id, FancyLoggerNodeType type)
        {
            // If id is self
            if (Id == id && Type == type) return this;
            // If not self and no children
            if (Children.Count == 0) return null;
            // Find in all children
            foreach (var child in Children)
            {
                FancyLoggerNode? node = child.Value.Find(id, type);
                if (node != null) return node;
            }
            return null;
        }
    }
}
