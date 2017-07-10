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
using System.Linq;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.SharedLogic.Measurement;
using Gigya.ServiceContract.HttpService;
using Newtonsoft.Json.Linq;
using Orleans;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService
{
    public class CalculatorWorkerGrain : Grain, ICalculatorWorkerGrain
    {
      
        private ILog Log { get; set; }
        private int _counter;
        private int _counter2;

        public CalculatorWorkerGrain(ILog log)
        {
            Log = log;

        }


        public async Task<int> Add(int a, int b, bool shouldThrow)
        {
            using (RequestTimings.Current.DataSource.Hades.Delete.Measure())
                await Task.Delay(100);

            Log.Info(_ => _("Server: Adding {a} + {b}"));

            if (shouldThrow)
                throw new RequestException("You have request to throw an exception. Now catch!");

            return a + b;
        }


     

        public async Task<string[]> GetAppDomainChain(int depth)
        {
            var current = new[] { AppDomain.CurrentDomain.FriendlyName };

            if (depth == 1)
                return current;

            var chain = await GrainFactory.GetGrain<ICalculatorServiceGrain>(depth).GetAppDomainChain(depth - 1);

            return current.Concat(chain).ToArray();
        }


        public async Task<Tuple<DateTime, DateTimeOffset>> ToUniversalTime(DateTime localDateTime, DateTimeOffset localDateTimeOffset)
        {
            if (localDateTime.Kind != DateTimeKind.Local)
                throw new RequestException("localDateTime must be DateTimeKind.Local");

            if (localDateTimeOffset.Offset == TimeSpan.Zero)
                throw new RequestException("localDateTimeOffset must be in UTC offset");

            return Tuple.Create(localDateTime.ToUniversalTime(), localDateTimeOffset.ToUniversalTime());
        }


        public async Task<JObject> Add(JObject jObject)
        {
            jObject["c"] = jObject["a"].Value<int>() + jObject["b"].Value<int>();
            return jObject;
        }


        public async Task<JObjectWrapper> Add(JObjectWrapper jObjectW)
        {
            jObjectW.JObject["c"] = jObjectW.JObject["a"].Value<int>() + jObjectW.JObject["b"].Value<int>();
            return jObjectW;
        }


        public async Task Do() { }

        public async Task<Wrapper> DoComplex(Wrapper wrapper) { return wrapper; }

        public async Task<int> DoInt(int a) { return a; }

        public Task<int> GetNextNum()
        {
            _counter++;
            return Task.FromResult(_counter);
        }

        public Task<Revocable<int>> GetVersion(string Id)
        {
            _counter2++;
            return Task.FromResult(new Revocable<int> {Value = _counter2, RevokeKeys = new List<string> {Id}});
        }
    }
}