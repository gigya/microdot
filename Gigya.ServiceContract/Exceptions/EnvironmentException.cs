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
