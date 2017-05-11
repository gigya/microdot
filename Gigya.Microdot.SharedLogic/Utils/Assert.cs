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
