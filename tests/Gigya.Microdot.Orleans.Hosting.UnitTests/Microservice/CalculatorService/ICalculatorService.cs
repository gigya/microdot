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
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Attributes;
using Gigya.Common.Contracts.HttpService;
using Gigya.ServiceContract.Attributes;
using Gigya.ServiceContract.HttpService;
using Newtonsoft.Json.Linq;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService
{
    [HttpService(6555)]
    public interface ICalculatorService
    {
        Task<int> Add(int a, int b, bool shouldThrow = false);

        [PublicEndpoint("test.calculator.getAppDomainChain", RequireHTTPS = false, PropertyNameForResponseBody = "something")]
        Task<string[]> GetAppDomainChain(int depth);
        Task<Tuple<DateTime, DateTimeOffset>> ToUniversalTime(DateTime localDateTime, DateTimeOffset localDateTimeOffset);

        Task<Tuple<int, string, JObject>> AddWithOptions(JObject jObject, int optional1 = 5, string optional2 = "test", JObject optional3 = null);

        Task<JObject> Add(JObject jObject);

        Task<JObjectWrapper> Add(JObjectWrapper jObjectW);

        Task Do();

        Task<Wrapper> DoComplex(Wrapper wrapper);

        Task<int> DoInt(int a);

        [Cached] Task<int> GetNextNum();
        [Cached] Task<Revocable<int>> GetVersion(string id);
        Task LogData(string message);

        Task LogPram([Sensitive] string sensitive, [NonSensitive] string notSensitive, [Sensitive(Secretive = true)]string notExists, string @default);

        [NonSensitive]
        Task LogPram2([Sensitive] string sensitive, [NonSensitive] string notSensitive, [Sensitive(Secretive = true)]string notExists, string @default);

        Task<bool> IsLogParamSucceeded(List<string> sensitives, List<string> NoneSensitives, List<string> NotExists);
        Task CreatePerson([LogFields] CalculatorServiceTests.Person person);
        Task LogGrainId();
        Task<bool> ValidatePersonLogFields([LogFields] CalculatorServiceTests.Person person);
        Task RegexTestWithDefaultTimeoutDefault(int defaultTimeoutInSeconds);
    }
}