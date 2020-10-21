// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    #region Base Command Class
    /// <summary>
    /// Base class for classes which wrap operations that should be executed on the engine thread
    /// </summary>
    internal class EngineCommand
    {
        internal virtual void Execute(Engine parentEngine)
        {
            ErrorUtilities.VerifyThrow(false, "Should overwrite the execute method");
        }
    }
    #endregion

    #region RequestStatus
    /// <summary>
    /// Wrapper class for a node status request
    /// </summary>
    internal class RequestStatusEngineCommand : EngineCommand
    {
        internal RequestStatusEngineCommand(int requestId)
        {
            this.requestId = requestId;
        }

        internal override void Execute(Engine parentEngine)
        {
            NodeStatus nodeStatus = parentEngine.RequestStatus(requestId);
            ErrorUtilities.VerifyThrow(parentEngine.Router.ParentNode != null,
                                       "Method should be called only on child nodes");
            parentEngine.Router.ParentNode.PostStatus(nodeStatus, false /* don't block waiting on the send */);
        }

        #region Data
        private int requestId;
        #endregion
    }
    #endregion

    #region HostBuildRequestCompletion
    /// <summary>
    /// Wrapper class for a reporting completion of a host build request to the engine
    /// </summary>
    internal class HostBuildRequestCompletionEngineCommand : EngineCommand
    {
        internal HostBuildRequestCompletionEngineCommand()
        {
        }

        internal override void Execute(Engine parentEngine)
        {
            parentEngine.DecrementProjectsInProgress();
        }

        #region Data
        #endregion
    }
    #endregion

    #region ReportException
    /// <summary>
    /// Wrapper class for a reporting completion of a host build request to the engine
    /// </summary>
    internal class ReportExceptionEngineCommand : EngineCommand
    {
        internal ReportExceptionEngineCommand(Exception e)
        {
            this.e = e;
        }

        internal override void Execute(Engine parentEngine)
        {
            // Figure out if the exception occurred on a parent or child engine
            // On the parent rethrow nicely and make sure the finallies run
            // On the child try to communicate with the parent - if success, exit
            // if failure rethrow and hope Watson will pick the exception up
            string message = ResourceUtilities.FormatResourceString("RethrownEngineException");
            throw new Exception(message, e);
        }

        #region Data
        private Exception e;
        #endregion
    }
    #endregion

   #region ChangeTraversalType
    /// <summary>
    /// Wrapper class for a changing the traversal approach used by the TEM
    /// </summary>
    internal class ChangeTraversalTypeCommand : EngineCommand
    {
        /// <summary>
        /// Create a command that will switch the traversal of the system to breadthFirst traversal or depth first traveral. 
        /// changeLocalTraversalOnly is used to determine whether or not to change the traversal for the whole system or only the current node. 
        /// changeLocalTraversalOnly is set to true in the when a node is first started and in the updateNodeSettings method as these traversal changes are for the local node only. The reason 
        /// is because updateNodeSettings is called when the parent has told the node to switch traversal types, there is no need to forward the change to the engine again.
        /// Also, when a node starts up it is set to breadth first traversal, this is the default so the parent engine need not be notified of this change.
        /// </summary>
        internal ChangeTraversalTypeCommand(bool breadthFirstTraversal, bool changeLocalTraversalOnly)
        {
            this.breadthFirstTraversal = breadthFirstTraversal;
            this.changeLocalTraversalOnly = changeLocalTraversalOnly;
        }

        internal override void Execute(Engine parentEngine)
        {
            parentEngine.NodeManager.TaskExecutionModule.UseBreadthFirstTraversal = breadthFirstTraversal;
            if (!parentEngine.Router.ChildMode)
            {
                parentEngine.NodeManager.ChangeNodeTraversalType(breadthFirstTraversal);
            }
            else
            {
                if (!changeLocalTraversalOnly)
                {
                    parentEngine.Router.ParentNode.PostStatus(new NodeStatus(breadthFirstTraversal), false /* don't block waiting on the send */);
                }
            }
        }

        #region Data
        private bool breadthFirstTraversal;
        private bool changeLocalTraversalOnly;
        #endregion
    }
    #endregion
}
