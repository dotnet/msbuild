using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace System.Reflection
{
    internal static class TypeExtensions
    {
        public static bool IsEquivalentTo(this Type type, Type other)
        {
            return type.Equals(other);
        }

        public static object InvokeMember(this Type type, string name, BindingFlags bindingFlags, object target, object[] providedArgs,
            ParameterModifier[] modifiers, CultureInfo culture, string[] namedParams)
        {
            return new InvokeMemberHelper(type).InvokeMember(name, bindingFlags, target, providedArgs, modifiers, culture, namedParams);
        }

        public static MethodInfo GetMethod(this Type type,
                                    String name,
                                    BindingFlags bindingAttr,
                                    object binder,
                                    Type[] types,
                                    ParameterModifier[] modifiers)
        {
            return new InvokeMemberHelper(type).GetMethodImpl(name, bindingAttr, binder, CallingConventions.Any, types, modifiers);
        }


        class InvokeMemberHelper
        {
            public Type TargetType { get; private set; }

            public InvokeMemberHelper(Type targetType)
            {
                TargetType = targetType;

            }

            bool IsGenericParameter { get { return TargetType.IsGenericParameter; } }

            private const BindingFlags MemberBindingMask = (BindingFlags)0x000000FF;
            private const BindingFlags InvocationMask = (BindingFlags)0x0000FF00;

            //  InvokeMember code copied and modified from: https://github.com/Microsoft/referencesource/blob/74706335e3b8c806f44fa0683dc1e18d3ed747c2/mscorlib/system/rttype.cs#L4562
            //[Diagnostics.DebuggerHidden]
            internal Object InvokeMember(
                String name, BindingFlags bindingFlags, Object target,
                Object[] providedArgs, ParameterModifier[] modifiers, CultureInfo culture, String[] namedParams)
            {
                if (IsGenericParameter)
                    throw new InvalidOperationException("Arg_GenericParameter");
                Contract.EndContractBlock();

                #region Preconditions
                //if ((bindingFlags & InvocationMask) == 0)
                //    // "Must specify binding flags describing the invoke operation required."
                //    throw new ArgumentException("Arg_NoAccessSpec", "bindingFlags");

                // Provide a default binding mask if none is provided 
                if ((bindingFlags & MemberBindingMask) == 0)
                {
                    bindingFlags |= BindingFlags.Instance | BindingFlags.Public;

                    //if ((bindingFlags & BindingFlags.CreateInstance) == 0)
                    bindingFlags |= BindingFlags.Static;
                }

                // There must not be more named parameters than provided arguments
                if (namedParams != null)
                {
                    if (providedArgs != null)
                    {
                        if (namedParams.Length > providedArgs.Length)
                            // "Named parameter array can not be bigger than argument array."
                            throw new ArgumentException("Arg_NamedParamTooBig", "namedParams");
                    }
                    else
                    {
                        if (namedParams.Length != 0)
                            // "Named parameter array can not be bigger than argument array."
                            throw new ArgumentException("Arg_NamedParamTooBig", "namedParams");
                    }
                }
                #endregion

                #region COM Interop
#if FEATURE_COMINTEROP && FEATURE_USE_LCID
            if (target != null && target.GetType().IsCOMObject)
            {
                #region Preconditions
                if ((bindingFlags & ClassicBindingMask) == 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMAccess"), "bindingFlags");

                if ((bindingFlags & BindingFlags.GetProperty) != 0 && (bindingFlags & ClassicBindingMask & ~(BindingFlags.GetProperty | BindingFlags.InvokeMethod)) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_PropSetGet"), "bindingFlags");

                if ((bindingFlags & BindingFlags.InvokeMethod) != 0 && (bindingFlags & ClassicBindingMask & ~(BindingFlags.GetProperty | BindingFlags.InvokeMethod)) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_PropSetInvoke"), "bindingFlags");

                if ((bindingFlags & BindingFlags.SetProperty) != 0 && (bindingFlags & ClassicBindingMask & ~BindingFlags.SetProperty) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMPropSetPut"), "bindingFlags");

                if ((bindingFlags & BindingFlags.PutDispProperty) != 0 && (bindingFlags & ClassicBindingMask & ~BindingFlags.PutDispProperty) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMPropSetPut"), "bindingFlags");

                if ((bindingFlags & BindingFlags.PutRefDispProperty) != 0 && (bindingFlags & ClassicBindingMask & ~BindingFlags.PutRefDispProperty) != 0)
                    throw new ArgumentException(Environment.GetResourceString("Arg_COMPropSetPut"), "bindingFlags");
                #endregion

#if FEATURE_REMOTING
                if(!RemotingServices.IsTransparentProxy(target))
#endif
                {
                #region Non-TransparentProxy case
                    if (name == null)
                        throw new ArgumentNullException("name");

                    bool[] isByRef = modifiers == null ? null : modifiers[0].IsByRefArray;
                    
                    // pass LCID_ENGLISH_US if no explicit culture is specified to match the behavior of VB
                    int lcid = (culture == null ? 0x0409 : culture.LCID);

                    return InvokeDispMethod(name, bindingFlags, target, providedArgs, isByRef, lcid, namedParams);
                #endregion
                }
#if FEATURE_REMOTING
                else
                {
                #region TransparentProxy case
                    return ((MarshalByRefObject)target).InvokeMember(name, bindingFlags, binder, providedArgs, modifiers, culture, namedParams);
                #endregion
                }
#endif // FEATURE_REMOTING
            }
#endif // FEATURE_COMINTEROP && FEATURE_USE_LCID
                #endregion

                #region Check that any named paramters are not null
                if (namedParams != null && Array.IndexOf(namedParams, null) != -1)
                    // "Named parameter value must not be null."
                    throw new ArgumentException("Arg_NamedParamNull", "namedParams");
                #endregion

                int argCnt = (providedArgs != null) ? providedArgs.Length : 0;

                //#region Get a Binder
                //if (binder == null)
                //    binder = DefaultBinder;

                //bool bDefaultBinder = (binder == DefaultBinder);
                //#endregion

                //#region Delegate to Activator.CreateInstance
                //if ((bindingFlags & BindingFlags.CreateInstance) != 0)
                //{
                //    if ((bindingFlags & BindingFlags.CreateInstance) != 0 && (bindingFlags & BinderNonCreateInstance) != 0)
                //        // "Can not specify both CreateInstance and another access type."
                //        throw new ArgumentException(Environment.GetResourceString("Arg_CreatInstAccess"), "bindingFlags");

                //    return Activator.CreateInstance(this, bindingFlags, binder, providedArgs, culture);
                //}
                //#endregion

                //// PutDispProperty and\or PutRefDispProperty ==> SetProperty.
                //if ((bindingFlags & (BindingFlags.PutDispProperty | BindingFlags.PutRefDispProperty)) != 0)
                //    bindingFlags |= BindingFlags.SetProperty;

                #region Name
                if (name == null)
                    throw new ArgumentNullException("name");

                if (name.Length == 0 || name.Equals(@"[DISPID=0]"))
                {
                    //name = GetDefaultMemberName();

                    if (name == null)
                    {
                        // in InvokeMember we always pretend there is a default member if none is provided and we make it ToString
                        name = "ToString";
                    }
                }
                #endregion

                //#region GetField or SetField
                //bool IsGetField = (bindingFlags & BindingFlags.GetField) != 0;
                //bool IsSetField = (bindingFlags & BindingFlags.SetField) != 0;

                //if (IsGetField || IsSetField)
                //{
                //    #region Preconditions
                //    if (IsGetField)
                //    {
                //        if (IsSetField)
                //            // "Can not specify both Get and Set on a field."
                //            throw new ArgumentException(Environment.GetResourceString("Arg_FldSetGet"), "bindingFlags");

                //        if ((bindingFlags & BindingFlags.SetProperty) != 0)
                //            // "Can not specify both GetField and SetProperty."
                //            throw new ArgumentException(Environment.GetResourceString("Arg_FldGetPropSet"), "bindingFlags");
                //    }
                //    else
                //    {
                //        Contract.Assert(IsSetField);

                //        if (providedArgs == null)
                //            throw new ArgumentNullException("providedArgs");

                //        if ((bindingFlags & BindingFlags.GetProperty) != 0)
                //            // "Can not specify both SetField and GetProperty."
                //            throw new ArgumentException(Environment.GetResourceString("Arg_FldSetPropGet"), "bindingFlags");

                //        if ((bindingFlags & BindingFlags.InvokeMethod) != 0)
                //            // "Can not specify Set on a Field and Invoke on a method."
                //            throw new ArgumentException(Environment.GetResourceString("Arg_FldSetInvoke"), "bindingFlags");
                //    }
                //    #endregion

                //    #region Lookup Field
                //    FieldInfo selFld = null;
                //    FieldInfo[] flds = GetMember(name, MemberTypes.Field, bindingFlags) as FieldInfo[];

                //    Contract.Assert(flds != null);

                //    if (flds.Length == 1)
                //    {
                //        selFld = flds[0];
                //    }
                //    else if (flds.Length > 0)
                //    {
                //        selFld = binder.BindToField(bindingFlags, flds, IsGetField ? Empty.Value : providedArgs[0], culture);
                //    }
                //    #endregion

                //    if (selFld != null)
                //    {
                //        #region Invocation on a field
                //        if (selFld.FieldType.IsArray || Object.ReferenceEquals(selFld.FieldType, typeof(System.Array)))
                //        {
                //            #region Invocation of an array Field
                //            int idxCnt;

                //            if ((bindingFlags & BindingFlags.GetField) != 0)
                //            {
                //                idxCnt = argCnt;
                //            }
                //            else
                //            {
                //                idxCnt = argCnt - 1;
                //            }

                //            if (idxCnt > 0)
                //            {
                //                // Verify that all of the index values are ints
                //                int[] idx = new int[idxCnt];
                //                for (int i = 0; i < idxCnt; i++)
                //                {
                //                    try
                //                    {
                //                        idx[i] = ((IConvertible)providedArgs[i]).ToInt32(null);
                //                    }
                //                    catch (InvalidCastException)
                //                    {
                //                        throw new ArgumentException(Environment.GetResourceString("Arg_IndexMustBeInt"));
                //                    }
                //                }

                //                // Set or get the value...
                //                Array a = (Array)selFld.GetValue(target);

                //                // Set or get the value in the array
                //                if ((bindingFlags & BindingFlags.GetField) != 0)
                //                {
                //                    return a.GetValue(idx);
                //                }
                //                else
                //                {
                //                    a.SetValue(providedArgs[idxCnt], idx);
                //                    return null;
                //                }
                //            }
                //            #endregion
                //        }

                //        if (IsGetField)
                //        {
                //            #region Get the field value
                //            if (argCnt != 0)
                //                throw new ArgumentException(Environment.GetResourceString("Arg_FldGetArgErr"), "bindingFlags");

                //            return selFld.GetValue(target);
                //            #endregion
                //        }
                //        else
                //        {
                //            #region Set the field Value
                //            if (argCnt != 1)
                //                throw new ArgumentException(Environment.GetResourceString("Arg_FldSetArgErr"), "bindingFlags");

                //            selFld.SetValue(target, providedArgs[0], bindingFlags, binder, culture);

                //            return null;
                //            #endregion
                //        }
                //        #endregion
                //    }

                //    if ((bindingFlags & BinderNonFieldGetSet) == 0)
                //        throw new MissingFieldException(FullName, name);
                //}
                //#endregion

                #region Caching Logic
                /*
                bool useCache = false;
                // Note that when we add something to the cache, we are careful to ensure
                // that the actual providedArgs matches the parameters of the method.  Otherwise,
                // some default argument processing has occurred.  We don't want anyone
                // else with the same (insufficient) number of actual arguments to get a
                // cache hit because then they would bypass the default argument processing
                // and the invocation would fail.
                if (bDefaultBinder && namedParams == null && argCnt < 6)
                    useCache = true;
                if (useCache)
                {
                    MethodBase invokeMethod = GetMethodFromCache (name, bindingFlags, argCnt, providedArgs);
                    if (invokeMethod != null)
                        return ((MethodInfo) invokeMethod).Invoke(target, bindingFlags, binder, providedArgs, culture);
                }
                */
                #endregion

                //#region Property PreConditions
                //// @Legacy - This is RTM behavior
                //bool isGetProperty = (bindingFlags & BindingFlags.GetProperty) != 0;
                //bool isSetProperty = (bindingFlags & BindingFlags.SetProperty) != 0;

                //if (isGetProperty || isSetProperty)
                //{
                //    #region Preconditions
                //    if (isGetProperty)
                //    {
                //        Contract.Assert(!IsSetField);

                //        if (isSetProperty)
                //            throw new ArgumentException(Environment.GetResourceString("Arg_PropSetGet"), "bindingFlags");
                //    }
                //    else
                //    {
                //        Contract.Assert(isSetProperty);

                //        Contract.Assert(!IsGetField);

                //        if ((bindingFlags & BindingFlags.InvokeMethod) != 0)
                //            throw new ArgumentException(Environment.GetResourceString("Arg_PropSetInvoke"), "bindingFlags");
                //    }
                //    #endregion
                //}
                //#endregion

                MethodInfo[] finalists = null;
                MethodInfo finalist = null;

                #region BindingFlags.InvokeMethod
                //if ((bindingFlags & BindingFlags.InvokeMethod) != 0)
                {
                    #region Lookup Methods
                    //MethodInfo[] semiFinalists = GetMember(name, MemberTypes.Method, bindingFlags) as MethodInfo[];
                    MethodInfo[] semiFinalists = TargetType.GetMethods(bindingFlags).Where(mi => mi.Name == name).ToArray();
                    List<MethodInfo> results = null;

                    for (int i = 0; i < semiFinalists.Length; i++)
                    {
                        MethodInfo semiFinalist = semiFinalists[i];
                        Contract.Assert(semiFinalist != null);

                        if (!FilterApplyMethodBase(semiFinalist, bindingFlags, bindingFlags, CallingConventions.Any, new Type[argCnt]))
                            continue;

                        if (finalist == null)
                        {
                            finalist = semiFinalist;
                        }
                        else
                        {
                            if (results == null)
                            {
                                results = new List<MethodInfo>(semiFinalists.Length);
                                results.Add(finalist);
                            }

                            results.Add(semiFinalist);
                        }
                    }

                    if (results != null)
                    {
                        Contract.Assert(results.Count > 1);
                        finalists = new MethodInfo[results.Count];
                        results.CopyTo(finalists);
                    }
                    #endregion
                }
                #endregion

                Contract.Assert(finalists == null || finalist != null);

                //#region BindingFlags.GetProperty or BindingFlags.SetProperty
                //if (finalist == null && isGetProperty || isSetProperty)
                //{
                //    #region Lookup Property
                //    PropertyInfo[] semiFinalists = GetMember(name, MemberTypes.Property, bindingFlags) as PropertyInfo[];
                //    List<MethodInfo> results = null;

                //    for (int i = 0; i < semiFinalists.Length; i++)
                //    {
                //        MethodInfo semiFinalist = null;

                //        if (isSetProperty)
                //        {
                //            semiFinalist = semiFinalists[i].GetSetMethod(true);
                //        }
                //        else
                //        {
                //            semiFinalist = semiFinalists[i].GetGetMethod(true);
                //        }

                //        if (semiFinalist == null)
                //            continue;

                //        if (!FilterApplyMethodInfo((RuntimeMethodInfo)semiFinalist, bindingFlags, CallingConventions.Any, new Type[argCnt]))
                //            continue;

                //        if (finalist == null)
                //        {
                //            finalist = semiFinalist;
                //        }
                //        else
                //        {
                //            if (results == null)
                //            {
                //                results = new List<MethodInfo>(semiFinalists.Length);
                //                results.Add(finalist);
                //            }

                //            results.Add(semiFinalist);
                //        }
                //    }

                //    if (results != null)
                //    {
                //        Contract.Assert(results.Count > 1);
                //        finalists = new MethodInfo[results.Count];
                //        results.CopyTo(finalists);
                //    }
                //    #endregion
                //}
                //#endregion

                if (finalist != null)
                {
                    #region Invoke
                    if (finalists == null &&
                        argCnt == 0 &&
                        finalist.GetParameters().Length == 0 
                        //&& (bindingFlags & BindingFlags.OptionalParamBinding) == 0
                        )
                    {
                        //if (useCache && argCnt == props[0].GetParameters().Length)
                        //    AddMethodToCache(name, bindingFlags, argCnt, providedArgs, props[0]);

                        //return finalist.Invoke(target, bindingFlags, binder, providedArgs, culture);
                        return finalist.Invoke(target, providedArgs);
                    }

                    if (finalists == null)
                        finalists = new MethodInfo[] { finalist };

                    if (providedArgs == null)
                        providedArgs = Array.Empty<object>();

                    Object state = null;


                    MethodBase invokeMethod = null;

                    try { invokeMethod = DefaultBinder.BindToMethod(bindingFlags, finalists, ref providedArgs, modifiers, culture, namedParams, out state); }
                    catch (MissingMethodException) { }

                    if (invokeMethod == null)
                        throw new MissingMethodException(TargetType.FullName + "." + name);

                    //if (useCache && argCnt == invokeMethod.GetParameters().Length)
                    //    AddMethodToCache(name, bindingFlags, argCnt, providedArgs, invokeMethod);

                    //Object result = ((MethodInfo)invokeMethod).Invoke(target, bindingFlags, binder, providedArgs, culture);
                    Object result = ((MethodInfo)invokeMethod).Invoke(target, providedArgs);

                    if (state != null)
                        DefaultBinder.ReorderArgumentArray(ref providedArgs, state);

                    return result;
                    #endregion
                }

                throw new MissingMethodException(TargetType.FullName + "." + name);
            }


            //  FilterApplyMethodBase code copied from: https://github.com/Microsoft/referencesource/blob/74706335e3b8c806f44fa0683dc1e18d3ed747c2/mscorlib/system/rttype.cs#L2514
            private static bool FilterApplyMethodBase(
                    MethodBase methodBase, BindingFlags methodFlags, BindingFlags bindingFlags, CallingConventions callConv, Type[] argumentTypes)
            {
                Contract.Requires(methodBase != null);

                bindingFlags ^= BindingFlags.DeclaredOnly;

                #region Apply Base Filter
                if ((bindingFlags & methodFlags) != methodFlags)
                    return false;
                #endregion

                #region Check CallingConvention
                if ((callConv & CallingConventions.Any) == 0)
                {
                    if ((callConv & CallingConventions.VarArgs) != 0 &&
                        (methodBase.CallingConvention & CallingConventions.VarArgs) == 0)
                        return false;

                    if ((callConv & CallingConventions.Standard) != 0 &&
                        (methodBase.CallingConvention & CallingConventions.Standard) == 0)
                        return false;
                }
                #endregion

                #region If argumentTypes supplied
                if (argumentTypes != null)
                {
                    ParameterInfo[] parameterInfos = methodBase.GetParameters();

                    if (argumentTypes.Length != parameterInfos.Length)
                    {
                        #region Invoke Member, Get\Set & Create Instance specific case
                        //// If the number of supplied arguments differs than the number in the signature AND
                        //// we are not filtering for a dynamic call -- InvokeMethod or CreateInstance -- filter out the method.
                        //if ((bindingFlags &
                        //    (BindingFlags.InvokeMethod | BindingFlags.CreateInstance | BindingFlags.GetProperty | BindingFlags.SetProperty)) == 0)
                        //    return false;

                        bool testForParamArray = false;
                        bool excessSuppliedArguments = argumentTypes.Length > parameterInfos.Length;

                        if (excessSuppliedArguments)
                        { // more supplied arguments than parameters, additional arguments could be vararg
                            #region Varargs
                            // If method is not vararg, additional arguments can not be passed as vararg
                            if ((methodBase.CallingConvention & CallingConventions.VarArgs) == 0)
                            {
                                testForParamArray = true;
                            }
                            else
                            {
                                // If Binding flags did not include varargs we would have filtered this vararg method.
                                // This Invariant established during callConv check.
                                Contract.Assert((callConv & CallingConventions.VarArgs) != 0);
                            }
                            #endregion
                        }
                        else
                        {// fewer supplied arguments than parameters, missing arguments could be optional
                            #region OptionalParamBinding
                            //if ((bindingFlags & BindingFlags.OptionalParamBinding) == 0)
                            if (true)
                            {
                                testForParamArray = true;
                            }
                            //else
                            //{
                            //    // From our existing code, our policy here is that if a parameterInfo 
                            //    // is optional then all subsequent parameterInfos shall be optional. 

                            //    // Thus, iff the first parameterInfo is not optional then this MethodInfo is no longer a canidate.
                            //    if (!parameterInfos[argumentTypes.Length].IsOptional)
                            //        testForParamArray = true;
                            //}
                            #endregion
                        }

                        #region ParamArray
                        if (testForParamArray)
                        {
                            if (parameterInfos.Length == 0)
                                return false;

                            // The last argument of the signature could be a param array. 
                            bool shortByMoreThanOneSuppliedArgument = argumentTypes.Length < parameterInfos.Length - 1;

                            if (shortByMoreThanOneSuppliedArgument)
                                return false;

                            ParameterInfo lastParameter = parameterInfos[parameterInfos.Length - 1];

                            if (!lastParameter.ParameterType.IsArray)
                                return false;

                            if (!lastParameter.IsDefined(typeof(ParamArrayAttribute), false))
                                return false;
                        }
                        #endregion

                        #endregion
                    }
                    else
                    {
                        //#region Exact Binding
                        //if ((bindingFlags & BindingFlags.ExactBinding) != 0)
                        //{
                        //    // Legacy behavior is to ignore ExactBinding when InvokeMember is specified.
                        //    // Why filter by InvokeMember? If the answer is we leave this to the binder then why not leave
                        //    // all the rest of this  to the binder too? Further, what other semanitc would the binder
                        //    // use for BindingFlags.ExactBinding besides this one? Further, why not include CreateInstance 
                        //    // in this if statement? That's just InvokeMethod with a constructor, right?
                        //    if ((bindingFlags & (BindingFlags.InvokeMethod)) == 0)
                        //    {
                        //        for (int i = 0; i < parameterInfos.Length; i++)
                        //        {
                        //            // a null argument type implies a null arg which is always a perfect match
                        //            if ((object)argumentTypes[i] != null && !Object.ReferenceEquals(parameterInfos[i].ParameterType, argumentTypes[i]))
                        //                return false;
                        //        }
                        //    }
                        //}
                        //#endregion
                    }
                }
                #endregion

                return true;
            }

            //  GetMethodImpl code copied from: https://github.com/dotnet/coreclr/blob/bc146608854d1db9cdbcc0b08029a87754e12b49/src/mscorlib/src/System/RtType.cs#L3165
            public MethodInfo GetMethodImpl(
                    String name, BindingFlags bindingAttr, object binder, CallingConventions callConv,
                    Type[] types, ParameterModifier[] modifiers)
            {
                List<MethodInfo> candidates = GetMethodCandidates(name, bindingAttr, callConv, types, false);

                if (candidates.Count == 0)
                    return null;

                if (types == null || types.Length == 0)
                {
                    MethodInfo firstCandidate = candidates[0];

                    if (candidates.Count == 1)
                    {
                        return firstCandidate;
                    }
                    else if (types == null)
                    {
                        for (int j = 1; j < candidates.Count; j++)
                        {
                            MethodInfo methodInfo = candidates[j];
                            if (!System.DefaultBinder.CompareMethodSigAndName(methodInfo, firstCandidate))
                            {
                                throw new AmbiguousMatchException("Arg_AmbiguousMatchException");
                            }
                        }

                        // All the methods have the exact same name and sig so return the most derived one.
                        return System.DefaultBinder.FindMostDerivedNewSlotMeth(candidates.ToArray(), candidates.Count) as MethodInfo;
                    }
                }

                //if (binder == null)
                //    binder = DefaultBinder;

                return DefaultBinder.SelectMethod(bindingAttr, candidates.ToArray(), types, modifiers) as MethodInfo;
            }

            //  MemberListType code copied from: https://github.com/dotnet/coreclr/blob/bc146608854d1db9cdbcc0b08029a87754e12b49/src/mscorlib/src/System/RtType.cs#L87
            internal enum MemberListType
            {
                All,
                CaseSensitive,
                CaseInsensitive,
                HandleToInfo
            }

            //  FilterHelper code copied from: https://github.com/dotnet/coreclr/blob/bc146608854d1db9cdbcc0b08029a87754e12b49/src/mscorlib/src/System/RtType.cs#L87
            // Calculate prefixLookup, ignoreCase, and listType for use by GetXXXCandidates
            private static void FilterHelper(
                BindingFlags bindingFlags, ref string name, bool allowPrefixLookup, out bool prefixLookup,
                out bool ignoreCase, out MemberListType listType)
            {
                prefixLookup = false;
                ignoreCase = false;

                if (name != null)
                {
                    if ((bindingFlags & BindingFlags.IgnoreCase) != 0)
                    {
                        name = name.ToLowerInvariant();
                        ignoreCase = true;
                        listType = MemberListType.CaseInsensitive;
                    }
                    else
                    {
                        listType = MemberListType.CaseSensitive;
                    }

                    if (allowPrefixLookup && name.EndsWith("*", StringComparison.Ordinal))
                    {
                        // We set prefixLookup to true if name ends with a "*".
                        // We will also set listType to All so that all members are included in 
                        // the candidates which are later filtered by FilterApplyPrefixLookup.
                        name = name.Substring(0, name.Length - 1);
                        prefixLookup = true;
                        listType = MemberListType.All;
                    }
                }
                else
                {
                    listType = MemberListType.All;
                }
            }

            //  GetMethodCondidates code copied from: https://github.com/dotnet/coreclr/blob/bc146608854d1db9cdbcc0b08029a87754e12b49/src/mscorlib/src/System/RtType.cs#L2792
            private List<MethodInfo> GetMethodCandidates(
                 String name, BindingFlags bindingAttr, CallingConventions callConv,
                 Type[] types, bool allowPrefixLookup)
            {
                bool prefixLookup, ignoreCase;
                MemberListType listType;
                FilterHelper(bindingAttr, ref name, allowPrefixLookup, out prefixLookup, out ignoreCase, out listType);

                //RuntimeMethodInfo[] cache = Cache.GetMethodList(listType, name);
                MethodInfo[] cache;
                if (listType == MemberListType.All)
                {
                    cache = TargetType.GetMethods(bindingAttr);
                }
                else
                {
                    cache = TargetType.GetMethods().Where(m =>
                    {
                        if (listType == MemberListType.CaseSensitive)
                        {
                            return m.Name == name;
                        }
                        else
                        {
                            return m.Name.Equals(name, StringComparison.OrdinalIgnoreCase);
                        }
                    }).ToArray();
                }

                List<MethodInfo> candidates = new List<MethodInfo>(cache.Length);
                for (int i = 0; i < cache.Length; i++)
                {
                    MethodInfo methodInfo = cache[i];
                    if (FilterApplyMethodBase(methodInfo, bindingAttr, bindingAttr, callConv, types) &&
                        (!prefixLookup || FilterApplyPrefixLookup(methodInfo, name, ignoreCase)))
                    {
                        candidates.Add(methodInfo);
                    }
                }

                return candidates;
            }

            //  FilterApplyPrefixLookup code copied from: https://github.com/dotnet/coreclr/blob/bc146608854d1db9cdbcc0b08029a87754e12b49/src/mscorlib/src/System/RtType.cs#L2367
            // Only called by GetXXXCandidates, GetInterfaces, and GetNestedTypes when FilterHelper has set "prefixLookup" to true.
            // Most of the plural GetXXX methods allow prefix lookups while the singular GetXXX methods mostly do not.
            private static bool FilterApplyPrefixLookup(MemberInfo memberInfo, string name, bool ignoreCase)
            {
                Contract.Assert(name != null);

                if (ignoreCase)
                {
                    if (!memberInfo.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else
                {
                    if (!memberInfo.Name.StartsWith(name, StringComparison.Ordinal))
                        return false;
                }

                return true;
            }
        }
    }
}