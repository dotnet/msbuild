// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    internal class DynamicAssembly
    {
        public DynamicAssembly(string assemblyName, System.Version verToLoad, string publicKeyToken)
        {
            AssemblyFullName = string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}, Version={1}.{2}.0.0, Culture=neutral, PublicKeyToken={3}", assemblyName, verToLoad.Major, verToLoad.Minor, publicKeyToken);
#if NET472
            bool isAssemblyLoaded = false;
            try
            {
                Assembly = Assembly.Load(AssemblyFullName);
                isAssemblyLoaded = true;
            }
            catch (FileNotFoundException)
            {
            }

            // if the assembly is not available in the gac, try to load it from the same path as task assembly.
            if (!isAssemblyLoaded)
            {
                Assembly = Assembly.LoadFrom(Path.Combine(TaskAssemblyDirectory, assemblyName+".dll"));
            }
#endif
            Version = verToLoad;
        }

#if NET472
        public static string TaskAssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
#endif
        public DynamicAssembly() { }

        public string AssemblyFullName { get; set; }
        public System.Version Version { get; set; }
        public Assembly Assembly { get; set; }

        public System.Type GetType(string typeName)
        {
            System.Type type = Assembly.GetType(typeName);
            Debug.Assert(type != null);
            return type;
        }

        public virtual System.Type TryGetType(string typeName)
        {
            System.Type type = Assembly.GetType(typeName);
            return type;
        }

        public object GetEnumValue(string enumName, string enumValue)
        {
            System.Type enumType = Assembly.GetType(enumName);
            FieldInfo enumItem = enumType.GetField(enumValue);
            object ret = enumItem.GetValue(enumType);
            Debug.Assert(ret != null);
            return ret;
        }

        public object GetEnumValueIgnoreCase(string enumName, string enumValue)
        {
            System.Type enumType = Assembly.GetType(enumName);
            FieldInfo enumItem = enumType.GetField(enumValue, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            object ret = enumItem.GetValue(enumType);
            Debug.Assert(ret != null);
            return ret;
        }

        public bool TryGetEnumValue(string enumTypeName, string enumStrValue, out object retValue)
        {
            bool fGetValue = false;
            retValue = System.Enum.ToObject(GetType(enumTypeName), 0);
            try
            {
                retValue = GetEnumValueIgnoreCase(enumTypeName, enumStrValue);
                fGetValue = true;
            }
            catch
            {
            }
            return fGetValue;
        }


        public object CreateObject(string typeName)
        {
            return CreateObject(typeName, null);
        }

        public object CreateObject(string typeName, object[] arguments)
        {
            object createdObject = null;
            System.Type[] argumentTypes = null;
            if (arguments == null || arguments.GetLength(0) == 0)
            {
                argumentTypes = System.Type.EmptyTypes;
            }
            else
            {
                argumentTypes = arguments.Select(p => p.GetType()).ToArray();
            }
            System.Type typeToConstruct = Assembly.GetType(typeName);
            System.Reflection.ConstructorInfo constructorInfoObj = typeToConstruct.GetConstructor(argumentTypes);

            if (constructorInfoObj == null)
            {
                Debug.Assert(false, "DynamicAssembly.CreateObject Failed to get the constructorInfoObject");
            }
            else
            {
                createdObject = constructorInfoObj.Invoke(arguments);
            }
            Debug.Assert(createdObject != null);
            return createdObject;
        }

#if NET472
        public object CallStaticMethod(string typeName, string methodName, object[] arguments)
        {
            System.Type t = GetType(typeName);
            return t.InvokeMember(methodName, BindingFlags.InvokeMethod, null, t, arguments, System.Globalization.CultureInfo.InvariantCulture);
        }

#endif

        /// <summary>
        /// Support late bind delegate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void EventHandlerDynamicDelegate(object sender, dynamic e);
        public delegate void EventHandlerEventArgsDelegate(object sender, System.EventArgs e);
        internal static System.Delegate CreateEventHandlerDelegate<TDelegate>(System.Reflection.EventInfo evt, TDelegate d)
        {
            var handlerType = evt.EventHandlerType;
            var eventParams = handlerType.GetMethod("Invoke").GetParameters();

            ParameterExpression[] parameters = eventParams.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            MethodCallExpression body = Expression.Call(Expression.Constant(d), d.GetType().GetMethod("Invoke"), parameters);
            var lambda = Expression.Lambda(body, parameters);
            // Diagnostics.Debug.Assert(false, lambda.ToString());
#if NET472
            return System.Delegate.CreateDelegate(handlerType, lambda.Compile(), "Invoke", false);
#else
            return null;
#endif
        }

        static public System.Delegate AddEventDeferHandler(dynamic obj, string eventName, System.Delegate deferEventHandler)
        {
            EventInfo eventinfo = obj.GetType().GetEvent(eventName);
            System.Delegate eventHandler = DynamicAssembly.CreateEventHandlerDelegate(eventinfo, deferEventHandler);
            eventinfo.AddEventHandler(obj, eventHandler);
            return eventHandler;
        }

        static public void AddEventHandler(dynamic obj, string eventName, System.Delegate eventHandler)
        {
            EventInfo eventinfo = obj.GetType().GetEvent(eventName);
            eventinfo.AddEventHandler(obj, eventHandler);
        }

        static public void RemoveEventHandler(dynamic obj, string eventName, System.Delegate eventHandler)
        {
            EventInfo eventinfo = obj.GetType().GetEvent(eventName);
            eventinfo.RemoveEventHandler(obj, eventHandler);
        }
    }
}
