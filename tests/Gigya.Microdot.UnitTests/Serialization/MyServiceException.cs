//using System;
//using System.Runtime.Serialization;

//using Gigya.Common.Contracts.Exceptions;

//namespace Gigya.Microdot.UnitTests.Serialization
//{
   //todo: toli remove file
//    [Serializable]
//	public class MyServiceException : RequestException
//	{
//		public IBusinessEntity Entity { get; private set; }

//		public MyServiceException(string message, IBusinessEntity entity, Tags encrypted = null, Tags unencrypted = null)
//			: base(message, null, encrypted, unencrypted)
//		{
//			Entity = entity;
//		}

//		public MyServiceException(string message, Exception innerException) : base(message, innerException) { }
//		public MyServiceException(SerializationInfo info, StreamingContext context) : base(info, context) { }
//	}
//}