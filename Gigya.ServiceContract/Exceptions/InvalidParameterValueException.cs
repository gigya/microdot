using System;
using System.Runtime.Serialization;
using Gigya.Common.Contracts.Exceptions;
using Newtonsoft.Json;

namespace Gigya.ServiceContract.Exceptions
{
    /// <summary>
    /// This excpetion is thrown if a parameter contains an invalid value
    /// </summary>
    [Serializable]
    public class InvalidParameterValueException: RequestException
    {
        ///<summary>ErrorCode of Invalid_parameter_value</summary>
        public override int? ErrorCode => 400006;

        /// <summary>
        /// Name of the parameter which has an invalid value
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// Path to the parameter (e.g. on Json object)
        /// </summary>
        public string ParameterPath { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidParameterValueException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="encrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which must be encrypted when stored.</param>
        /// <param name="unencrypted">Optional. A collection of type <see cref="Tags"/> that contains additional data about the exception, which needn't be encrypted when stored.</param>
        /// <param name="innerException">Optional. The exception that is the cause of the current exception.</param>
        public InvalidParameterValueException(string parameterName, string parameterPath, string message, Exception innerException = null, Tags encrypted = null, Tags unencrypted = null) : base(message, innerException, encrypted, unencrypted)
        {
            ParameterName = parameterName;
            ParameterPath = parameterPath;
        }

        /// <summary>Initializes a new instance of the <see cref="InvalidParameterValueException"/> class with serialized data.</summary>
        /// <param name="info"> The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
        /// <param name="context">The <see cref="StreamingContext"/> that contains  contextual information about the source or destination.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="info"/> parameter is null.</exception>
        /// <exception cref="SerializationException">The class name is null or <see cref="Exception.HResult"/> is zero (0). </exception>
        public InvalidParameterValueException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
