#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.SharedLogic.Utils {

    /// <summary>A Gigya equivalent for Microsoft.VisualStudio.TestTools.UnitTesting.Assert, to prevent including that
    /// dependency, and to throw our own exception type. Assertion errors should be communicated to developers somehow in
    /// the future.</summary>
    public static class GAssert
    {
        [Serializable]
        public class AssertionException: ProgrammaticException
        {

            public AssertionException(string message, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null)
                : base(message, innerException, encrypted, unencrypted) { }


            public AssertionException(SerializationInfo info, StreamingContext context)
                : base(info, context) { }
        }


        public static void IsTrue(bool condition, string message = null, Exception exception = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = "", [CallerMemberName] string method = null) {
            if (condition == false)
            {
                LogCritical(message, null, line, file, method); 
                throw new AssertionException(message, exception);
            }
        }


        public static void IsTrue<T>(bool condition, string message = null, Exception exception = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = "", [CallerMemberName] string method = null)
            where T:Exception,new()
        {
            if (condition == false) {
                LogCritical(message, null, line, file, method);             
                throw new T();
            }
        }


        /// <summary>Asserts the condition is true, or logs an assertion error and throws the exception that's returned
        /// by your provided lambda method.</summary>
        /// <example>GAssert.IsTrue(false, () => new EnvironmentException("test"));</example>
        /// 
        public static void IsTrue<T>(bool condition, Func<T> makeException, [CallerLineNumber] int line = 0, [CallerFilePath] string file = "", [CallerMemberName] string method = null)
            where T:Exception
        {
            if (condition == false) {
                T exception = makeException();
                LogCritical(exception.RawMessage(), null, line, file, method);
                throw exception;
            }
        }


        public static T AssertNotNull<T>(this T obj, string message = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = "", [CallerMemberName] string method = null)
            where T:class
        {
            if (obj == null) {
                string details = "Object reference is null" + (message == null ? "" : ": " + message);
                LogCritical(details, null, line, file, method); 
                throw new AssertionException(message);
            }
            else return obj;
        }
    
        public static void Fail(string message = null, Exception exception = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = "", [CallerMemberName] string method = null) {
            LogCritical(message, exception, line, file, method);        
            throw new AssertionException(message, exception);
        }
    
        public static AssertionException LogAndMakeFailureException(string message = null, Exception exception = null, [CallerLineNumber] int line = 0, [CallerFilePath] string file = "", [CallerMemberName] string method = null) {
            LogCritical(message, exception, line, file, method);        
            return new AssertionException(message, exception);
        }

        private static void LogCritical(string details, Exception exception, int line, string file, string method)
        {
            // commented out till we get IOC right
            //Log.Critical(_ => _("Assertion error", exception: exception, includeStack: true,
			//	unencryptedTags: new { details, stack = Environment.StackTrace, method, file, line }));
        }
    }
}
