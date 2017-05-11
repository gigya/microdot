using System;
using System.Runtime.Serialization;

namespace Gigya.Common.Contracts.Exceptions
{
	/// <summary>
	/// This exception denotes a problem with the environment, such as a database that's down, an external service
	/// we rely on that's inaccessible, a missing or invalid configuration file, or even going out of memory. Exceptions of
	/// this type are sometimes recoverable, e.g. by retrying the DB operation. They're the same kind of errors that cause
	/// web servers to return HTTP 5xx errors. Exceptions of this type are routed by default to IT/Operations -- it's up to
	/// them to fix the problem in production environment. <see cref="ProgrammaticException"/>s, on the other hand, are
	/// routed to developers.
	/// </summary>	
	[Serializable]
	public class EnvironmentException : SerializableException
	{
        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">Optional. The exception that is the cause of the current exception.</param>
        /// <param name="encrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which must be encrypted when stored.</param>
        /// <param name="unencrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which needn't be encrypted when stored.</param>
        public EnvironmentException(string message, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null) : base(message, innerException, encrypted, unencrypted) { }

		/// <summary>Initializes a new instance of the <see cref="EnvironmentException"/> class with serialized data.</summary>
		/// <param name="info"> The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
		/// <param name="context">The <see cref="StreamingContext"/> that contains  contextual information about the source or destination.</param>
		/// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is null.</exception>
		/// <exception cref="SerializationException">The class name is null or <see cref="Exception.HResult"/> is zero (0). </exception>
		protected EnvironmentException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}
