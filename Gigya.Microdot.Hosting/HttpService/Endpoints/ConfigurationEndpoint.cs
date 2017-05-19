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

using System.Net;
using System.Threading.Tasks;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    public class ConfigurationEndpoint : ICustomEndpoint
    {
        private readonly ConfigurationResponseBuilder _responseBuilder;


        public ConfigurationEndpoint(ConfigurationResponseBuilder responseBuilder)
        {
            _responseBuilder = responseBuilder;
        }


        public async Task<bool> TryHandle(HttpListenerContext context, WriteResponseDelegate writeResponse)
        {
            if (context.Request.Url.AbsolutePath == "/config")
            {
                var format = context.Request.QueryString["format"] ?? "text";
                switch (format)
                {
                    case "json":
                        var json = _responseBuilder.BuildJson();
                        await writeResponse(json);
                        break;
                    default:
                        var text = _responseBuilder.BuildText();
                        await writeResponse(text, contentType: "text/plain");
                        break;
                }

                return true;
            }

            return false;
        }
    }
}
