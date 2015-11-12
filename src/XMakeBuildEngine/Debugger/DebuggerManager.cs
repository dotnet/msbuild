// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Provides debugging support for state machines.</summary>
//-----------------------------------------------------------------------

#if FEATURE_MSBUILD_DEBUGGER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
#if FEATURE_DEBUGGER
using System.Diagnostics.SymbolStore;
#endif

namespace Microsoft.Build.Debugging
{
    /// <summary>
    /// Manager for supporting debugging a state machine.   
    /// This is for internal use by MSBuild, only.
    /// </summary>    
    /// <comment>
    /// This is using the theory described at: 
    ///  http://blogs.msdn.com/jmstall/archive/2005/07/27/state-machine-theory.aspx. 
    /// The summary is that it emits IL snippets ("Islands") for each state in the machine.
    /// The island serves as a spot to set a breakpoint. 
    ///
    /// You should be able to set breakpoints on states and hit them.
    /// To do stepping between states:
    ///   - ensure the interpreter is non-user code (perhaps by placing the [DebuggerNonUserCode] attribute
    ///      on all the classes, or not providing the symbols to the debugger)
    ///   - ensure Just-My-Code is turned on. In VS2005, 
    ///       this is at: Tools > Options  > Debugging > General, "Enable Just My Code".
    ///  - Use step-in (F11) between states.
    /// 
    /// 
    /// The general usage is to call:
    ///  - DefineState() for each state
    ///  - Bake() once you've defined all the states you need to enter.
    ///  - EnterState() / LeaveState() for each state. 
    /// You can Define new states and bake them, such as if the script loads a new file.
    /// Baking is expensive, so it's best to define as many states in each batch.
    ///
    /// UNDONE: Show proper state of items and properties set and modified within targets.
    /// UNDONE: Characterization and fixing of debugging multiproc MSBuild, and MSBuild hosted by VS.
    /// </comment>
#if JMC
    [DebuggerNonUserCode]
#endif
    public static class DebuggerManager
    {
        /// <summary>
        /// Whether debugging should break on startup.
        /// This is normally true, but setting it to false 
        /// might be useful in some situations, such as multiproc build.
        /// </summary>
        private static bool s_breakOnStartup;

        /// <summary>
        /// Whether debugging is enabled. This is not normally
        /// enabled as it makes everything slow.
        /// </summary>
        private static bool? s_debuggingEnabled;

        /// <summary>
        /// The states that the debugger may be in, indexed
        /// by their location. All baked states are in here.
        /// </summary>
        private static IDictionary<ElementLocation, DebuggerState> s_allBakedStates = new Dictionary<ElementLocation, DebuggerState>();

        /// <summary>
        /// Method that islands call back to.
        /// </summary>
        private static MethodInfo s_islandCallback;

#if FEATURE_DEBUGGER
        /// <summary>
        /// Cached mapping of file path to symbol store documents
        /// </summary>
        private static Dictionary<string, ISymbolDocumentWriter> s_sources = new Dictionary<string, ISymbolDocumentWriter>(StringComparer.OrdinalIgnoreCase);
#endif

        /// <summary>
        /// The single dynamic module used.
        /// </summary>
        private static ModuleBuilder s_dynamicModule;

        /// <summary>
        /// Islands are executed on an auxiliary thread instead of the main thread.
        /// This gives a better default stepping experience (allows Step-in, step-over, step-out),
        /// and also allows unloading the islands (since the thread can be in a separate appdomain).
        /// </summary>
        private static IslandThread s_islandThread;

        /// <summary>
        /// List of all state that have been created with DefineState
        /// and are yet to be Baked into types.
        /// We use a hashtable instead of a list so that we can find duplicate
        /// state names immediately.
        /// </summary>
        private static Dictionary<string, DebuggerState> s_unbakedStates = new Dictionary<string, DebuggerState>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// In special cases, we ignore an EnterState, and increment this counter
        /// so that we can ignore the matching LeaveState.
        /// </summary>
        private static int s_skippedEnters;

        /// <summary>
        /// Whether the debugger manager has been initialized yet.
        /// </summary>
        private static bool s_initialized;

        /// <summary>
        /// Type of delegate used by the debugger worker thread to call back to invoke an island
        /// </summary>
        internal delegate void InvokeIslandDelegate(object argument, VirtualStackFrame stackFrame);

        /// <summary>
        /// Whether debugging of project files is enabled.
        /// By default it is not.
        /// </summary>
        internal static bool DebuggingEnabled
        {
            get
            {
                if (!s_debuggingEnabled.HasValue)
                {
                    s_debuggingEnabled = String.Equals(Environment.GetEnvironmentVariable("MSBUILDDEBUGGING"), "1", StringComparison.OrdinalIgnoreCase);
                }

                return s_debuggingEnabled.Value;
            }
        }

        /// <summary>
        /// Stop debugging thread.
        /// This may not necessarily unload islands or dynamic modules that were created until the calling appdomain has exited.
        /// UNDONE: Call this. Otherwise we still exit cleanly, just only when the process exits.
        /// </summary>
        internal static void Terminate()
        {
            ErrorUtilities.VerifyThrow(s_initialized, "Not initialized");
            ErrorUtilities.VerifyThrow(DebuggingEnabled, "Debugging not enabled");

            if (s_islandThread != null)
            {
                s_islandThread.Exit();
                s_islandThread = null;
            }
        }

        /// <summary>
        /// Declare a new state associated with the given source location.
        /// States should (probably) have disjoint source locations.
        /// Define as many states as possible using this method before calling Bake().
        /// Location must map to a unique state within the type in which it is baked.
        /// Name of the state will showup in the callstack as if it was a method name. Must be unique within the type in which it is baked.
        /// Early-bound locals are arbitrary types whose values will be supplied on EnterState. May be null.
        /// </summary>
        internal static void DefineState(ElementLocation location, string name, ICollection<DebuggerLocalType> earlyLocals)
        {
            ErrorUtilities.VerifyThrow(s_initialized, "Not initialized");
            ErrorUtilities.VerifyThrow(DebuggingEnabled, "Debugging not enabled");

            // Special case: elements added by editing, such as in a solution wrapper project,
            // do not have line numbers. Such files cannot be debugged, so we special case
            // such locations by doing nothing.
            if (location.Line == 0)
            {
                return;
            }

            ErrorUtilities.VerifyThrow(!s_unbakedStates.ContainsKey(name), "Need unique debug state name, already seen '{0}'", name);

            DebuggerState state = new DebuggerState(location, name, earlyLocals);
            s_unbakedStates.Add(name, state);
        }

        /// <summary>
        /// Bake all unbaked states. States must be baked before calling EnterState().
        /// Islands are created in a type with the specified name.
        /// File name is to show up on the callstack as the type name: "ASSEMBLYNAME!FILENAME.STATENAME(...LOCALS...)"
        /// If the type name is not unique, it will be appended with a unique identifier.
        /// </summary>
        internal static void BakeStates(string fileName)
        {
            ErrorUtilities.VerifyThrow(s_initialized, "Not initialized");
            ErrorUtilities.VerifyThrow(DebuggingEnabled, "Debugging not enabled");

            // We may have baked no states if all states were in a file
            // for which we did not have detailed location information
            if (s_unbakedStates.Count == 0)
            {
                return;
            }

            // Default assembly name, eg., for unnamed projects
            fileName = fileName ?? "MSBuild";

            int suffix = 0;
            while (s_dynamicModule.GetType(fileName, false, false) != null)
            {
                fileName += suffix;
                suffix++;
            }

            TypeBuilder type = s_dynamicModule.DefineType(fileName, TypeAttributes.Public | TypeAttributes.Class);

            foreach (DebuggerState state in s_unbakedStates.Values)
            {
                if (s_allBakedStates.ContainsKey(state.Location))
                {
                    // This will happen if it is an import loaded by more than one project
                    continue;
                }

                string methodName = CreateIsland(type, state);

                state.RecordMethodInfo(type.CreateTypeInfo().AsType(), methodName);

                s_allBakedStates.Add(state.Location, state);
            }

            s_unbakedStates = new Dictionary<string, DebuggerState>(StringComparer.OrdinalIgnoreCase);

            // Although type is going out of scope now, it will
            // subsequently be accessed by its name
            type.CreateTypeInfo();
        }

        /// <summary>
        /// Enter a state and push it onto the 'virtual callstack'.
        /// If the user set a a breakpoint at the source location associated with 
        /// this state, this call will hit that breakpoint.
        /// Call LeaveState when the interpreter is finished with this state.
        /// State must have already been defined.
        /// </summary>
        /// <param name="location">
        /// Location of state to enter, used to look up the state.
        /// </param>
        /// <param name="locals">
        /// Local variables associated with this state, matching by index with the types
        /// passed into DefineState. The debugger will show the names, types, and values.
        /// </param>
        /// <remarks>
        /// EnterState can be called reentrantly. If code calls Enter(A); Enter(B); Enter(C); 
        /// Then on the call to Enter(C), the virtual callstack will be A-->B-->C. 
        /// Each call to Enter() will rebuild the virtual callstack. 
        /// </remarks>
        internal static void EnterState(ElementLocation location, IDictionary<string, object> locals)
        {
            ErrorUtilities.VerifyThrow(s_initialized, "Not initialized");
            ErrorUtilities.VerifyThrow(DebuggingEnabled, "Debugging not enabled");

            // Special case: elements added by editing, such as in a solution wrapper project,
            // do not have line numbers. Such files cannot be debugged, so we special case
            // such locations by doing nothing.
            if (location.Line == 0)
            {
                s_skippedEnters++;
                return;
            }

            DebuggerState state;
            ErrorUtilities.VerifyThrow(s_allBakedStates.TryGetValue(location, out state), "No state defined and baked for location {0}", location.LocationString);

            s_islandThread.EnterState(state, locals);
        }

        /// <summary>
        /// Enter and immediately leave a state, so that any breakpoint can be hit.
        /// </summary>
        internal static void PulseState(ElementLocation location, IDictionary<string, object> locals)
        {
            ErrorUtilities.VerifyThrow(s_initialized, "Not initialized");
            ErrorUtilities.VerifyThrow(DebuggingEnabled, "Debugging not enabled");

            EnterState(location, locals);
            LeaveState(location);
        }

        /// <summary>
        /// Break in the current state last set by EnterState(). 
        /// An interpreter could call this to
        /// implement a "data breakpoint".
        /// </summary>
        internal static void Break()
        {
            ErrorUtilities.VerifyThrow(s_initialized, "Not initialized");
            ErrorUtilities.VerifyThrow(DebuggingEnabled, "Debugging not enabled");

            s_islandThread.Break();
        }

        /// <summary>
        /// Pop the state most recently pushed by EnterState. 
        /// The identifier (location) of a Leave must match the Enter at the top of the stack,
        /// to catch mismatched Leaves.
        /// </summary>
        internal static void LeaveState(ElementLocation location)
        {
            ErrorUtilities.VerifyThrow(s_initialized, "Not initialized");
            ErrorUtilities.VerifyThrow(DebuggingEnabled, "Debugging not enabled");
            ErrorUtilities.VerifyThrow(s_skippedEnters >= 0, "Left too many");

            // Special case: elements added by editing, such as in a solution wrapper project,
            // do not have line numbers. Such files cannot be debugged, so we special case
            // such locations by doing nothing.
            if (s_skippedEnters > 0)
            {
                s_skippedEnters--;
                return;
            }

            s_islandThread.LeaveState(location);
        }

        /// <summary>
        /// Starts debugger worker thread immediately, if debugging is enabled.
        /// This must not be called by a static constructor, as the 
        /// time at which it is called will then be undefined, and
        /// the debugging environment variable might not have had a 
        /// chance to be set.
        /// </summary>
        internal static void Initialize()
        {
            if (s_islandThread == null)
            {
                if (DebuggingEnabled)
                {
                    Trace.WriteLine("MSBuild debugging enabled");

                    s_breakOnStartup = !String.Equals(Environment.GetEnvironmentVariable("MSBUILDDONOTBREAKONSTARTUP"), "1", StringComparison.OrdinalIgnoreCase);

                    CreateDynamicModule();

                    s_islandCallback = typeof(IslandThread).GetMethod("IslandWorker", BindingFlags.Static | BindingFlags.Public);
                    s_islandThread = new IslandThread(InvokeIsland /* delegate to invoke an island */, s_breakOnStartup);
                }
            }

            s_initialized = true;
        }

        /// <summary>
        /// Create the single dynamic module that will
        /// contain all our types and states.
        /// </summary>
        /// <remarks>
        /// Emits the module into the current appdomain.
        /// This could be improved to use another appdomain so that all
        /// the types could be unloaded. All locals would have to be 
        /// marshalable in this case.
        /// </remarks>
        private static void CreateDynamicModule()
        {
            // See http://blogs.msdn.com/jmstall/archive/2005/02/03/366429.aspx for a simple example
            // of debuggable reflection-emit.
            ErrorUtilities.VerifyThrow(s_dynamicModule == null, "Already emitted");

#if FEATURE_DEBUGGER
            // In a later release, this could be changed to use LightweightCodeGen (DynamicMethod instead of AssemblyBuilder); 
            // currently they don't support sequence points, so they can't be debugged in the normal way
            AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("msbuild"), AssemblyBuilderAccess.Run);

            // Mark generated code as debuggable. 
            // See http://blogs.msdn.com/rmbyers/archive/2005/06/26/432922.aspx for explanation.        
            ConstructorInfo constructor = typeof(DebuggableAttribute).GetConstructor(new Type[] { typeof(DebuggableAttribute.DebuggingModes) });

            DebuggableAttribute.DebuggingModes debuggingMode = DebuggableAttribute.DebuggingModes.DisableOptimizations |
                                                               DebuggableAttribute.DebuggingModes.Default;

            CustomAttributeBuilder attribute = new CustomAttributeBuilder(constructor, new object[] { debuggingMode });
            assembly.SetCustomAttribute(attribute);

            // Arbitrary but reasonable name
            string name = Process.GetCurrentProcess().ProcessName;
#if FEATURE_REFLECTION_EMIT_DEBUG_INFO
            s_dynamicModule = assembly.DefineDynamicModule(name, true /* track debug information */);
#else
            s_dynamicModule = assembly.DefineDynamicModule(name);
#endif
#endif
        }

        /// <summary>
        /// Create the representation of a single state, known as an "island".
        /// It is implemented as a small method in the type being baked into the dynamic module.
        /// Returns the name of the method.
        /// </summary>
        private static string CreateIsland(TypeBuilder typeBuilder, DebuggerState state)
        {
            // Parameters to the islands:
            // 1. Island thread
            // 2 ... N.  list of early bound locals.
            Type[] parameterTypes = new Type[1 + state.EarlyLocalsTypes.Count];
            parameterTypes[0] = typeof(IslandThread);

            int i = 1;
            foreach (DebuggerLocalType local in state.EarlyLocalsTypes)
            {
                parameterTypes[i] = local.Type;
                i++;
            }

            MethodBuilder method = typeBuilder.DefineMethod
                (
                state.Name /* method name */,
                MethodAttributes.Static | MethodAttributes.Public,
                typeof(void) /* return type */,
                parameterTypes
                );

            // Define the parameter names.
            // Do not define a parameter for the first parameter, as this is an
            // implementation detail and we want to hide it from VS.
            // Parameter 0 is the return type.
            int j = 2;
            foreach (DebuggerLocalType local in state.EarlyLocalsTypes)
            {
                method.DefineParameter(j, ParameterAttributes.None, local.Name);
                j++;
            }

            // Note that the locals are ignored by the method, they are only for the debugger to display;
            // only the thread parameter is passed on.

            // void MethodName(IslandThread thread, ... early locals ... )
            // {
            //    .line
            //     nop
            //     call Worker(thread)
            //     ret;
            // }
            ILGenerator generator = method.GetILGenerator();

#if FEATURE_DEBUGGER
            ISymbolDocumentWriter source;
            if (!s_sources.TryGetValue(state.Location.File, out source))
            {
                source = s_dynamicModule.DefineDocument(state.Location.File, Guid.Empty, Guid.Empty, Guid.Empty);
                s_sources.Add(state.Location.File, source);
            }

            // Lines may not be zero, columns may be zero
            int line = (state.Location.Line == 0) ? 1 : state.Location.Line;

            generator.MarkSequencePoint(source, line, state.Location.Column, line, Int32.MaxValue); // mapping to source file
            generator.Emit(OpCodes.Nop); // Can help with setting a breakpoint

            generator.Emit(OpCodes.Ldarg_0); // Load argument 0 that went to this method back onto the stack to pass to the call
            generator.EmitCall(OpCodes.Call, s_islandCallback /* method */, null /* no opt params */);

            generator.Emit(OpCodes.Ret); // Return from state
#endif

            return method.Name;
        }

        /// <summary>
        /// Invoke an "island", marshaling the arguments.
        /// Called on debugger worker thread.
        /// </summary>
        private static void InvokeIsland(Object islandThread, VirtualStackFrame frame)
        {
            Object[] arguments = new Object[1 + frame.State.EarlyLocalsTypes.Count];
            arguments[0] = islandThread;

            int i = 1;
            foreach (DebuggerLocalType localType in frame.State.EarlyLocalsTypes)
            {
                object value;
                ErrorUtilities.VerifyThrow(frame.Locals.TryGetValue(localType.Name, out value), "Didn't define value for {0}", localType.Name);

                arguments[i] = value;
                i++;
            }

            // ReflectionPermission perm = new ReflectionPermission(ReflectionPermissionFlag.MemberAccess);
            // perm.Assert();
            // frame.State.MethodInfo.Invoke(null /* no instance */, BindingFlags.NonPublic | BindingFlags.Static, null /* default binder */, args2, null /* default culture */);
            frame.State.Method.Invoke(null /* no instance */, arguments);
        }

        /// <summary>
        /// This is for internal use by MSBuild, only.
        /// </summary>
        /// <comment>
        /// Executes the islands on a dedicated worker thread. The worker thread's
        /// physical callstack then maps to the interpreter's virtual callstack.
        /// </comment>
#if JMC
        [DebuggerNonUserCode]
#endif
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "Working to avoid this being public")]
        public sealed class IslandThread : IDisposable
        {
            /// <summary>
            /// Callback used to enter an island
            /// </summary>
            private InvokeIslandDelegate _invokeIsland;

            /// <summary>
            /// Set to true to notify to Break on first instruction. This helps the F11 on startup experience.
            /// Since the islands are on a new thread, there may be no user code on the main thread and so 
            /// F11 doesn't work. Thus the new worker thread needs to fire some break event.
            /// This gets reset after the 'startup breakpoint'.
            /// The initial Properties can override this.
            /// </summary>
            private bool _breakOnStartup;

            /// <summary>
            /// Wrapped worker thread
            /// </summary>
            private Thread _workerThread;

            /// <summary>
            /// Signalled when the main thread wants to send an event to the debugger worker thread.
            /// The main thread fills out the data first. 
            /// </summary>
            private AutoResetEvent _workToDoEvent;

            /// <summary>
            /// Signalled by the worker thread when it's finished handling the event and 
            /// the main thread can resume.
            /// </summary>
            private AutoResetEvent _workDoneEvent;

            /// <summary>
            /// Slot for passing operation to the worker thread
            /// </summary>
            private DebugAction _debugAction = DebugAction.Invalid;

            /// <summary>
            /// Parameter for EnterState.
            /// Stored on a stack only for verification that enters and leaves are matched.
            /// </summary>
            private Stack<VirtualStackFrame> _virtualStack;

            /// <summary>
            /// Constructor
            /// </summary>
            internal IslandThread(InvokeIslandDelegate invokeIsland, bool breakOnStartup)
            {
                _invokeIsland = invokeIsland;

                _breakOnStartup = breakOnStartup;

                _virtualStack = new Stack<VirtualStackFrame>();

                _workToDoEvent = new AutoResetEvent(false);
                _workDoneEvent = new AutoResetEvent(false);

                _workerThread = new Thread(new ThreadStart(WorkerThreadProc));
                _workerThread.Name = "DebuggerWorker";
                _workerThread.IsBackground = true; // Don't prevent process exit
                _workerThread.Start();
            }

            /// <summary>
            /// Action for the thread to take
            /// </summary>
            private enum DebugAction
            {
                /// <summary>
                /// Uninitialized
                /// </summary>
                Invalid,

                /// <summary>
                /// Enter a state 
                /// </summary>
                Enter,

                /// <summary>
                /// Leave the current state
                /// </summary>
                Leave,

                /// <summary>
                /// Stop execution
                /// </summary>
                Break
            }

            /// <summary>
            /// This is for internal use by MSBuild, only.
            /// </summary>
            /// <comment>
            /// Private Entry point called from islands. Must be public so that the islands can invoke it.
            /// UNDONE: Make this internal somehow.
            /// Called on debugger worker thread.
            /// </comment>
            public static void IslandWorker(IslandThread controller)
            {
                controller.Worker(true);
            }

            /// <summary>
            /// IDisposable implementation.
            /// </summary>
            void IDisposable.Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Worker thread loop.
            /// Called on debugger worker thread.
            /// </summary>
            internal void Worker(bool withinCallback)
            {
                if (withinCallback)
                {
                    // Fire the 1-time "startup" breakpoint
                    // the first time we are entered from an island
                    if (_breakOnStartup)
                    {
#if FEATURE_DEBUG_LAUNCH
                        Debugger.Launch();
#endif
                        _breakOnStartup = false;
                    }

                    _workDoneEvent.Set(); // Done entering state
                }

                // The final terminator is when leave returns, but from a recursive call.
                while (true)
                {
                    _workToDoEvent.WaitOne();
                    switch (_debugAction)
                    {
                        case DebugAction.Enter:
                            _invokeIsland(this, _virtualStack.Peek());

                            // LeaveState() caused a return back to here
                            _workDoneEvent.Set(); // Done leaving state
                            break;

                        case DebugAction.Leave:
                            // Back up the stack, and if this is the
                            // top of the stack, return out of 
                            // this method. In that case workDoneEvent
                            // must be set by the caller
                            return;

                        case DebugAction.Break:
                            if (!Debugger.IsAttached)
                            {
                                Trace.WriteLine("Triggering debugger attach");
                                Debugger.Break();
                            }

                            _workDoneEvent.Set();
                            break;

                        default:
                            ErrorUtilities.ThrowInternalErrorUnreachable();
                            break;
                    }
                }
            }

            /// <summary>
            /// Posts an Enter instruction to the island thread.
            /// Called by debugger manager thread
            /// </summary>
            internal void EnterState(DebuggerState state, IDictionary<string, object> locals)
            {
                _debugAction = DebugAction.Enter;
                _virtualStack.Push(new VirtualStackFrame(state, locals));
                _workToDoEvent.Set();

                // Block until Island executes NOP, 
                // giving BPs a chance to be hit.
                // Must block here if the island is stopped at a breakpoint.
                _workDoneEvent.WaitOne();
            }

            /// <summary>
            /// Posts a Leave instruction to the island thread.
            /// Called by debugger manager thread
            /// If location is provided, verifies that the state being left is the state that was entered.
            /// Stack may already be empty, in which case it is not modified.
            /// </summary>
            internal void LeaveState(ElementLocation location)
            {
                ErrorUtilities.VerifyThrow(location == null || location == _virtualStack.Peek().State.Location, "Mismatched leave was {0} expected {1}", location.LocationString, _virtualStack.Peek().State.Location.LocationString);

                _debugAction = DebugAction.Leave;

                if (_virtualStack.Count > 0) // May be falling out of the first enter
                {
                    _virtualStack.Pop();
                }

                _workToDoEvent.Set();
                _workDoneEvent.WaitOne();
            }

            /// <summary>
            /// Posts a Break instruction to the island thread.
            /// Called by debugger manager thread.
            /// </summary>
            internal void Break()
            {
                _debugAction = DebugAction.Break;

                _workToDoEvent.Set();
                _workDoneEvent.WaitOne();

                ((IDisposable)this).Dispose();
            }

            /// <summary>
            /// Exit debugging.
            /// Called by debugger manager thread.
            /// </summary>
            internal void Exit()
            {
                // Pop out of any existing stack
                while (_virtualStack.Count >= 0)
                {
                    LeaveState(null /* don't know what was the state */);
                }

                // Add an unbalanced leave to make
                // the debugger worker thread leave the threadproc.
                LeaveState(null /* unbalanced */);

                _workerThread.Join();

                _workToDoEvent.Dispose();
                _workDoneEvent.Dispose();
            }

            /// <summary>
            /// The real disposer.
            /// </summary>
            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _workToDoEvent.Dispose();
                    _workDoneEvent.Dispose();
                }
            }

            /// <summary>
            /// Threadproc.
            /// Called on debugger worker thread.
            /// </summary>
            private void WorkerThreadProc()
            {
                Worker(false /* not within callback */);

                _workDoneEvent.Set(); // Done leaving state the last time
            }
        }

        /// <summary>
        /// Describes a state in the interpreter. A state is any source location that 
        /// a breakpoint could be set on or that could be stepped to, such
        /// as a line of code or a statement.
        /// </summary>
#if JMC
    [DebuggerNonUserCode]
#endif
        internal class DebuggerState
        {
            /// <summary>
            /// Type to later call GetMethod on
            /// </summary>
            private Type _type;

            /// <summary>
            /// Name to later call GetMethod with
            /// </summary>
            private string _methodName;

            /// <summary>
            /// Cached MethodInfo for the method for this state
            /// </summary>
            private MethodInfo _methodInfo;

            /// <summary>
            /// Constructor.
            /// State is given arbitrary provided name, which will appear in the debugger callstack: "ASSEMBLYNAME!FILENAME.STATENAME(...LOCALS...)"
            /// Early locals are any locals whose names and types available at the time the state was created. May be null.
            /// "Calling Type.GetMethod() is slow (10,000 calls can take ~1 minute). So defer that to later."
            /// CALLED ONLY FROM THE DEBUGGER MANAGER.
            /// </summary>
            internal DebuggerState(ElementLocation location, string name, ICollection<DebuggerLocalType> earlyLocalsTypes)
            {
                ErrorUtilities.VerifyThrowInternalNull(location, "location");
                ErrorUtilities.VerifyThrowInternalLength(name, "name");

                this.Location = location;
                this.Name = name;
                this.EarlyLocalsTypes = earlyLocalsTypes ?? ReadOnlyEmptyList<DebuggerLocalType>.Instance;
            }

            /// <summary>
            /// Location in source file associated with this state.
            /// SourceLocations for all the states should be disjoint.
            /// </summary>
            internal ElementLocation Location
            {
                get;
                private set;
            }

            /// <summary>
            /// Friendly name of the state, such as method name.
            /// Never null.
            /// </summary>
            internal string Name
            {
                get;
                private set;
            }

            /// <summary>
            /// Type definitions for early bound locals. This list is ordered.
            /// Names should be unique. 
            /// </summary>
            internal ICollection<DebuggerLocalType> EarlyLocalsTypes
            {
                get;
                private set;
            }

            /// <summary>
            /// Method to call into this state.
            /// Must be gotten on the debugger thread, otherwise 
            /// "NotSupportedException: The invoked member is not supported in a dynamic module."
            /// </summary>
            internal MethodInfo Method
            {
                get
                {
                    ErrorUtilities.VerifyThrow(_type != null, "Didn't bake state '{0}'", Name);

                    if (_methodInfo == null)
                    {
                        _methodInfo = _type.GetMethod(_methodName);
                    }

                    return _methodInfo;
                }
            }

            /// <summary>
            /// Record information necessary to find the method info from
            /// the debugger thread.
            /// CALLED ONLY FROM THE DEBUGGER MANAGER.
            /// </summary>
            internal void RecordMethodInfo(Type typeToRecord, string methodNameToRecord)
            {
                ErrorUtilities.VerifyThrow(_type == null, "already recorded type");
                ErrorUtilities.VerifyThrowInternalNull(typeToRecord, "typeToRecord");
                ErrorUtilities.VerifyThrowInternalLength(methodNameToRecord, "methodNameToRecord");

                _type = typeToRecord;
                _methodName = methodNameToRecord;
            }
        }

        /// <summary>
        /// A virtual callstack frame for the interpreter. 
        /// This is created by calls to EnterState and LeaveState.
        /// </summary>
#if JMC
        [DebuggerNonUserCode]
#endif
        internal class VirtualStackFrame
        {
            /// <summary>
            /// Construct a stack frame for the given state with the given locals (both early and late bound).
            /// </summary>
            /// <param name="state">state for this stackframe</param>
            /// <param name="locals">collection of all locals (both early and late) for this frame. May be null.</param>
            internal VirtualStackFrame(DebuggerState state, IDictionary<string, object> locals)
            {
                ErrorUtilities.VerifyThrowInternalNull(state, "state");

                State = state;
                Locals = locals;
            }

            /// <summary>
            /// State for this frame.
            /// </summary>
            internal DebuggerState State
            {
                get;
                private set;
            }

            /// <summary>
            /// All locals (both early-bound and late-bound) for this frame.
            /// </summary>
            internal IDictionary<string, object> Locals
            {
                get;
                private set;
            }
        }
    }
}
#endif
