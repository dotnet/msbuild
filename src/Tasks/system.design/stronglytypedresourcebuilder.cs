// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// ************************************************************************************************
// ************************************************************************************************
// Extracted in order to remove System.Designer dependency from Microsoft.Build.Tasks.
// When they add typeforwarders for StronglyTypedResourceBuilder this can be removed.
// Almost completely unchanged, except for visibility and namespace.
//
// When making changes to this file, consider whether the changes also need to be made in
// venus\project\webapp\package\GlobalResourceProxyGenerator.cs
// ************************************************************************************************
// ************************************************************************************************


//------------------------------------------------------------------------------
// </copyright>                                                                
//------------------------------------------------------------------------------
/*============================================================
**
** Purpose: Uses CodeDOM to produce a resource class, with a
**          strongly-typed property for every resource.
**          For usability & eventually some perf work.
**
**
===========================================================*/

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Resources;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;

/*
  Plan for the future:
  Ideally we will be able to change the property getters here to use a
  resource index calculated at build time, being the x'th resource in the
  .resources file.  We would then call something like 
  ResourceManager.LookupResourceByIndex().  This would avoid some string
  comparisons during resource lookup.

  This would require work from ResourceReader and/or ResourceWriter (or 
  a standalone, separate utility with duplicated code) to calculate the
  id's.  It would also require that all satellite assemblies use the same
  resource ID's as the main assembly.  This would require dummy entries
  for resources in all satellites.

  I'm not sure how much time this will save, but it does sound like an
  interesting idea.
         -- Brian Grunkemeyer, 1/16/2003
*/


namespace Microsoft.Build.Tasks
{
    internal static class StronglyTypedResourceBuilder
    {
        // Note - if you add a new property to the class, add logic to reject
        // keys of that name in VerifyResourceNames.
        private const String ResMgrFieldName = "resourceMan";
        private const String ResMgrPropertyName = "ResourceManager";
        private const String CultureInfoFieldName = "resourceCulture";
        private const String CultureInfoPropertyName = "Culture";

        // When fixing up identifiers, we will replace all these chars with
        // a single char that is valid in identifiers, such as '_'.
        private static readonly char[] s_charsToReplace = new char[] { ' ',
        '\u00A0' /* non-breaking space */, '.', ',', ';', '|', '~', '@',
        '#', '%', '^', '&', '*', '+', '-', '/', '\\', '<', '>', '?', '[',
        ']', '(', ')', '{', '}', '\"', '\'', ':', '!' };
        private const char ReplacementChar = '_';

        private const String DocCommentSummaryStart = "<summary>";
        private const String DocCommentSummaryEnd = "</summary>";

        // Maximum size of a String resource to show in the doc comment for its property
        private const int DocCommentLengthThreshold = 512;

        // Save the strings for better doc comments.
        internal sealed class ResourceData
        {
            internal ResourceData(Type type, String valueAsString)
            {
                Type = type;
                ValueAsString = valueAsString;
            }

            internal Type Type { get; }

            internal String ValueAsString { get; }
        }

        internal static CodeCompileUnit Create(IDictionary resourceList, String baseName, String generatedCodeNamespace, CodeDomProvider codeProvider, bool internalClass, out String[] unmatchable)
        {
            return Create(resourceList, baseName, generatedCodeNamespace, null, codeProvider, internalClass, out unmatchable);
        }

        internal static CodeCompileUnit Create(IDictionary resourceList, String baseName, String generatedCodeNamespace, String resourcesNamespace, CodeDomProvider codeProvider, bool internalClass, out String[] unmatchable)
        {
            if (resourceList == null)
            {
                throw new ArgumentNullException(nameof(resourceList));
            }

            var resourceTypes = new Dictionary<String, ResourceData>(StringComparer.InvariantCultureIgnoreCase);
            foreach (DictionaryEntry de in resourceList)
            {
                var node = de.Value as ResXDataNode;
                ResourceData data;
                if (node != null)
                {
                    string keyname = (string)de.Key;
                    if (keyname != node.Name)
                    {
                        throw new ArgumentException(SR.GetString(SR.MismatchedResourceName, keyname, node.Name));
                    }

                    String typeName = node.GetValueTypeName((AssemblyName[])null);
                    Type type = Type.GetType(typeName);
                    String valueAsString = node.GetValue((AssemblyName[])null).ToString();
                    data = new ResourceData(type, valueAsString);
                }
                else
                {
                    // If the object is null, we don't have a good way of guessing the
                    // type.  Use Object.  This will be rare after WinForms gets away
                    // from their resource pull model in Whidbey M3.
                    Type type = de.Value?.GetType() ?? typeof(Object);
                    data = new ResourceData(type, de.Value?.ToString());
                }
                resourceTypes.Add((String)de.Key, data);
            }

            // Note we still need to verify the resource names are valid language
            // keywords, etc.  So there's no point to duplicating the code above.

            return InternalCreate(resourceTypes, baseName, generatedCodeNamespace, resourcesNamespace, codeProvider, internalClass, out unmatchable);
        }

        private static CodeCompileUnit InternalCreate(Dictionary<String, ResourceData> resourceList, String baseName, String generatedCodeNamespace, String resourcesNamespace, CodeDomProvider codeProvider, bool internalClass, out String[] unmatchable)
        {
            if (baseName == null)
            {
                throw new ArgumentNullException(nameof(baseName));
            }
            if (codeProvider == null)
            {
                throw new ArgumentNullException(nameof(codeProvider));
            }

            // Keep a list of errors describing known strings that couldn't be
            // fixed up (like "4"), as well as listing all duplicate resources that
            // were fixed up to the same name (like "A B" and "A-B" both going to
            // "A_B").
            var errors = new List<string>();

            // Verify the resource names are valid property names, and they don't
            // conflict.  This includes checking for language-specific keywords,
            // translating spaces to underscores, etc.
            SortedList<string, ResourceData> cleanedResourceList = VerifyResourceNames(resourceList, codeProvider, errors, out Dictionary<string, string> reverseFixupTable);

            // Verify the class name is legal.
            String className = baseName;
            // Attempt to fix up class name, and throw an exception if it fails.
            if (!codeProvider.IsValidIdentifier(className))
            {
                String fixedClassName = VerifyResourceName(className, codeProvider);
                if (fixedClassName != null)
                {
                    className = fixedClassName;
                }
            }

            if (!codeProvider.IsValidIdentifier(className))
            {
                throw new ArgumentException(SR.GetString(SR.InvalidIdentifier, className));
            }

            // If we have a namespace, verify the namespace is legal, 
            // attempting to fix it up if needed.
            if (!String.IsNullOrEmpty(generatedCodeNamespace))
            {
                if (!codeProvider.IsValidIdentifier(generatedCodeNamespace))
                {
                    String fixedNamespace = VerifyResourceName(generatedCodeNamespace, codeProvider, true);
                    if (fixedNamespace != null)
                    {
                        generatedCodeNamespace = fixedNamespace;
                    }
                }
                // Note we cannot really ensure that the generated code namespace
                // is a valid identifier, as namespaces can have '.' and '::', but
                // identifiers cannot.
            }

            var ccu = new CodeCompileUnit();
            ccu.ReferencedAssemblies.Add("System.dll");

            ccu.UserData.Add("AllowLateBound", false);
            ccu.UserData.Add("RequireVariableDeclaration", true);

            var ns = new CodeNamespace(generatedCodeNamespace);
            ns.Imports.Add(new CodeNamespaceImport("System"));
            ccu.Namespaces.Add(ns);

            // Generate class
            var srClass = new CodeTypeDeclaration(className);
            ns.Types.Add(srClass);
            AddGeneratedCodeAttributeforMember(srClass);

            TypeAttributes ta = internalClass ? TypeAttributes.NotPublic : TypeAttributes.Public;
            //ta |= TypeAttributes.Sealed;
            srClass.TypeAttributes = ta;
            srClass.Comments.Add(new CodeCommentStatement(DocCommentSummaryStart, true));
            srClass.Comments.Add(new CodeCommentStatement(SR.GetString(SR.ClassDocComment), true));

            var comment = new CodeCommentStatement(SR.GetString(SR.ClassComments1), true);
            srClass.Comments.Add(comment);
            comment = new CodeCommentStatement(SR.GetString(SR.ClassComments3), true);
            srClass.Comments.Add(comment);

            srClass.Comments.Add(new CodeCommentStatement(DocCommentSummaryEnd, true));
            var debuggerAttrib =
                new CodeTypeReference(typeof(System.Diagnostics.DebuggerNonUserCodeAttribute))
                {
                    Options = CodeTypeReferenceOptions.GlobalReference
                };
            srClass.CustomAttributes.Add(new CodeAttributeDeclaration(debuggerAttrib));

            var compilerGenedAttrib =
                new CodeTypeReference(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))
                {
                    Options = CodeTypeReferenceOptions.GlobalReference
                };
            srClass.CustomAttributes.Add(new CodeAttributeDeclaration(compilerGenedAttrib));

            // Figure out some basic restrictions to the code generation
            bool useStatic = internalClass || codeProvider.Supports(GeneratorSupport.PublicStaticMembers);
            EmitBasicClassMembers(srClass, generatedCodeNamespace, baseName, resourcesNamespace, internalClass, useStatic);

            // Now for each resource, add a property
            foreach (KeyValuePair<string, ResourceData> entry in cleanedResourceList)
            {
                String propertyName = entry.Key;
                // The resourceName will be the original value, before fixups, if any.
                if (reverseFixupTable.TryGetValue(propertyName, out string resourceName))
                {
                    resourceName = propertyName;
                }
                bool r = DefineResourceFetchingProperty(propertyName, resourceName, entry.Value, srClass, internalClass, useStatic);
                if (!r)
                {
                    errors.Add(propertyName);
                }
            }

            unmatchable = errors.ToArray();

            // Validate the generated class now
            CodeGenerator.ValidateIdentifiers(ccu);

            return ccu;
        }

        internal static CodeCompileUnit Create(String resxFile, String baseName, String generatedCodeNamespace, CodeDomProvider codeProvider, bool internalClass, out String[] unmatchable)
        {
            return Create(resxFile, baseName, generatedCodeNamespace, null, codeProvider, internalClass, out unmatchable);
        }

        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
        internal static CodeCompileUnit Create(String resxFile, String baseName, String generatedCodeNamespace, String resourcesNamespace, CodeDomProvider codeProvider, bool internalClass, out String[] unmatchable)
        {
            if (resxFile == null)
            {
                throw new ArgumentNullException(nameof(resxFile));
            }

            // Read the resources from a ResX file into a dictionary - name & type name
            Dictionary<String, ResourceData> resourceList = new Dictionary<String, ResourceData>(StringComparer.InvariantCultureIgnoreCase);
            using (ResXResourceReader rr = new ResXResourceReader(resxFile))
            {
                rr.UseResXDataNodes = true;
                foreach (DictionaryEntry de in rr)
                {
                    var node = (ResXDataNode)de.Value;
                    String typeName = node.GetValueTypeName((AssemblyName[])null);
                    Type type = Type.GetType(typeName);
                    String valueAsString = node.GetValue((AssemblyName[])null).ToString();
                    var data = new ResourceData(type, valueAsString);
                    resourceList.Add((String)de.Key, data);
                }
            }

            // Note we still need to verify the resource names are valid language
            // keywords, etc.  So there's no point to duplicating the code above.

            return InternalCreate(resourceList, baseName, generatedCodeNamespace, resourcesNamespace, codeProvider, internalClass, out unmatchable);
        }

        private static void AddGeneratedCodeAttributeforMember(CodeTypeMember typeMember)
        {
            var generatedCodeAttrib = new CodeAttributeDeclaration(new CodeTypeReference(typeof(GeneratedCodeAttribute)));
            generatedCodeAttrib.AttributeType.Options = CodeTypeReferenceOptions.GlobalReference;
            var toolArg = new CodeAttributeArgument(new CodePrimitiveExpression(typeof(StronglyTypedResourceBuilder).FullName));
            var versionArg = new CodeAttributeArgument(new CodePrimitiveExpression(MSBuildConstants.CurrentAssemblyVersion));

            generatedCodeAttrib.Arguments.Add(toolArg);
            generatedCodeAttrib.Arguments.Add(versionArg);

            typeMember.CustomAttributes.Add(generatedCodeAttrib);
        }

        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters")]
        private static void EmitBasicClassMembers(CodeTypeDeclaration srClass, String nameSpace, String baseName, String resourcesNamespace, bool internalClass, bool useStatic)
        {
            const String tmpVarName = "temp";
            String resMgrCtorParam;

            if (resourcesNamespace != null)
            {
                if (resourcesNamespace.Length > 0)
                    resMgrCtorParam = resourcesNamespace + '.' + baseName;
                else
                    resMgrCtorParam = baseName;
            }
            else if (!string.IsNullOrEmpty(nameSpace))
            {
                resMgrCtorParam = nameSpace + '.' + baseName;
            }
            else
            {
                resMgrCtorParam = baseName;
            }

            var suppressMessageAttrib = new CodeAttributeDeclaration(new CodeTypeReference(typeof(SuppressMessageAttribute)));
            suppressMessageAttrib.AttributeType.Options = CodeTypeReferenceOptions.GlobalReference;
            suppressMessageAttrib.Arguments.Add(new CodeAttributeArgument(new CodePrimitiveExpression("Microsoft.Performance")));
            suppressMessageAttrib.Arguments.Add(new CodeAttributeArgument(new CodePrimitiveExpression("CA1811:AvoidUncalledPrivateCode")));

            // Emit a constructor - make it protected even if it is a "static" class to allow subclassing
            CodeConstructor ctor = new CodeConstructor();
            ctor.CustomAttributes.Add(suppressMessageAttrib);
            if (useStatic || internalClass)
                ctor.Attributes = MemberAttributes.FamilyAndAssembly;
            else
                ctor.Attributes = MemberAttributes.Public;
            srClass.Members.Add(ctor);

            // Emit _resMgr field.
            var ResMgrCodeTypeReference = new CodeTypeReference(typeof(ResourceManager), CodeTypeReferenceOptions.GlobalReference);
            var field = new CodeMemberField(ResMgrCodeTypeReference, ResMgrFieldName)
            {
                Attributes = MemberAttributes.Private
            };
            if (useStatic)
                field.Attributes |= MemberAttributes.Static;
            srClass.Members.Add(field);

            // Emit _resCulture field, and leave it set to null.
            var CultureTypeReference = new CodeTypeReference(typeof(CultureInfo), CodeTypeReferenceOptions.GlobalReference);
            field = new CodeMemberField(CultureTypeReference, CultureInfoFieldName);
            field.Attributes = MemberAttributes.Private;
            if (useStatic)
                field.Attributes |= MemberAttributes.Static;
            srClass.Members.Add(field);

            // Emit ResMgr property
            CodeMemberProperty resMgr = new CodeMemberProperty();
            srClass.Members.Add(resMgr);
            resMgr.Name = ResMgrPropertyName;
            resMgr.HasGet = true;
            resMgr.HasSet = false;
            resMgr.Type = ResMgrCodeTypeReference;
            if (internalClass)
                resMgr.Attributes = MemberAttributes.Assembly;
            else
                resMgr.Attributes = MemberAttributes.Public;
            if (useStatic)
                resMgr.Attributes |= MemberAttributes.Static;

            // Mark the ResMgr property as advanced
            var editorBrowsableStateTypeRef =
                new CodeTypeReference(typeof(System.ComponentModel.EditorBrowsableState))
                {
                    Options = CodeTypeReferenceOptions.GlobalReference
                };

            var editorBrowsableStateAdvanced = new CodeAttributeArgument(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(editorBrowsableStateTypeRef), "Advanced"));
            var editorBrowsableAdvancedAttribute = new CodeAttributeDeclaration("System.ComponentModel.EditorBrowsableAttribute", editorBrowsableStateAdvanced);
            editorBrowsableAdvancedAttribute.AttributeType.Options = CodeTypeReferenceOptions.GlobalReference;
            resMgr.CustomAttributes.Add(editorBrowsableAdvancedAttribute);

            // Emit the Culture property (read/write)
            var culture = new CodeMemberProperty();
            srClass.Members.Add(culture);
            culture.Name = CultureInfoPropertyName;
            culture.HasGet = true;
            culture.HasSet = true;
            culture.Type = CultureTypeReference;
            if (internalClass)
                culture.Attributes = MemberAttributes.Assembly;
            else
                culture.Attributes = MemberAttributes.Public;

            if (useStatic)
                culture.Attributes |= MemberAttributes.Static;

            // Mark the Culture property as advanced
            culture.CustomAttributes.Add(editorBrowsableAdvancedAttribute);
            
            /*
              // Here's what I'm trying to emit.  Since not all languages support
              // try/finally, we'll avoid our double lock pattern here.
              // This will only hurt perf when we get two threads racing through
              // this method the first time.  Unfortunate, but not a big deal.
              // Also, the .NET Compact Framework doesn't support 
              // Thread.MemoryBarrier (they only run on processors w/ a strong 
              // memory model, and who knows about IA64...)
              // Once we have Interlocked.CompareExchange<T>, we should use it here.
              if (_resMgr == null) {
                  ResourceManager tmp = new ResourceManager("<resources-name-with-namespace>", typeof("<class-name>").Assembly);
                  _resMgr = tmp;
              }
              return _resMgr;
             */
            var field_resMgr = new CodeFieldReferenceExpression(null, ResMgrFieldName);
            var object_equalsMethod = new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(Object)), "ReferenceEquals");

            var isResMgrNull = new CodeMethodInvokeExpression(object_equalsMethod, field_resMgr, new CodePrimitiveExpression(null));

            // typeof(<class-name>).Assembly
            var getAssembly = new CodePropertyReferenceExpression(new CodeTypeOfExpression(new CodeTypeReference(srClass.Name)), "Assembly");

            // new ResourceManager(resMgrCtorParam, typeof(<class-name>).Assembly);
            var newResMgr = new CodeObjectCreateExpression(ResMgrCodeTypeReference, new CodePrimitiveExpression(resMgrCtorParam), getAssembly);

            var init = new CodeStatement[2];
            init[0] = new CodeVariableDeclarationStatement(ResMgrCodeTypeReference, tmpVarName, newResMgr);
            init[1] = new CodeAssignStatement(field_resMgr, new CodeVariableReferenceExpression(tmpVarName));

            resMgr.GetStatements.Add(new CodeConditionStatement(isResMgrNull, init));
            resMgr.GetStatements.Add(new CodeMethodReturnStatement(field_resMgr));

            // Add a doc comment to the ResourceManager property
            resMgr.Comments.Add(new CodeCommentStatement(DocCommentSummaryStart, true));
            resMgr.Comments.Add(new CodeCommentStatement(SR.GetString(SR.ResMgrPropertyComment), true));
            resMgr.Comments.Add(new CodeCommentStatement(DocCommentSummaryEnd, true));

            // Emit code for Culture property
            var field_resCulture = new CodeFieldReferenceExpression(null, CultureInfoFieldName);
            culture.GetStatements.Add(new CodeMethodReturnStatement(field_resCulture));

            var newCulture = new CodePropertySetValueReferenceExpression();
            culture.SetStatements.Add(new CodeAssignStatement(field_resCulture, newCulture));

            // Add a doc comment to Culture property
            culture.Comments.Add(new CodeCommentStatement(DocCommentSummaryStart, true));
            culture.Comments.Add(new CodeCommentStatement(SR.GetString(SR.CulturePropertyComment1), true));
            culture.Comments.Add(new CodeCommentStatement(SR.GetString(SR.CulturePropertyComment2), true));
            culture.Comments.Add(new CodeCommentStatement(DocCommentSummaryEnd, true));
        }

        // Helper method for DefineResourceFetchingProperty
        // Truncates a comment string if it is too long and ensures it is safely encoded for XML.
        private static string TruncateAndFormatCommentStringForOutput(string commentString)
        {
            if (commentString != null)
            {
                // Stop at some length
                if (commentString.Length > DocCommentLengthThreshold)
                    commentString = SR.GetString(SR.StringPropertyTruncatedComment, commentString.Substring(0, DocCommentLengthThreshold));

                // Encode the comment so it is safe for xml.  SecurityElement.Escape is the only method I've found to do this. 
                commentString = System.Security.SecurityElement.Escape(commentString);
            }

            return commentString;
        }

        // Defines a property like this:
        // {internal|internal} {static} Point MyPoint {
        //     get {
        //          Object obj = ResourceManager.GetObject("MyPoint", _resCulture);
        //          return (Point) obj; }
        // }
        // Special cases static vs. non-static, as well as internal vs. internal.
        // Also note the resource name could contain spaces, etc, while the 
        // property name has to be a valid language identifier.
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters")]
        private static bool DefineResourceFetchingProperty(String propertyName, String resourceName, ResourceData data, CodeTypeDeclaration srClass, bool internalClass, bool useStatic)
        {
            var prop = new CodeMemberProperty
            {
                Name = propertyName,
                HasGet = true,
                HasSet = false
            };

            Type type = data.Type;
            if (type == null)
            {
                return false;
            }

            if (type == typeof(MemoryStream))
            {
                type = typeof(UnmanagedMemoryStream);
            }

            // Ensure type is internalally visible.  This is necessary to ensure
            // users can access classes via a base type.  Imagine a class like
            // Image or Stream as a internalally available base class, then an 
            // internal type like MyBitmap or __UnmanagedMemoryStream as an 
            // internal implementation for that base class.  For internalally 
            // available strongly typed resource classes, we must return the 
            // internal type.  For simplicity, we'll do that for internal strongly 
            // typed resource classes as well.  Ideally we'd also like to check
            // for interfaces like IList, but I don't know how to do that without
            // special casing collection interfaces & ignoring serialization 
            // interfaces or IDisposable.
            while (!type.IsPublic)
            {
                type = type.BaseType;
            }

            var valueType = new CodeTypeReference(type);
            prop.Type = valueType;
            if (internalClass)
                prop.Attributes = MemberAttributes.Assembly;
            else
                prop.Attributes = MemberAttributes.Public;

            if (useStatic)
                prop.Attributes |= MemberAttributes.Static;

            // For Strings, emit this:
            //    return ResourceManager.GetString("name", _resCulture);
            // For Streams, emit this:
            //    return ResourceManager.GetStream("name", _resCulture);
            // For Objects, emit this:
            //    Object obj = ResourceManager.GetObject("name", _resCulture);
            //    return (MyValueType) obj;
            var resMgr = new CodePropertyReferenceExpression(null, "ResourceManager");
            var resCultureField = new CodeFieldReferenceExpression((useStatic) ? null : new CodeThisReferenceExpression(), CultureInfoFieldName);

            bool isString = type == typeof(String);
            bool isStream = type == typeof(UnmanagedMemoryStream) || type == typeof(MemoryStream);
            String getMethodName;
            String text;
            String valueAsString = TruncateAndFormatCommentStringForOutput(data.ValueAsString);
            String typeName = String.Empty;

            if (!isString) // Stream or Object
            {
                typeName = TruncateAndFormatCommentStringForOutput(type.ToString());
            }

            if (isString)
                getMethodName = "GetString";
            else if (isStream)
                getMethodName = "GetStream";
            else
                getMethodName = "GetObject";

            if (isString)
            {
                text = SR.GetString(SR.StringPropertyComment, valueAsString);
            }
            else
            { // Stream or Object
                if (valueAsString == null ||
                    String.Equals(typeName, valueAsString)) // If the type did not override ToString, ToString just returns the type name.
                    text = SR.GetString(SR.NonStringPropertyComment, typeName);
                else
                    text = SR.GetString(SR.NonStringPropertyDetailedComment, typeName, valueAsString);
            }

            prop.Comments.Add(new CodeCommentStatement(DocCommentSummaryStart, true));
            prop.Comments.Add(new CodeCommentStatement(text, true));
            prop.Comments.Add(new CodeCommentStatement(DocCommentSummaryEnd, true));

            var getValue = new CodeMethodInvokeExpression(resMgr, getMethodName, new CodePrimitiveExpression(resourceName), resCultureField);
            CodeMethodReturnStatement ret;
            if (isString || isStream)
            {
                ret = new CodeMethodReturnStatement(getValue);
            }
            else
            {
                var returnObj = new CodeVariableDeclarationStatement(typeof(Object), "obj", getValue);
                prop.GetStatements.Add(returnObj);

                ret = new CodeMethodReturnStatement(new CodeCastExpression(valueType, new CodeVariableReferenceExpression("obj")));
            }
            prop.GetStatements.Add(ret);

            srClass.Members.Add(prop);
            return true;
        }

        // Returns a valid identifier made from key, or null if it can't.
        internal static String VerifyResourceName(String key, CodeDomProvider provider)
        {
            return VerifyResourceName(key, provider, false);
        }

        // Once CodeDom provides a way to verify a namespace name, revisit this method.
        private static String VerifyResourceName(String key, CodeDomProvider provider, bool isNameSpace)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            foreach (char c in s_charsToReplace)
            {
                // For namespaces, allow . and ::
                if (!(isNameSpace && (c == '.' || c == ':')))
                    key = key.Replace(c, ReplacementChar);
            }

            if (provider.IsValidIdentifier(key))
                return key;

            // Now try fixing up keywords like "for".  
            key = provider.CreateValidIdentifier(key);
            if (provider.IsValidIdentifier(key))
                return key;

            // make one last ditch effort by prepending _.  This fixes keys that start with a number
            key = "_" + key;
            if (provider.IsValidIdentifier(key))
                return key;

            return null;
        }

        private static SortedList<string, ResourceData> VerifyResourceNames(
            Dictionary<String, ResourceData> resourceList,
            CodeDomProvider codeProvider,
            List<string> errors,
            out Dictionary<string, string> reverseFixupTable)
        {
            reverseFixupTable = new Dictionary<string, string>(0, StringComparer.InvariantCultureIgnoreCase);
            var cleanedResourceList =
                new SortedList<string, ResourceData>(StringComparer.InvariantCultureIgnoreCase)
                {
                    Capacity = resourceList.Count
                };

            foreach (KeyValuePair<String, ResourceData> entry in resourceList)
            {
                String key = entry.Key;

                // Disallow a property named ResourceManager or Culture - we add 
                // those.  (Any other properties we add also must be listed here)
                // Also disallow resource values of type Void.
                if (String.Equals(key, ResMgrPropertyName) ||
                    String.Equals(key, CultureInfoPropertyName) ||
                    typeof(void) == entry.Value.Type)
                {
                    errors.Add(key);
                    continue;
                }

                // Ignore WinForms design time and hierarchy information.
                // Skip resources starting with $ or >>, like "$this.Text",
                // ">>$this.Name" or ">>treeView1.Parent".
                if ((key.Length > 0 && key[0] == '$') ||
                    (key.Length > 1 && key[0] == '>' && key[1] == '>'))
                {
                    continue;
                }


                if (!codeProvider.IsValidIdentifier(key))
                {
                    String newKey = VerifyResourceName(key, codeProvider, false);
                    if (newKey == null)
                    {
                        errors.Add(key);
                        continue;
                    }

                    // Now see if we've already mapped another key to the 
                    // same name.
                    if (reverseFixupTable.TryGetValue(newKey, out string oldDuplicateKey))
                    {
                        // We can't handle this key nor the previous one.
                        // Remove the old one.
                        if (!errors.Contains(oldDuplicateKey))
                        {
                            errors.Add(oldDuplicateKey);
                        }
                        cleanedResourceList.Remove(newKey);
                        errors.Add(key);
                        continue;
                    }
                    reverseFixupTable[newKey] = key;
                    key = newKey;
                }
                ResourceData value = entry.Value;
                if (!cleanedResourceList.ContainsKey(key))
                {
                    cleanedResourceList.Add(key, value);
                }
                else
                {
                    // There was a case-insensitive conflict between two keys.
                    // Or possibly one key was fixed up in a way that conflicts 
                    // with another key (ie, "A B" and "A_B").
                    if (reverseFixupTable.TryGetValue(key, out string fixedUp))
                    {
                        if (!errors.Contains(fixedUp))
                        {
                            errors.Add(fixedUp);
                        }
                        reverseFixupTable.Remove(key);
                    }
                    errors.Add(entry.Key);
                    cleanedResourceList.Remove(key);
                }
            }
            return cleanedResourceList;
        }
    }
}
