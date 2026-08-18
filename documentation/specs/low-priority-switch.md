
# Low Priority Nodes in MSBuild and Visual Studio

## Problem Summary

When doing other work, it can be useful for builds (which often take a long time and consume a lot of resources) to happen in the background, allowing other work to happen in the interim. This is true for both command line builds and builds within Visual Studio.

Visual Studio, on the other hand, should always run at normal priority. This ensures that users can continue to interact with its other features, most notably editing code and seeing intellisense and autocomplete pop up.

## High Level Design

### Requirements

1. A long-lived process can execute a series of builds divided between Normal and BelowNormal priority.
2. Transitions between a build at Normal priority and one at BelowNormal priority (and vice versa) are fairly efficient, at least on Windows but ideally on all operating systems.
3. NodeReuse is still possible. That is, another process can (often) use nodes from the long-lived process if NodeReuse is true.
4. Any reused nodes are at the priority they themselves specify. Normal priority nodes are actually at normal priority, and low priority nodes are actually at BelowNormal priority.
5. All nodes are at the priority they should be when being used to build even if a normal priority process had connected to them as normal priority worker nodes, and they are now executing a low priority build.

## Non-goals

Perfect parity between windows and mac or linux. Windows permits processes to raise their own priority or that of another process, whereas other operating systems do not. This is very efficient, so we should use it. As we expect this feature to be used in Visual Studio, we anticipate it being less used on mac and linux, hence not being as high priority to make it just as efficient.

## Details

Each node (including worker nodes) initially takes its priority from its parent process. Since we now need the priority to align with what it is passed instead of its parent, attempt to adjust priority afterwards if necessary as part of node startup.

BuildManager.cs remembers the priority of the previous build it had executed. If that was set to a value that differs from the priority of the current build:

1. On windows or when decreasing the priority: lowers the priority of all connected nodes
2. On linux and mac when increasing the priority: disconnects from all nodes.

When a worker node disconnects from the entrypoint node, it should ensure that it is the priority that would be expected by nodes that successfully connect to it. That means that it should be normal priority if lowPriority is false and BelowNormal priority otherwise.

For this reason, if we intend to reuse a node, we check its priority and adjust it to the expected value if possible. If it proves impossible to adjust to the correct priority, the node shuts down.
