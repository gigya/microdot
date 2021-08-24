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
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.SharedLogic.Configurations.Serialization;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.SharedLogic.Security;
using Newtonsoft.Json;

namespace Gigya.Microdot.Hosting.HttpService.Endpoints
{
    public class SchemaEndpoint : ICustomEndpoint
    {
        private readonly string _jsonSchema;
        
        public SchemaEndpoint(ServiceSchema schemaProvider, IServiceSchemaPostProcessor serviceSchemaPostProcessor)
        {
            _jsonSchema = GenerateJsonSchema(schemaProvider, serviceSchemaPostProcessor);
        }

        private string GenerateJsonSchema(ServiceSchema schemaProvider,
            IServiceSchemaPostProcessor serviceSchemaPostProcessor)
        {
            serviceSchemaPostProcessor.PostProcessServiceSchema(schemaProvider);

            var jsonSchema = JsonConvert.SerializeObject(schemaProvider,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented,
                    DateParseHandling = DateParseHandling.None,
                });
            return jsonSchema;
        }

        public async Task<bool> TryHandle(HttpListenerContext context, WriteResponseDelegate writeResponse)
        {
            if (context.Request.Url.AbsolutePath.EndsWith("/schema"))
            {
                await writeResponse(_jsonSchema).ConfigureAwait(false);
                return true;
            }

            return false;
        }
    }
}
