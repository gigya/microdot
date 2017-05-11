using System;
using System.Runtime.Serialization;

namespace Gigya.Common.Contracts.Exceptions
{
	/// <summary>
	/// This exception denotes a problem in an incoming request. For example, a missing required parameter, bad
	/// format, no permissions, trying to act on an entity that was deleted (e.g. site, user), etc. It should NOT be thrown
	/// when the request is valid but we fail to process it due to some internal error; <see cref="EnvironmentException"/>
	/// or <see cref="ProgrammaticException"/> should be thrown instead. Exceptions of this type are the same kind of errors
	/// that cause web servers to return HTTP 4xx errors. Note that clients to external systems should catch exceptions of
	/// this type and re-throw them as <see cref="ProgrammaticException"/>.
	/// </summary>
	[Serializable]
	public class RequestException : SerializableException
	{
		/// <summary>
		/// Represents custom error code for easily distinguish between different types of request exceptions.
		/// </summary>
		public virtual int? ErrorCode { get; private set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="RequestException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="encrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which must be encrypted when stored.</param>
        /// <param name="unencrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which needn't be encrypted when stored.</param>
        /// <param name="innerException">Optional. The exception that is the cause of the current exception.</param>
        public RequestException(string message, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null): this(message, null, innerException, encrypted, unencrypted)
        {
        }

        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="errorCode">Represents custom error code for easily distinguish between different types of request exceptions.</param>
        /// <param name="innerException">Optional. The exception that is the cause of the current exception.</param>
        /// <param name="encrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which must be encrypted when stored.</param>
        /// <param name="unencrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which needn't be encrypted when stored.</param>
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        public RequestException(string message, int? errorCode, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null)
			: base(message, innerException, encrypted, unencrypted)
		{
			ErrorCode = errorCode;
		}

		/// <summary>Initializes a new instance of the <see cref="RequestException"/> class with serialized data.</summary>
		/// <param name="info"> The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
		/// <param name="context">The <see cref="StreamingContext"/> that contains  contextual information about the source or destination.</param>
		/// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is null.</exception>
		/// <exception cref="SerializationException">The class name is null or <see cref="Exception.HResult"/> is zero (0). </exception>
		protected RequestException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}



    [Serializable]
    public class SecureRequestException : RequestException
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="SecureRequestException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		/// <param name="encrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which must be encrypted when stored.</param>
		/// <param name="unencrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which needn't be encrypted when stored.</param>
		public SecureRequestException(string message, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null) : base(message, innerException, encrypted: encrypted, unencrypted: unencrypted) { }

        /// <summary>Initializes a new instance of the <see cref="SecureRequestException"/> class with serialized data.</summary>
		/// <param name="info"> The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
		/// <param name="context">The <see cref="StreamingContext"/> that contains  contextual information about the source or destination.</param>
		/// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is null.</exception>
		/// <exception cref="SerializationException">The class name is null or <see cref="Exception.HResult"/> is zero (0). </exception>
        protected SecureRequestException(SerializationInfo info, StreamingContext context) : base(info, context) { }

    }
}
