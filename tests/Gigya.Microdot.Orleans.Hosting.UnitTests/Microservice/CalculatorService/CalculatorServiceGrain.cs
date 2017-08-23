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
using System.Threading.Tasks;
using Gigya.ServiceContract.HttpService;
using Newtonsoft.Json.Linq;
using Orleans;
using Orleans.Concurrency;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService
{

    [StatelessWorker, Reentrant]
    public class CalculatorServiceGrain : Grain, ICalculatorServiceGrain
    {
        private ICalculatorWorkerGrain Worker { get; set; }


        public override Task OnActivateAsync()
        {
            Worker = GrainFactory.GetGrain<ICalculatorWorkerGrain>(new Random(Guid.NewGuid().GetHashCode()).Next());
            return base.OnActivateAsync();
        }


        public Task<int> Add(int a, int b, bool shouldThrow = false) { return Worker.Add(a, b, shouldThrow); }

        public Task<string[]> GetAppDomainChain(int depth) { return Worker.GetAppDomainChain(depth); }


        public Task<Tuple<DateTime, DateTimeOffset>> ToUniversalTime(DateTime localDateTime, DateTimeOffset localDateTimeOffset)
        {
            return Worker.ToUniversalTime(localDateTime, localDateTimeOffset);
        }

        public async Task<string> AddWithOptions(JObject jObject, int optional1 = 5, string optional2 = "", JObject optional3 = null)
        {
            return optional1 + "|" + optional2 + "|" + optional3 ?? "NULL";
        }


        public Task<JObject> Add(JObject jObject) { return Worker.Add(jObject); }


        public Task<JObjectWrapper> Add(JObjectWrapper jObjectW) { return Worker.Add(jObjectW); }


        public Task Do() { return Worker.Do(); }


        public Task<Wrapper> DoComplex(Wrapper wrapper) { return Worker.DoComplex(wrapper); }


        public Task<int> DoInt(int a) { return Worker.DoInt(a); }

        public Task<int> GetNextNum() { return Worker.GetNextNum(); }

        public Task<Revocable<int>> GetVersion(string id){return Worker.GetVersion(id);}
    }

}