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

namespace Gigya.Common.Contracts.HttpService
{
	[AttributeUsage(AttributeTargets.Interface)]
	public class HttpServiceAttribute : Attribute
	{
        /// <summary>
        /// This is the port number that the service will listen to for incoming HTTP requests. Other ports (used for
        /// Orleans, Metrics.Net, etc) are opened at sequential numbers from this base offset. 
        /// </summary>
		public int BasePort { get; set; }

        public bool UseHttps { get; set; }

		public HttpServiceAttribute(int basePort)
		{
			BasePort = basePort;
		}

        [Obsolete("This propery is no longer in use, and will be removed on Microdot version 2.0. Service name is now extracted from its interface's namespace.")]
	    public string Name { get; set; }
    }
}
