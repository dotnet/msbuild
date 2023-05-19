// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Framework
{

    public static class TranslatorExtensions
    {
        public static Exception ReadException(this ITranslator translator)
        {
            // read message

            // read inner exception

            // create new exception

            // populate it
        }

        public static void WriteException(this ITranslator translator, Exception exception)
        {
            Exception? exception = null;
            translator.Translate(ref exception);
            return exception!;
        }

    }

    internal class BuildTranslatedException : Exception
    {
        public MyException()
        {
        }

        public MyException(string message) : base(message)
        {
        }

        public MyException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public abstract class BuildExceptionBase : Exception
    {
        protected BuildExceptionBase()
        {
            Exception e;
            //e.TargetSite

        }

        protected BuildExceptionBase(string? message) : base(message)
        { }

        protected BuildExceptionBase(string? message, Exception? inner) : base(message, inner)
        { }

        protected virtual void InitializeCustomState(Dictionary<string, string>? customData)
        { /* This is it. Override for exceptions with custom state */ }

        protected virtual Dictionary<string, string>? FlushCustomState(Dictionary<string, string>? customData)
        {
             /* This is it. Override for exceptions with custom state */
             return null;
        }
    }

    public class BxM : BuildException2
    {
        public BxM(string message) : base(message)
        { }
    }

    public class MyException2 : Exception
    {
        public MyException2()
        {
        }

        public MyException2(string message) : base(message)
        { }

        public MyException2(string message, Exception inner) : base(message, inner)
        { }
    }


    public abstract class BuildException: Exception, ITranslatable
    {
        public const string UnknownTypeName = "Unknown";
        private string? _deserializedStackTrace;

        public virtual string TypeName => this.GetType().FullName ?? UnknownTypeName;

        protected virtual void TranslateCustomState(ITranslator translator)
        { /* This is it. Override for exceptions with custom state */ }

        protected virtual void DeepTranslate(ITranslator translator)
        {
            if(translator.Mode == TranslationDirection.WriteToStream)
            {
                string? stackTrace = this.StackTrace;
                translator.Translate(ref stackTrace);
                string? source = this.Source;
                translator.Translate(ref source);
                int hresult = this.HResult;
                translator.Translate(ref hresult);
                string? helpLink = this.HelpLink;
                translator.Translate(ref helpLink);

            }
            else
            {
                translator.Translate(ref _deserializedStackTrace);
                string? source = null;
                translator.Translate(ref source);
                this.Source = source;
                int hresult = 0;
                translator.Translate(ref hresult);
                this.HResult = hresult;
                string? helpLink = null;
                translator.Translate(ref helpLink);
                this.HelpLink = helpLink;
                string? message = null;
                translator.Translate(ref message);
                
            }
            
            translator.Translate(ref this.StackTrace);
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            this.DeepTranslate(translator);
        }

        public override string? StackTrace => _deserializedStackTrace ?? base.StackTrace;

        protected virtual IDictionary CustomData => new Dictionary<string, string>();

        public override IDictionary Data => CustomData;
    }

    public class MyException : Exception
    {
        public MyException()
        {
        }

        public MyException(string message) : base(message)
        {
        }

        public MyException(string message, Exception inner) : base(message, inner)
        {
        }

        public MyException(SerializationInfo si, StreamingContext c): base(si, c)
        {
            
        }
    }


    public class BuildAbortedException: BuildException
    {
        public BuildAbortedException()
        {
        }

        override void Translate(ITranslator translator)
        {

        }

         public BuildAbortedException(string? message) : base(message)
        {
        }

        public BuildAbortedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
