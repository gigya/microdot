using System;

namespace Gigya.Common.Contracts.HttpService
{
	[AttributeUsage(AttributeTargets.Interface)]
	public class HttpServiceAttribute : Attribute
	{
        /// <summary>
        /// This is the port number that the service will listen to for incoming HTTP requests. Other ports (used for
        /// Orleans, Metrics.Net, etc) are opened at sequencial numbers from this base offset. 
        /// </summary>
		public int BasePort { get; private set; }

        public bool UseHttps { get; set; }

		public HttpServiceAttribute(int basePort)
		{
			BasePort = basePort;
		}

        public string Name { get; set; }
	}
}
