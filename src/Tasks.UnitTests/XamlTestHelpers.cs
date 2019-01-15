// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CSharp;
using Microsoft.Build.Tasks.Xaml;

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xaml;
using Microsoft.Build.Evaluation;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    internal static class XamlTestHelpers
    {
        // the following are used for the tests in XamlTaskFactory_Tests.cs and XamlDataDrivenToolTask_Tests.cs
        // make as robust as possible
        private const string fakeXml = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                            <Rule Name=`FakeTask`>
                                              <BoolProperty Name=`Always` Switch=`/always` />

                                              <!-- Basic types -->
                                              <BoolProperty Name=`BasicReversible` Switch=`/Br` ReverseSwitch=`/BrF` />
                                              <BoolProperty Name=`BasicNonreversible` Switch=`/Bn` />
                                              <EnumProperty Name=`BasicString`>
                                                <EnumValue Name=`Enum1` Switch=`/Bs1` />
                                                <EnumValue Name=`Enum2` Switch=`/Bs2` />
                                              </EnumProperty>
                                              <StringListProperty Name=`BasicStringArray` Switch=`/Bsa` />
                                              <IntProperty Name=`BasicInteger` Switch=`/Bi` />
                                              <StringProperty Name=`BasicFileWSwitch` Switch=`/Bfws` />
                                              <StringProperty Name=`BasicFileWOSwitch` />
                                              <StringProperty Name=`BasicDirectory` />
                                              <DynamicEnumProperty Name=`BasicDynamicEnum` />
                                              
                                              <!-- More Complex types -->
                                              <BoolProperty Name=`ComplexReversible` Switch=`/Cr:CT` ReverseSwitch=`/Cr:CF` Separator=`:` />
                                              <BoolProperty Name=`ComplexNonreversibleWArgument` Switch=`/Cnrwa`>
                                                <Argument Property=`ComplexFile` IsRequired=`true` />
                                              </BoolProperty>
                                              <EnumProperty Name=`ComplexString` IsRequired=`true`>
                                                <EnumValue Name=`LegalValue1` Switch=`/Lv1` />
                                                <EnumValue Name=`LegalValue2` Switch=`/Lv2` />
                                              </EnumProperty>

                                              <StringListProperty Name=`ComplexStringArray` Switch=`/Csa` Separator=`;` />
                                              <StringProperty Name=`ComplexFileNoDefault` />
                                              <IntProperty Name=`ComplexInteger` Switch=`/Ci` MinValue=`64` MaxValue=`255` />

                                              <!-- Dependencies, fallbacks, and so on -->
                                              <BoolProperty Name=`OtherNonreversible` Switch=`/Onr`> 
                                                <Argument IsRequired=`true` Property=`ComplexFileNoDefault` />
                                              </BoolProperty>
                                              <StringProperty Name=`ComplexDirectory` />
                                              <StringListProperty Name=`OutputFile` Switch=`/Of` Separator=`;` />
                                              <StringListProperty Name=`InputFile` />
                                            </Rule>
                                         </ProjectSchemaDefinitions>
                                            ";

        public const string QuotingQuotesXml = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                            <Rule Name=`FakeTask`>
                                              <!-- Quoted: If the quote mechanism isn't working, all the tests will fail with exceptions related to compiling. -->
                                              <EnumProperty Name=`Quoted`>
                                                <EnumValue Name=`Value1` Switch=`/foo: &quot;bar&quot;` />
                                              </EnumProperty>

                                            </Rule>
                                         </ProjectSchemaDefinitions>
                                            ";

        public const string QuotingBackslashXml = @"<ProjectSchemaDefinitions xmlns=`clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework` xmlns:x=`http://schemas.microsoft.com/winfx/2006/xaml` xmlns:sys=`clr-namespace:System;assembly=mscorlib` xmlns:impl=`clr-namespace:Microsoft.VisualStudio.Project.Contracts.Implementation;assembly=Microsoft.VisualStudio.Project.Contracts.Implementation`>
                                            <Rule Name=`FakeTask`>
                                              <!-- Quoted: If the quote mechanism isn't working, all the tests will fail with exceptions related to compiling. -->
                                              <EnumProperty Name=`Quoted`>
                                                <EnumValue Name=`Value2` Switch=`/foo: a\$bar` />
                                              </EnumProperty>

                                            </Rule>
                                         </ProjectSchemaDefinitions>
                                            ";

        private static string s_pathToMSBuildBinaries = null;

        /// <summary>
        /// Returns the path to the MSBuild binaries 
        /// </summary>
        public static string PathToMSBuildBinaries
        {
            get
            {
                if (s_pathToMSBuildBinaries == null)
                {
                    Toolset currentToolset = ProjectCollection.GlobalProjectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);

                    Assert.NotNull(currentToolset); // String.Format("For some reason, we couldn't get the current ({0}) toolset!", ObjectModelHelpers.MSBuildDefaultToolsVersion)
                    s_pathToMSBuildBinaries = currentToolset.ToolsPath;
                }

                return s_pathToMSBuildBinaries;
            }
        }

        public static Assembly SetupGeneratedCode()
        {
            return SetupGeneratedCode(fakeXml);
        }

        public static Assembly SetupGeneratedCode(string xml)
        {
            TaskParser tp = null;
            try
            {
                tp = LoadAndParse(xml, "FakeTask");
            }
            catch (XamlParseException)
            {
                Assert.True(false, "Parse of FakeTask XML failed");
            }

            TaskGenerator tg = new TaskGenerator(tp);
            CodeCompileUnit compileUnit = tg.GenerateCode();
            CodeDomProvider codeGenerator = CodeDomProvider.CreateProvider("CSharp");

            using (StringWriter sw = new StringWriter(CultureInfo.CurrentCulture))
            {
                CodeGeneratorOptions options = new CodeGeneratorOptions();
                options.BlankLinesBetweenMembers = true;
                options.BracingStyle = "C";

                codeGenerator.GenerateCodeFromCompileUnit(compileUnit, sw, options);
                CSharpCodeProvider provider = new CSharpCodeProvider();
                // Build the parameters for source compilation.
                CompilerParameters cp = new CompilerParameters();

                // Add an assembly reference.
                cp.ReferencedAssemblies.Add("System.dll");
                cp.ReferencedAssemblies.Add("System.Data.dll");
                cp.ReferencedAssemblies.Add("System.Xml.dll");
                cp.ReferencedAssemblies.Add(Path.Combine(PathToMSBuildBinaries, "Microsoft.Build.Framework.dll"));
                cp.ReferencedAssemblies.Add(Path.Combine(PathToMSBuildBinaries, "Microsoft.Build.Utilities.Core.dll"));
                cp.ReferencedAssemblies.Add(Path.Combine(PathToMSBuildBinaries, "Microsoft.Build.Tasks.Core.dll"));

                // Generate an executable instead of 
                // a class library.
                cp.GenerateExecutable = false;
                // Set the assembly file name to generate.
                cp.GenerateInMemory = true;
                // Invoke compilation
                CompilerResults cr = provider.CompileAssemblyFromSource(cp, sw.ToString());

                foreach (CompilerError error in cr.Errors)
                {
                    Console.WriteLine(error.ToString());
                }
                if (cr.Errors.Count > 0)
                {
                    Console.WriteLine(sw.ToString());
                }
                Assert.Empty(cr.Errors);
                if (cr.Errors.Count > 0)
                {
                    foreach (CompilerError error in cr.Errors)
                    {
                        Console.WriteLine(error.ErrorText);
                    }
                }
                return cr.CompiledAssembly;
            }
        }

        /// <summary>
        /// used for testing. Will load snippets of xml into the task generator
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static TaskParser LoadAndParse(string s, string desiredRule)
        {
            TaskParser tp = new TaskParser();
            tp.Parse(s.Replace("`", "\""), desiredRule);
            return tp;
        }

        /// <summary>
        /// This method is a method to set any property in a task to a certain value
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="parameters"></param>
        public static void SetProperty(object instance, string propertyName, params object[] parameters)
        {
            try
            {
                instance.GetType().InvokeMember(propertyName, BindingFlags.SetProperty, null, instance, parameters, CultureInfo.CurrentCulture);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        /// <summary>
        /// This method returns the certain attribute for a property (the value it is set to)
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="parameters"></param>
        public static object GetProperty(object instance, string propertyName, params object[] parameters)
        {
            try
            {
                return instance.GetType().InvokeMember(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty, null, instance, parameters, CultureInfo.CurrentCulture);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        /// <summary>
        /// This method gets called to call the GenerateResponseFileCommands method
        /// </summary>
        /// <param name="taskObject"></param>
        /// <returns></returns>
        public static string GenerateCommandLine(object task)
        {
            try
            {
                return (string)task.GetType().InvokeMember("GetCommandLine_ForUnitTestsOnly", BindingFlags.Public | BindingFlags.NonPublic |
                                    BindingFlags.Instance | BindingFlags.InvokeMethod, null, task, new object[] { });
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }
    }
}
