﻿using System;
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
    [Serializable]
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
