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
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Gigya.Microdot.Interfaces.Logging
{
    /// <summary>
    /// A deledate that will be executed to get the actual logging information, if it is needed.
    /// </summary>
    /// <param name="message">The message of the log. This message should be a fixed, without any variabel parts (i.e, avoid concatenation or string.Format)</param>
    /// <param name="encryptedTags">Optional. An anonymous object with properties, where each property is the key of the tag and each value's 
    /// .ToString() result is the tag value, which must be encrypted upon storage. You can also pass an IEnumerable&lt;KeyValuePair&lt;string, string&gt;&gt;
    /// or IEnumerable&lt;KeyValuePair&lt;string, object&gt;&gt;. Defaults to null, which means no tags will be recored.</param>
    /// <param name="unencryptedTags">An anonymous object with properties, where each property is the key of the tag and each value's 
    /// .ToString() result is the tag value, which doesn't have to be encrypted upon storage. You can also pass an IEnumerable&lt;KeyValuePair&lt;string, string&gt;&gt;
    /// or IEnumerable&lt;KeyValuePair&lt;string, object&gt;&gt;. Defaults to null, which means no tags will be recored.</param>
    /// <param name="exception">Optional An exception to be logged, if logging an error.  Defaults to null, which means no exeption will be recored.</param>
    /// <param name="includeStack">True to include the full stack trace of the point where the logging occures, otherwise false. This incures a performance overhead.</param>
    public delegate void LogDelegate(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null, bool includeStack = false);


	/// <summary>
	/// An abstraction for a log
	/// </summary>
	public interface ILog
	{
		/// <summary>
		/// Log a Debug message
		/// </summary>
		/// <example>
		/// <code>
		/// Log.Debug(o => o(string.Format("This is a DEBUG message1 {0}", DateTime.Now), new {a = "a", b = 9}, new {a = "a", b = new {b1 = 9}}));
		/// </code>
		/// </example>
		/// <param name="log">lambda expression of the logging metadata</param>
		/// <param name="file">internal</param>
		/// <param name="line">internal</param>
		/// <param name="method">Internal</param>
		void Debug(Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null);

		/// <summary>
		/// Log a Info message
		/// </summary>
		/// <example>
		/// <code>
		/// Log.Info(o => o(string.Format("This is a Info message1 {0}", DateTime.Now), new {a = "a", b = 9}, new {a = "a", b = new {b1 = 9}}));
		/// </code>
		/// </example>
		/// <param name="log">lambda expression of the logging metadata</param>
		/// <param name="file">internal</param>
		/// <param name="line">internal</param>
		/// <param name="method">Internal</param>
		void Info(Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null);

		/// <summary>
		/// Log a Warning message
		/// </summary>
		/// <example>
		/// <code>
		/// Log.Warn(o => o(string.Format("This is a Warning message1 {0}", DateTime.Now), new {a = "a", b = 9}, new {a = "a", b = new {b1 = 9}}));
		/// </code>
		/// </example>
		/// <param name="log">lambda expression of the logging metadata</param>
		/// <param name="file">internal</param>
		/// <param name="line">internal</param>
		/// <param name="method">Internal</param>
		void Warn(Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null);

        /// <summary>
        /// Log a Warn message
        /// </summary>
        /// <example>
        /// <code>
        /// Log.Warn(string.Format("This is a Warning message1 {0}", DateTime.Now), new {a = "a", b = 9}, new {a = "a", b = new {b1 = 9}});
        /// </code>
        /// </example>
        /// <param name="message">The message of the log. This message should be a fixed, without any variabel parts (i.e, avoid concatenation or string.Format)</param>
        /// <param name="encryptedTags">Optional. An anonymous object with properties, where each property is the key of the tag and each value's 
        /// .ToString() result is the tag value, which must be encrypted upon storage. Defaults to null, which means no tags will be recored.</param>
        /// <param name="unencryptedTags">An anonymous object with properties, where each property is the key of the tag and each value's 
        /// .ToString() result is the tag value, which doesn't have to be encrypted upon storage. Defaults to null, which means no tags will be recored.</param>
        /// <param name="exception">Optional An exception to be logged, if logging an error.  Defaults to null, which means no exeption will be recored.</param>
        /// <param name="includeStack">True to include the full stack trace of the point where the logging occures, otherwise false. This incures a performance overhead.</param>
        /// <param name="file">internal</param>
        /// <param name="line">internal</param>
        /// <param name="method">Internal</param>
        void Warn(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null, bool includeStack = false, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null);

        /// <summary>
        /// Log a Error message
        /// </summary>
        /// <example>
        /// <code>
        /// Log.Error(o => o(string.Format("This is a Error message1 {0}", DateTime.Now), new {a = "a", b = 9}, new {a = "a", b = new {b1 = 9}}));
        /// </code>
        /// </example>
        /// <param name="log">lambda expression of the logging metadata</param>
        /// <param name="file">internal</param>
        /// <param name="line">internal</param>
        /// <param name="method">Internal</param>
        void Error(Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null);

        /// <summary>
        /// Log a Error message
        /// </summary>
        /// <example>
        /// <code>
        /// Log.Error(string.Format("This is a Critical message1 {0}", DateTime.Now), new {a = "a", b = 9}, new {a = "a", b = new {b1 = 9}});
        /// </code>
        /// </example>
        /// <param name="message">The message of the log. This message should be a fixed, without any variabel parts (i.e, avoid concatenation or string.Format)</param>
        /// <param name="encryptedTags">Optional. An anonymous object with properties, where each property is the key of the tag and each value's 
        /// .ToString() result is the tag value, which must be encrypted upon storage. Defaults to null, which means no tags will be recored.</param>
        /// <param name="unencryptedTags">An anonymous object with properties, where each property is the key of the tag and each value's 
        /// .ToString() result is the tag value, which doesn't have to be encrypted upon storage. Defaults to null, which means no tags will be recored.</param>
        /// <param name="exception">Optional An exception to be logged, if logging an error.  Defaults to null, which means no exeption will be recored.</param>
        /// <param name="includeStack">True to include the full stack trace of the point where the logging occures, otherwise false. This incures a performance overhead.</param>
        /// <param name="file">internal</param>
        /// <param name="line">internal</param>
        /// <param name="method">Internal</param>
        void Error(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null, bool includeStack = false, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null);

        /// <summary>
        /// Log a Critical message
        /// </summary>
        /// <example>
        /// <code>
        /// Log.Critical(o => o(string.Format("This is a Critical message1 {0}", DateTime.Now)));
        /// </code>
        /// </example>
        /// <param name="log">lambda expression of the logging metadata</param>
        /// <param name="file">internal</param>
        /// <param name="line">internal</param>
        /// <param name="method">Internal</param>
        void Critical(Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null);


        /// <summary>
        /// Log a Critical message
        /// </summary>
        /// <example>
        /// <code>
        /// Log.Critical(string.Format("This is a Critical message1 {0}", DateTime.Now));
        /// </code>
        /// </example>
        /// <param name="message">The message of the log. This message should be a fixed, without any variabel parts (i.e, avoid concatenation or string.Format)</param>
        /// <param name="encryptedTags">Optional. An anonymous object with properties, where each property is the key of the tag and each value's 
        /// .ToString() result is the tag value, which must be encrypted upon storage. Defaults to null, which means no tags will be recored.</param>
        /// <param name="unencryptedTags">An anonymous object with properties, where each property is the key of the tag and each value's 
        /// .ToString() result is the tag value, which doesn't have to be encrypted upon storage. Defaults to null, which means no tags will be recored.</param>
        /// <param name="exception">Optional An exception to be logged, if logging an error.  Defaults to null, which means no exeption will be recored.</param>
        /// <param name="includeStack">True to include the full stack trace of the point where the logging occures, otherwise false. This incures a performance overhead.</param>
        /// <param name="file">internal</param>
        /// <param name="line">internal</param>
        /// <param name="method">Internal</param>
        void Critical(string message, object encryptedTags = null, object unencryptedTags = null, Exception exception = null, bool includeStack = false, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null);
       
        /// <summary>
        /// Log a message and supply its logging level
        /// </summary>
        /// <example>
        /// <code>
        /// Log.Write(TraceEventType.Error, o => o(string.Format("This is a Error message1 {0}", DateTime.Now), new {a = "a", b = 9}, new {a = "a", b = new {b1 = 9}}));
        /// </code>
        /// </example>
        /// <param name="level">The severity level of the log message</param>
        /// <param name="log">lambda expression of the logging metadata</param>
        /// <param name="file">internal</param>
        /// <param name="line">internal</param>
        /// <param name="method">Internal</param>
        void Write(TraceEventType level, Action<LogDelegate> log, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string method = null);
	}
}