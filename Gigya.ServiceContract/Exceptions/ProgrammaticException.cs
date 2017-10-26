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
using System.Runtime.Serialization;

namespace Gigya.Common.Contracts.Exceptions
{
	/// <summary>
	/// This exception denotes a programmatic error, i.e. a bug we detected in our code at run time. For example,
	/// you can throw this when your method was called with a null pointer, or if someone attempts to use a static class
	/// you wrote before initializing it. Throw this when you detect you're in an invalid state (e.g. using assertions).
	/// Exceptions of this type are logged and (hopefully) routed to developers. <see cref="EnvironmentException"/>s, on
	/// the other hand, are routed to IT/Operations.
	/// </summary>
	[Serializable]
	public class ProgrammaticException : SerializableException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ProgrammaticException"/> class with a specified error message
		/// and a reference to the inner exception that is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">Optional. The exception that is the cause of the current exception.</param>
		/// <param name="encrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data
		/// about the exception, which must be encrypted when stored.</param>
		/// <param name="unencrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data
		/// about the exception, which needn't be encrypted when stored.</param>
		public ProgrammaticException(string message, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null) 
            : base(message, innerException, encrypted, unencrypted) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="ProgrammaticException"/> class with serialized data.
		/// </summary>
		/// <param name="info"> The <see cref="SerializationInfo"/> that holds the serialized object data about the
		/// exception being thrown.</param>
		/// <param name="context">The <see cref="StreamingContext"/> that contains  contextual information about the
		/// source or destination.</param>
		/// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is null.</exception>
		/// <exception cref="SerializationException">The class name is null or <see cref="Exception.HResult"/> is zero
		/// (0). </exception>
		protected ProgrammaticException(SerializationInfo info, StreamingContext context) 
            : base(info, context) { }
	}

    /// <summary>
    /// This exception is thrown by services when they encounter any unhandled exception that doesn't derive from
    /// <see cref="SerializableException"/> (e.g. <see cref="NullReferenceException"/>). It contains the original
    /// exception in its <see cref="Exception.InnerException"/> property. On the client-side, they are instead exposed
    /// as an RemoteServiceException, having an inner exception copied over. You should never throw this exception from
    /// your code.
    /// </summary>
    [Serializable, Obsolete("No longer used, preserved for backwards compatibility with older servers.")]
    public class UnhandledException : ProgrammaticException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnhandledException"/> class with the specified inner exception
        /// that is the cause of this exception.
        /// </summary>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        /// <param name="encrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data
        /// about the exception, which must be encrypted when stored.</param>
        /// <param name="unencrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data
        /// about the exception, which needn't be encrypted when stored.</param>
        public UnhandledException(Exception innerException, Tags encrypted = null, Tags unencrypted = null)
            : base("An unhandled exception occurred when processing this request. See InnerException for details.", innerException, encrypted, unencrypted) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProgrammaticException"/> class with serialized data.
        /// </summary>
        /// <param name="info"> The <see cref="SerializationInfo"/> that holds the serialized object data about the
        /// exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains  contextual information about the
        /// source or destination.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is null.</exception>
        /// <exception cref="SerializationException">The class name is null or <see cref="Exception.HResult"/> is zero
        /// (0). </exception>
        protected UnhandledException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
