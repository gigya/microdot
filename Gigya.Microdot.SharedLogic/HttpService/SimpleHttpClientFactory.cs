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
using System.Net.Http;

namespace Gigya.Microdot.SharedLogic.HttpService
{
    class SimpleHttpClientFactory : IHttpClientFactory
    {
        private object SyncLock { get; set; }
        private TimeSpan? RequestTimeout { get; set; }
        private HttpClient Client { get; set; }

        public HttpClient GetClient(bool https, TimeSpan? requestTimeout)
        {
            if (https)
                throw new ArgumentException("SimpleHttpClientFactory is a simpleton and doesn't know enough math to do encryption.");

            lock (SyncLock)
            {
                if (Client != null)
                {
                    if (requestTimeout != RequestTimeout)
                        throw new ArgumentException("SimpleHttpClientFactory is a simpleton and is confused when his parameters are different from the first request.");

                    return Client;
                }

                Client = new HttpClient();
            
                if (requestTimeout != null)
                    Client.Timeout = requestTimeout.Value;

                return Client;
            }
        }
    }
}