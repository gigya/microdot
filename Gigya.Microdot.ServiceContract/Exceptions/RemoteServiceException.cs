using System;
using System.Runtime.Serialization;

namespace Gigya.Microdot.ServiceContract.Exceptions
{
	[Serializable]
	public class RemoteServiceException : EnvironmentException
	{
		public string RequestedUri { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="RemoteServiceException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
		/// </summary>
		/// <param name="message">The error message that explains the reason for the exception.</param>
		/// <param name="requestedUri">The URI requested when the remote service call failed.</param>
		/// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
		/// <param name="encrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which must be encrypted when stored.</param>
		/// <param name="unencrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which needn't be encrypted when stored.</param>
		/// <param name="details">Optional. Additional details about the exception.</param>
		public RemoteServiceException(string message, string requestedUri, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null, string details = null)
			: base(message, innerException, encrypted, unencrypted)
		{
			RequestedUri = requestedUri;
		}

		/// <summary>Initializes a new instance of the <see cref="RemoteServiceException"/> class with serialized data.</summary>
		/// <param name="info"> The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
		/// <param name="context">The <see cref="StreamingContext"/> that contains  contextual information about the source or destination.</param>
		/// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is null.</exception>
		/// <exception cref="SerializationException">The class name is null or <see cref="Exception.HResult"/> is zero (0). </exception>
		// ReSharper disable once UnusedMember.Global
		protected RemoteServiceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
	}
}