// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A code task factory which uses code dom to generate tasks</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
#if FEATURE_CODETASKFACTORY
    using System.CodeDom.Compiler;
    using System.CodeDom;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Xml;

    /// <summary>
    /// A task factory which can take code dom supported languages and create a task out of it
    /// </summary>
    public class CodeTaskFactory : ITaskFactory
    {
        /// <summary>
        /// This dictionary keeps track of custom references to compiled assemblies.  The in-memory assembly is loaded from a byte
        /// stream and as such its dependencies cannot be found unless they are in the MSBuild.exe directory or the GAC.  They
        /// cannot be found even if they are already loaded in the AppDomain.  This dictionary knows the FullName of the assembly
        /// and a reference to the assembly itself.  In the <see cref="CurrentDomainOnAssemblyResolve"/> handler, the dictionary 
        /// is used to return the loaded assemblies as a way to allow custom references that are not in the normal assembly Load
        /// context.
        /// </summary>
        private static readonly IDictionary<string, Assembly> s_knownReferenceAssemblies = new ConcurrentDictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        static CodeTaskFactory()
        {
            // The handler is not detached because it only returns assemblies for custom references that cannot be found in the normal Load context
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
        }

        /// <summary>
        /// Handles the <see cref="AppDomain.AssemblyResolve"/> event to return assemblies loaded from custom references.
        /// </summary>
        private static Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Return the assembly loaded from the custom reference if the FullName matches what is being looked for
            s_knownReferenceAssemblies.TryGetValue(args.Name, out Assembly assembly);

            return assembly;
        }

        /// <summary>
        /// Default assemblies names to reference during inline code compilation - from the .NET Framework
        /// </summary>
        private static readonly string[] s_defaultReferencedFrameworkAssemblyNames = { @"System.Core" };

        /// <summary>
        ///  Default using's for the code
        /// </summary>
        private readonly string[] _defaultUsingNamespaces = { "System", "System.Collections", "System.Collections.Generic", "System.Text", "System.Linq", "System.IO", "Microsoft.Build.Framework", "Microsoft.Build.Utilities" };

        /// <summary>
        /// A collection of task assemblies which have been instantiated by any CodeTaskFactory.  Used to prevent us from creating
        /// duplicate assemblies.
        /// </summary>
        private static readonly ConcurrentDictionary<FullTaskSpecification, Assembly> s_compiledTaskCache = new ConcurrentDictionary<FullTaskSpecification, Assembly>();

        /// <summary>
        /// The default assemblies to reference when compiling inline code. 
        /// </summary>
        private static List<string> s_defaultReferencedAssemblies;

        /// <summary>
        /// Merged set of assembly reference paths (default + specified)
        /// </summary>
        private string[] _referencedAssemblies;

        /// <summary>
        /// Merged set of namespaces (default + specified) 
        /// </summary>
        private string[] _usingNamespaces;

        /// <summary>
        /// Type of code fragment, ie   Fragment, Class, Method
        /// </summary>
        private string _type;

        /// <summary>
        /// Is the type a fragment or not
        /// </summary>
        private bool _typeIsFragment;

        /// <summary>
        /// Is the type a method or not
        /// </summary>
        private bool _typeIsMethod;

        /// <summary>
        /// By default the language supported is C#, but anything that supports code dom will work
        /// </summary>
        private string _language = "cs";

        /// <summary>
        /// The source that will be compiled
        /// </summary>
        private string _sourceCode;

        /// <summary>
        /// The name of the task for which this is the factory
        /// </summary>
        private string _nameOfTask;

        /// <summary>
        /// Path to source that is outside the project file
        /// </summary>
        private string _sourcePath;

        /// <summary>
        /// The using task node from the project file
        /// </summary>
        private XmlNode _taskNode;

        /// <summary>
        /// The inline source compiled into an in memory assembly
        /// </summary>
        private Assembly _compiledAssembly;

        /// <summary>
        /// Helper to assist in logging messages
        /// </summary>
        private TaskLoggingHelper _log;

        /// <summary>
        /// Task parameter type information
        /// </summary>
        private IDictionary<string, TaskPropertyInfo> _taskParameterTypeInfo;

        /// <summary>
        /// MSBuild engine uses this for logging where the task comes from
        /// </summary>
        public string FactoryName => "Code Task Factory";

        /// <summary>
        /// Gets the type of the generated task.
        /// </summary>
        public Type TaskType { get; private set; }

        /// <summary>
        /// The assemblies that the codetaskfactory should reference by default. 
        /// </summary>
        private static List<string> DefaultReferencedAssemblies
        {
            get
            {
                if (s_defaultReferencedAssemblies == null)
                {
                    s_defaultReferencedAssemblies = new List<string>();

                    // Loading with the partial name is fine for framework assemblies -- we'll always get the correct one 
                    // through the magic of unification
                    foreach (string frameworkAssembly in s_defaultReferencedFrameworkAssemblyNames)
                    {
                        s_defaultReferencedAssemblies.Add(frameworkAssembly);
                    }

                    // We also want to add references to two MSBuild assemblies: Microsoft.Build.Framework.dll and 
                    // Microsoft.Build.Utilities.Core.dll.  If we just let the CLR unify the simple name, it will 
                    // pick the highest version on the machine, which means that in hosts with restrictive binding 
                    // redirects, or no binding redirects, we'd end up creating an inline task that could not be 
                    // run.  Instead, to make sure that we can actually use what we're building, just use the Framework
                    // and Utilities currently loaded into this process -- Since we're in Microsoft.Build.Tasks.Core.dll
                    // right now, by definition both of them are always already loaded. 
                    string msbuildFrameworkPath = Assembly.GetAssembly(typeof(ITask)).Location;
                    string msbuildUtilitiesPath = Assembly.GetAssembly(typeof(Task)).Location;

                    s_defaultReferencedAssemblies.Add(msbuildFrameworkPath);
                    s_defaultReferencedAssemblies.Add(msbuildUtilitiesPath);
                }

                return s_defaultReferencedAssemblies;
            }
        }

        /// <summary>
        /// Get the type information for all task parameters
        /// </summary>
        public TaskPropertyInfo[] GetTaskParameters()
        {
            var properties = new TaskPropertyInfo[_taskParameterTypeInfo.Count];
            _taskParameterTypeInfo.Values.CopyTo(properties, 0);
            return properties;
        }

        /// <summary>
        /// Initialze the task factory
        /// </summary>
        public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> taskParameters, string taskElementContents, IBuildEngine taskFactoryLoggingHost)
        {
            _nameOfTask = taskName;
            _log = new TaskLoggingHelper(taskFactoryLoggingHost, taskName)
            {
                TaskResources = AssemblyResources.PrimaryResources,
                HelpKeywordPrefix = "MSBuild."
            };

            XmlNode taskContent = ExtractTaskContent(taskElementContents);
            if (taskContent == null)
            {
                // Just return false because we have already logged the error in ExtractTaskContents
                return false;
            }

            bool validatedTaskNode = ValidateTaskNode();
            if (!validatedTaskNode)
            {
                return false;
            }

            if (taskContent.Attributes["Type"] != null)
            {
                _type = taskContent.Attributes["Type"].Value;
                if (_type.Length == 0)
                {
                    _log.LogErrorWithCodeFromResources("CodeTaskFactory.AttributeEmpty", "Type");
                    return false;
                }
            }

            if (taskContent.Attributes["Language"] != null)
            {
                _language = taskContent.Attributes["Language"].Value;
                if (_language.Length == 0)
                {
                    _log.LogErrorWithCodeFromResources("CodeTaskFactory.AttributeEmpty", "Language");
                    return false;
                }
            }

            if (taskContent.Attributes["Source"] != null)
            {
                _sourcePath = taskContent.Attributes["Source"].Value;

                if (_sourcePath.Length == 0)
                {
                    _log.LogErrorWithCodeFromResources("CodeTaskFactory.AttributeEmpty", "Source");
                    return false;
                }

                if (_type == null)
                {
                    _type = "Class";
                }
            }

            _referencedAssemblies = ExtractReferencedAssemblies();

            if (_log.HasLoggedErrors)
            {
                return false;
            }

            _usingNamespaces = ExtractUsingNamespaces();

            if (_log.HasLoggedErrors)
            {
                return false;
            }

            _sourceCode = taskContent.InnerText;

            if (_type == null)
            {
                _type = "Fragment";
            }

            if (_language == null)
            {
                _language = "cs";
            }

            if (String.Equals(_type, "Fragment", StringComparison.OrdinalIgnoreCase))
            {
                _typeIsFragment = true;
                _typeIsMethod = false;
            }
            else if (String.Equals(_type, "Method", StringComparison.OrdinalIgnoreCase))
            {
                _typeIsFragment = false;
                _typeIsMethod = true;
            }

            _taskParameterTypeInfo = taskParameters;

            _compiledAssembly = CompileInMemoryAssembly();

            // If it wasn't compiled, it logged why.
            // If it was, continue.
            if (_compiledAssembly != null)
            {
                // Now go find the type int he compiled assembly.
                Type[] exportedTypes = _compiledAssembly.GetExportedTypes();

                Type fullNameMatch = null;
                Type partialNameMatch = null;

                foreach (Type exportedType in exportedTypes)
                {
                    string exportedTypeName = exportedType.FullName;
                    if (exportedTypeName.Equals(_nameOfTask, StringComparison.OrdinalIgnoreCase))
                    {
                        fullNameMatch = exportedType;
                        break;
                    }
                    else if (partialNameMatch == null && exportedTypeName.EndsWith(_nameOfTask, StringComparison.OrdinalIgnoreCase))
                    {
                        partialNameMatch = exportedType;
                    }
                }

                TaskType = fullNameMatch ?? partialNameMatch;
                if (TaskType == null)
                {
                    _log.LogErrorWithCodeFromResources("CodeTaskFactory.CouldNotFindTaskInAssembly", _nameOfTask);
                }
            }

            return !_log.HasLoggedErrors;
        }

        /// <summary>
        /// Create a taskfactory instance which contains the data that needs to be refreshed between task invocations
        /// </summary>
        public ITask CreateTask(IBuildEngine loggingHost)
        {
            // The assembly will have been compiled during class factory initialization, create an instance of it
            if (_compiledAssembly != null)
            {
                // In order to use the resource strings from the tasks assembly we need to register the resources with the task logging helper.
                var log = new TaskLoggingHelper(loggingHost, _nameOfTask)
                {
                    TaskResources = AssemblyResources.PrimaryResources,
                    HelpKeywordPrefix = "MSBuild."
                };

                ITask taskInstance = Activator.CreateInstance(TaskType) as ITask;
                if (taskInstance == null)
                {
                    log.LogErrorWithCodeFromResources("CodeTaskFactory.NeedsITaskInterface", _nameOfTask);
                }

                return taskInstance;
            }
            return null;
        }

        /// <summary>
        /// Cleans up any context or state that may have been built up for a given task.
        /// </summary>
        /// <param name="task">The task to clean up.</param>
        /// <remarks>
        /// For many factories, this method is a no-op.  But some factories may have built up
        /// an AppDomain as part of an individual task instance, and this is their opportunity
        /// to shutdown the AppDomain.
        /// </remarks>
        public void CleanupTask(ITask task)
        {
            ErrorUtilities.VerifyThrowArgumentNull(task, nameof(task));
        }

        /// <summary>
        /// Create a property (with the corresponding private field) from the given type information
        /// </summary>
        private static void CreateProperty(CodeTypeDeclaration ctd, string propertyName, Type propertyType, object defaultValue)
        {
            var field = new CodeMemberField(new CodeTypeReference(propertyType), "_" + propertyName)
            {
                Attributes = MemberAttributes.Private
            };
            if (defaultValue != null)
            {
                field.InitExpression = new CodePrimitiveExpression(defaultValue);
            }

            ctd.Members.Add(field);

            var prop = new CodeMemberProperty
            {
                Name = propertyName,
                Type = new CodeTypeReference(propertyType),
                Attributes = MemberAttributes.Public,
                HasGet = true,
                HasSet = true
            };

            var fieldRef = new CodeFieldReferenceExpression { FieldName = field.Name };
            prop.GetStatements.Add(new CodeMethodReturnStatement(fieldRef));

            var fieldAssign = new CodeAssignStatement
            {
                Left = fieldRef,
                Right = new CodeArgumentReferenceExpression("value")
            };
            prop.SetStatements.Add(fieldAssign);
            ctd.Members.Add(prop);
        }

        /// <summary>
        /// Create the Execute() method for the task from the fragment of code from the <Task /> element
        /// </summary>
        private static void CreateExecuteMethodFromFragment(CodeTypeDeclaration codeTypeDeclaration, string executeCode)
        {
            var executeMethod = new CodeMemberMethod
            {
                Name = "Execute",
                Attributes = MemberAttributes.Override | MemberAttributes.Public
            };
            executeMethod.Statements.Add(new CodeSnippetStatement(executeCode));
            executeMethod.ReturnType = new CodeTypeReference(typeof(Boolean));
            executeMethod.Statements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(null, "_Success")));
            codeTypeDeclaration.Members.Add(executeMethod);
        }

        /// <summary>
        /// Create the body of the task's code by simply using the taskCode as a snippet for the CodeDom
        /// </summary>
        private static void CreateTaskBody(CodeTypeDeclaration codeTypeDeclaration, string taskCode)
        {
            var snippet = new CodeSnippetTypeMember(taskCode);
            codeTypeDeclaration.Members.Add(snippet);
        }

        /// <summary>
        /// Create a property (with the corresponding private field) from the given type information
        /// </summary>
        private static void CreateProperty(CodeTypeDeclaration codeTypeDeclaration, TaskPropertyInfo propInfo, object defaultValue)
        {
            CreateProperty(codeTypeDeclaration, propInfo.Name, propInfo.PropertyType, defaultValue);
        }

        /// <summary>
        /// Extract the <Reference /> elements from the <UsingTask />
        /// </summary>
        /// <returns>string[] of reference paths</returns>
        private string[] ExtractReferencedAssemblies()
        {
            XmlNodeList referenceNodes = _taskNode.SelectNodes("//*[local-name()='Reference']");
            var references = new List<string>();
            for (int i = 0; i < referenceNodes.Count; i++)
            {
                XmlAttribute attribute = referenceNodes[i].Attributes["Include"];

                bool hasInvalidChildNodes = HasInvalidChildNodes(referenceNodes[i], new[] { XmlNodeType.Comment, XmlNodeType.Whitespace });

                if (hasInvalidChildNodes)
                {
                    return null;
                }

                if (attribute == null || attribute.Value.Length == 0)
                {
                    _log.LogErrorWithCodeFromResources("CodeTaskFactory.AttributeEmpty", "Include");
                    return null;
                }

                references.Add(attribute.Value);
            }

            return references.ToArray();
        }

        /// <summary>
        /// Extract the <Using /> elements from the <UsingTask />
        /// </summary>
        /// <returns>string[] of using's</returns>
        private string[] ExtractUsingNamespaces()
        {
            XmlNodeList usingNodes = _taskNode.SelectNodes("//*[local-name()='Using']");

            var usings = new List<string>();
            for (int i = 0; i < usingNodes.Count; i++)
            {
                bool hasInvalidChildNodes = HasInvalidChildNodes(usingNodes[i], new[] { XmlNodeType.Comment, XmlNodeType.Whitespace });

                if (hasInvalidChildNodes)
                {
                    return null;
                }

                XmlAttribute attribute = usingNodes[i].Attributes["Namespace"];
                if (attribute == null || attribute.Value.Length == 0)
                {
                    _log.LogErrorWithCodeFromResources("CodeTaskFactory.AttributeEmpty", "Namespace");
                    return null;
                }

                usings.Add(attribute.Value);
            }

            return usings.ToArray();
        }

        /// <summary>
        /// Extract the <Task /> node from the UsingTask node
        /// </summary>
        /// <param name="taskElementContents">textual content of the <Task /> node</param>
        /// <returns>XmlNode <Task /></returns>
        private XmlNode ExtractTaskContent(string taskElementContents)
        {
            // We need to get the InnerXml of the <Task /> node back into
            // a root node so that we can execute the appropriate XPath on it
            var document = new XmlDocument();

            _taskNode = document.CreateElement("Task");
            document.AppendChild(_taskNode);

            // record our internal representation of the <Task /> node
            _taskNode.InnerXml = taskElementContents;

            XmlNodeList codeNodes = _taskNode.SelectNodes("//*[local-name()='Code']");

            if (codeNodes.Count > 1)
            {
                _log.LogErrorWithCodeFromResources("CodeTaskFactory.MultipleCodeNodes");
                return null;
            }
            else if (codeNodes.Count == 0)
            {
                _log.LogErrorWithCodeFromResources("CodeTaskFactory.CodeElementIsMissing", _nameOfTask);
                return null;
            }

            bool hasInvalidChildNodes = HasInvalidChildNodes(codeNodes[0], new[] { XmlNodeType.Comment, XmlNodeType.Whitespace, XmlNodeType.Text, XmlNodeType.CDATA });

            if (hasInvalidChildNodes)
            {
                return null;
            }

            return codeNodes[0];
        }

        /// <summary>
        /// Make sure the task node only contains Code, Reference, Usings
        /// </summary>
        private bool ValidateTaskNode()
        {
            if (_taskNode.HasChildNodes)
            {
                foreach (XmlNode childNode in _taskNode.ChildNodes)
                {
                    bool foundInvalidNode;
                    switch (childNode.NodeType)
                    {
                        case XmlNodeType.Comment:
                        case XmlNodeType.Whitespace:
                        case XmlNodeType.Text:
                            // These are legal, and ignored
                            continue;
                        case XmlNodeType.Element:
                            if (childNode.Name.Equals("Code", StringComparison.OrdinalIgnoreCase) || childNode.Name.Equals("Reference", StringComparison.OrdinalIgnoreCase) || childNode.Name.Equals("Using", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            else
                            {
                                foundInvalidNode = true;
                            }

                            break;
                        default:
                            foundInvalidNode = true;
                            break;
                    }

                    if (foundInvalidNode)
                    {
                        _log.LogErrorWithCodeFromResources("CodeTaskFactory.InvalidElementLocation", childNode.Name, _taskNode.Name);
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// If a parent node has a child node and it is not supposed to, log an error indicating it has an invalid element.
        /// </summary>
        private bool HasInvalidChildNodes(XmlNode parentNode, XmlNodeType[] allowedNodeTypes)
        {
            bool hasInvalidNode = false;
            if (parentNode.HasChildNodes)
            {
                foreach (XmlNode childNode in parentNode.ChildNodes)
                {
                    bool elementAllowed = false;
                    foreach (XmlNodeType nodeType in allowedNodeTypes)
                    {
                        if (nodeType == childNode.NodeType)
                        {
                            elementAllowed = true;
                            break;
                        }
                    }

                    if (!elementAllowed)
                    {
                        _log.LogErrorWithCodeFromResources("CodeTaskFactory.InvalidElementLocation", childNode.Name, parentNode.Name);
                        hasInvalidNode = true;
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            return hasInvalidNode;
        }

        /// <summary>
        /// Add a reference assembly to the list of references passed to the compiler. We will try and load the assembly to make sure it is found 
        /// before sending it to the compiler. The reason we load here is that we will be using it in this appdomin anyways as soon as we are going to compile, which should be right away.
        /// </summary>
        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadWithPartialName", Justification = "Necessary since we don't have the full assembly name. ")]
        private void AddReferenceAssemblyToReferenceList(List<string> referenceAssemblyList, string referenceAssembly)
        {
            if (referenceAssemblyList != null)
            {
                string candidateAssemblyLocation = null;

                if (!String.IsNullOrEmpty(referenceAssembly))
                {
                    try
                    {
                        bool fileExists = File.Exists(referenceAssembly);
                        if (!fileExists)
                        {
                            if (!referenceAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || !referenceAssembly.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
#pragma warning disable 618, 612
                                // Unfortunately Assembly.Load is not an alternative to LoadWithPartialName, since
                                // Assembly.Load requires the full assembly name to be passed to it.
                                // Therefore we must ignore the deprecated warning.
                                Assembly candidateAssembly = Assembly.LoadWithPartialName(referenceAssembly);
                                if (candidateAssembly != null)
                                {
                                    candidateAssemblyLocation = candidateAssembly.Location;
                                }
                                else if (NativeMethodsShared.IsMono)
                                {
                                    string path = Path.Combine(
                                        NativeMethodsShared.FrameworkCurrentPath,
                                        "Facades",
                                        Path.GetFileName(referenceAssembly));
                                    if (!File.Exists(path))
                                    {
                                        var newPath = path + ".dll";
                                        path = !File.Exists(newPath) ? path + ".exe" : newPath;
                                    }
                                    candidateAssembly = Assembly.UnsafeLoadFrom(path);
                                    if (candidateAssembly != null)
                                    {
                                        candidateAssemblyLocation = candidateAssembly.Location;
                                    }
                                }
#pragma warning restore 618, 612
                            }
                        }
                        else
                        {
                            try
                            {
                                Assembly candidateAssembly = Assembly.UnsafeLoadFrom(referenceAssembly);
                                if (candidateAssembly != null)
                                {
                                    candidateAssemblyLocation = candidateAssembly.Location;
                                    s_knownReferenceAssemblies[candidateAssembly.FullName] = candidateAssembly;
                                }
                            }
                            catch (BadImageFormatException e)
                            {
                                Debug.Assert(e.Message.Contains("0x80131058"), "Expected Message to contain 0x80131058");
                                AssemblyName.GetAssemblyName(referenceAssembly);
                                candidateAssemblyLocation = referenceAssembly;
                                _log.LogMessageFromResources(MessageImportance.Low, "CodeTaskFactory.HaveReflectionOnlyAssembly", referenceAssembly);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (ExceptionHandling.IsCriticalException(e))
                        {
                            throw;
                        }

                        _log.LogErrorWithCodeFromResources("CodeTaskFactory.ReferenceAssemblyIsInvalid", referenceAssembly, e.Message);
                    }
                }

                if (candidateAssemblyLocation != null)
                {
                    referenceAssemblyList.Add(candidateAssemblyLocation);
                }
                else
                {
                    _log.LogErrorWithCodeFromResources("CodeTaskFactory.CouldNotFindReferenceAssembly", referenceAssembly);
                }
            }
        }

        /// <summary>
        /// Compile the assembly in memory and get a reference to the assembly itself.
        /// If compilation fails, returns null.
        /// </summary>
        private Assembly CompileInMemoryAssembly()
        {
            // Combine our default assembly references with those specified
            List<string> finalReferencedAssemblies = new List<string>();
            CombineReferencedAssemblies(finalReferencedAssemblies);

            // Combine our default using's with those specified
            string[] finalUsingNamespaces = CombineUsingNamespaces();

            // Language can be anything that has a codedom provider, in the standard naming method
            // "c#;cs;csharp", "vb;vbs;visualbasic;vbscript", "js;jscript;javascript", "vj#;vjs;vjsharp", "c++;mc;cpp"
            using (CodeDomProvider provider = CodeDomProvider.CreateProvider(_language))
            {
                if (provider is CSharp.CSharpCodeProvider)
                {
                    AddReferenceAssemblyToReferenceList(finalReferencedAssemblies, "System");
                }

                var compilerParameters =
                    new CompilerParameters(finalReferencedAssemblies.ToArray())
                    {
                        // We don't need debug information
                        IncludeDebugInformation = true,

                        // Not a file based assembly
                        GenerateInMemory = true,

                        // Indicates that a .dll should be generated.
                        GenerateExecutable = false
                    };

                // Horrible code dom / compilation declarations
                var codeBuilder = new StringBuilder();
                var writer = new StringWriter(codeBuilder, CultureInfo.CurrentCulture);
                var codeGeneratorOptions = new CodeGeneratorOptions
                {
                    BlankLinesBetweenMembers = true,
                    VerbatimOrder = true
                };
                var compilationUnit = new CodeCompileUnit();

                // If our code is in a separate file, then read it in here
                if (_sourcePath != null)
                {
                    _sourceCode = File.ReadAllText(_sourcePath);
                }

                // A fragment is essentially the contents of the execute method (except the final return true/false)
                // A method is the whole execute method specified
                // Anything else assumes that the whole class is being supplied
                if (_typeIsFragment || _typeIsMethod)
                {
                    CodeTypeDeclaration codeTypeDeclaration = CreateTaskClass();

                    CreateTaskProperties(codeTypeDeclaration);

                    if (_typeIsFragment)
                    {
                        CreateExecuteMethodFromFragment(codeTypeDeclaration, _sourceCode);
                    }
                    else
                    {
                        CreateTaskBody(codeTypeDeclaration, _sourceCode);
                    }

                    var codeNamespace = new CodeNamespace("InlineCode");
                    foreach (string importname in finalUsingNamespaces)
                    {
                        codeNamespace.Imports.Add(new CodeNamespaceImport(importname));
                    }

                    codeNamespace.Types.Add(codeTypeDeclaration);
                    compilationUnit.Namespaces.Add(codeNamespace);

                    // Create the source for the CodeDom
                    provider.GenerateCodeFromCompileUnit(compilationUnit, writer, codeGeneratorOptions);
                }
                else
                {
                    // We are a full class, so just create the CodeDom from the source
                    provider.GenerateCodeFromStatement(new CodeSnippetStatement(_sourceCode), writer, codeGeneratorOptions);
                }

                // Our code generation is complete, grab the source from the builder ready for compilation
                string fullCode = codeBuilder.ToString();

                var fullSpec = new FullTaskSpecification(finalReferencedAssemblies, fullCode);
                if (!s_compiledTaskCache.TryGetValue(fullSpec, out Assembly existingAssembly))
                {
                    // Invokes compilation. 

                    // Note: CompileAssemblyFromSource uses Path.GetTempPath() directory, but will not create it. In some cases 
                    // this will throw inside CompileAssemblyFromSource. To work around this, ensure the temp directory exists. 
                    // See: https://github.com/Microsoft/msbuild/issues/328
                    Directory.CreateDirectory(Path.GetTempPath());

                    CompilerResults compilerResults = provider.CompileAssemblyFromSource(compilerParameters, fullCode);

                    string outputPath = null;
                    if (compilerResults.Errors.Count > 0 || Environment.GetEnvironmentVariable("MSBUILDLOGCODETASKFACTORYOUTPUT") != null)
                    {
                        string tempDirectory = Path.GetTempPath();
                        string fileName = Guid.NewGuid().ToString() + ".txt";
                        outputPath = Path.Combine(tempDirectory, fileName);
                        File.WriteAllText(outputPath, fullCode);
                    }

                    if (compilerResults.NativeCompilerReturnValue != 0 && compilerResults.Errors.Count > 0)
                    {
                        _log.LogErrorWithCodeFromResources("CodeTaskFactory.FindSourceFileAt", outputPath);

                        foreach (CompilerError e in compilerResults.Errors)
                        {
                            _log.LogErrorWithCodeFromResources("CodeTaskFactory.CompilerError", e.ToString());
                        }

                        return null;
                    }

                    // Add to the cache.  Failing to add is not a fatal error.
                    s_compiledTaskCache.TryAdd(fullSpec, compilerResults.CompiledAssembly);
                    return compilerResults.CompiledAssembly;
                }
                else
                {
                    return existingAssembly;
                }
            }
        }

        /// <summary>
        /// Combine our default referenced assemblies with those explicitly specified
        /// </summary>
        private void CombineReferencedAssemblies(List<string> finalReferenceList)
        {
            foreach (string defaultReference in DefaultReferencedAssemblies)
            {
                AddReferenceAssemblyToReferenceList(finalReferenceList, defaultReference);
            }

            if (_referencedAssemblies != null)
            {
                foreach (string referenceAssembly in _referencedAssemblies)
                {
                    AddReferenceAssemblyToReferenceList(finalReferenceList, referenceAssembly);
                }
            }
        }

        /// <summary>
        /// Combine our default imported namespaces with those explicitly specified
        /// </summary>
        private string[] CombineUsingNamespaces()
        {
            int usingNamespaceCount = _defaultUsingNamespaces.Length;

            if (_usingNamespaces != null)
            {
                usingNamespaceCount += _usingNamespaces.Length;
            }

            string[] finalUsingNamespaces = new string[usingNamespaceCount];
            _defaultUsingNamespaces.CopyTo(finalUsingNamespaces, 0);
            _usingNamespaces?.CopyTo(finalUsingNamespaces, _defaultUsingNamespaces.Length);

            return finalUsingNamespaces;
        }

        /// <summary>
        /// Create the task properties
        /// </summary>
        private void CreateTaskProperties(CodeTypeDeclaration codeTypeDeclaration)
        {
            // If we are only a fragment, then create a default task parameter called
            // Success - that we can use in the fragment to indicate success or failure of the task
            if (_typeIsFragment)
            {
                CreateProperty(codeTypeDeclaration, "Success", typeof(bool), true);
            }

            foreach (TaskPropertyInfo propInfo in _taskParameterTypeInfo.Values)
            {
                CreateProperty(codeTypeDeclaration, propInfo, null);
            }
        }

        /// <summary>
        /// Create the task class
        /// </summary>
        private CodeTypeDeclaration CreateTaskClass()
        {
            CodeTypeDeclaration codeTypeDeclaration = new CodeTypeDeclaration
            {
                IsClass = true,
                Name = _nameOfTask,
                TypeAttributes = TypeAttributes.Public,
                Attributes = MemberAttributes.Final
            };
            codeTypeDeclaration.BaseTypes.Add("Microsoft.Build.Utilities.Task");
            return codeTypeDeclaration;
        }

        /// <summary>
        /// Class used as a key for the compiled assembly cache
        /// </summary>
        private class FullTaskSpecification : IComparable<FullTaskSpecification>, IEquatable<FullTaskSpecification>
        {
            /// <summary>
            /// The set of assemblies referenced by this task.
            /// </summary>
            private readonly List<string> _referenceAssemblies;

            /// <summary>
            /// The complete source code for the task.
            /// </summary>
            private readonly string _fullCode;

            /// <summary>
            /// Constructor
            /// </summary>
            public FullTaskSpecification(List<string> references, string fullCode)
            {
                _referenceAssemblies = references;
                _fullCode = fullCode;
            }

            /// <summary>
            /// Override of GetHashCode
            /// </summary>
            public override int GetHashCode()
            {
                return _fullCode.GetHashCode();
            }

            /// <summary>
            /// Override of Equals
            /// </summary>
            public override bool Equals(object other)
            {
                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (!(other is FullTaskSpecification otherSpec))
                {
                    return false;
                }

                return ((IEquatable<FullTaskSpecification>)this).Equals(otherSpec);
            }

            /// <summary>
            /// Implementation of Equals.
            /// </summary>
            bool IEquatable<FullTaskSpecification>.Equals(FullTaskSpecification other)
            {
                if (_referenceAssemblies.Count != other._referenceAssemblies.Count)
                {
                    return false;
                }

                for (int i = 0; i < _referenceAssemblies.Count; i++)
                {
                    if (_referenceAssemblies[i] != other._referenceAssemblies[i])
                    {
                        return false;
                    }
                }

                return other._fullCode == _fullCode;
            }

            /// <summary>
            /// Implementation of CompareTo
            /// </summary>
            int IComparable<FullTaskSpecification>.CompareTo(FullTaskSpecification other)
            {
                int result = Comparer<int>.Default.Compare(_referenceAssemblies.Count, other._referenceAssemblies.Count);
                if (result == 0)
                {
                    result = Comparer<string>.Default.Compare(_fullCode, other._fullCode);
                }

                return result;
            }
        }
    }
#else
    /// <summary>
    /// A task factory which can take code dom supported languages and create a task out of it
    /// </summary>
    /// <remarks>CodeDom is not supported for .NET Core so this code task factory simply logs an error that it isn't supported.
    /// If we don't compile this class, then the user will get an error that the class doesn't exist which is a bad experience.</remarks>
    [Obsolete("The CodeTaskFactory is not supported on .NET Core.  This class is included so that users receive run-time errors and should not be used for any other purpose.", error: true)]
    public sealed class CodeTaskFactory : ITaskFactory
    {
        public string FactoryName => "Code Task Factory";

        public Type TaskType { get; } = null;

        public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost)
        {
            TaskLoggingHelper log = new TaskLoggingHelper(taskFactoryLoggingHost, taskName)
            {
                TaskResources = AssemblyResources.PrimaryResources,
                HelpKeywordPrefix = "MSBuild."
            };
            
            log.LogErrorFromResources("TaskFactoryNotSupportedFailure", nameof(CodeTaskFactory));

            return false;
        }

        public TaskPropertyInfo[] GetTaskParameters()
        {
            throw new NotSupportedException();
        }

        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost)
        {
            throw new NotSupportedException();
        }

        public void CleanupTask(ITask task)
        {
            throw new NotSupportedException();
        }
    }
#endif
}
