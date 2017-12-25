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
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Hosting.Events;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Orleans.Hosting.Events;
using Gigya.ServiceContract.HttpService;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Orleans;
using Orleans.Concurrency;
using Shouldly;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService
{

    [StatelessWorker, Reentrant]
    public class CalculatorServiceGrain : Grain, ICalculatorServiceGrain
    {
        private readonly ILog _log;
        private readonly IEventPublisher _eventPublisher;
        private ICalculatorWorkerGrain Worker { get; set; }

        public CalculatorServiceGrain(ILog log, IEventPublisher eventPublisher)
        {
            _log = log;
            _eventPublisher = eventPublisher;
        }


        public override Task OnActivateAsync()
        {
            Worker = GrainFactory.GetGrain<ICalculatorWorkerGrain>(new Random(Guid.NewGuid().GetHashCode()).Next());
            return base.OnActivateAsync();
        }


        public Task<int> Add(int a, int b, bool shouldThrow = false)
        {
            return Worker.Add(a, b, shouldThrow);
        }

        public Task<string[]> GetAppDomainChain(int depth)
        {
            return Worker.GetAppDomainChain(depth);
        }


        public Task<Tuple<DateTime, DateTimeOffset>> ToUniversalTime(DateTime localDateTime,
            DateTimeOffset localDateTimeOffset)
        {
            return Worker.ToUniversalTime(localDateTime, localDateTimeOffset);
        }

        public async Task<Tuple<int, string, JObject>> AddWithOptions(JObject jObject, int optional1 = 5,
            string optional2 = "test", JObject optional3 = null)
        {
            return Tuple.Create(optional1, optional2, optional3);
        }


        public Task<JObject> Add(JObject jObject)
        {
            return Worker.Add(jObject);
        }


        public Task<JObjectWrapper> Add(JObjectWrapper jObjectW)
        {
            return Worker.Add(jObjectW);
        }


        public Task Do()
        {
            return Worker.Do();
        }


        public Task<Wrapper> DoComplex(Wrapper wrapper)
        {
            return Worker.DoComplex(wrapper);
        }


        public Task<int> DoInt(int a)
        {
            return Worker.DoInt(a);
        }

        public Task<int> GetNextNum()
        {
            return Worker.GetNextNum();
        }

        public Task<Revocable<int>> GetVersion(string id)
        {
            return Worker.GetVersion(id);
        }


        public Task LogData(string message)
        {
            _log.Warn(x => x(message));
            return TaskDone.Done;
        }

        public Task LogPram(string sensitive, string notSensitive, string notExists, string @default)
        {
            return Task.FromResult(1);
        }

        public Task LogPram2(string sensitive, string notSensitive, string notExists, string @default)
        {
            return Task.FromResult(1);
        }

        public async Task<bool> IsLogPramSucceed(List<string> sensitive, List<string> NoneSensitive,
            List<string> NotExists)
        {
            await Task.Delay(150);
            try
            {
                var eventPublisher = _eventPublisher as SpyEventPublisher;

                var serviceCallEvent = eventPublisher.Events.OfType<ServiceCallEvent>().Last();
                foreach (var s in sensitive)
                {
                    serviceCallEvent.EncryptedServiceMethodArguments.ShouldContain(x1 => x1.Value == s);
                    serviceCallEvent.UnencryptedServiceMethodArguments.ShouldNotContain(x1 => x1.Value == s);

                }

                foreach (var s in NoneSensitive)
                {
                    serviceCallEvent.UnencryptedServiceMethodArguments.ShouldContain(x1 => x1.Value == s);
                    serviceCallEvent.EncryptedServiceMethodArguments.ShouldNotContain(x1 => x1.Value == s);

                }

                foreach (var n in NotExists)
                {
                    serviceCallEvent.UnencryptedServiceMethodArguments.ShouldNotContain(x1 => x1.Value == n);
                    serviceCallEvent.EncryptedServiceMethodArguments.ShouldNotContain(x1 => x1.Value == n);
                }

            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }



        public async Task LogGrainId()
        {

            var eventPublisher = _eventPublisher as SpyEventPublisher;
            await GrainFactory.GetGrain<ICalculatorServiceGrain>(0).Do();

            await Task.Delay(150);

            var serviceCallEvent = eventPublisher.Events.OfType<GrainCallEvent>().Last();
            serviceCallEvent.GrainKeyExtention.ShouldBeNull();
            serviceCallEvent.GrainKeyLong.ShouldBe(0);
            serviceCallEvent.GrainKeyGuid.ShouldBeNull();
            serviceCallEvent.GrainKeyString.ShouldBeNull();

            var id = await GrainFactory.GetGrain<IUserGrainWithGuid>(Guid.NewGuid()).GetIdentety();
            await Task.Delay(150);

             serviceCallEvent = eventPublisher.Events.OfType<GrainCallEvent>().Last();
            serviceCallEvent.GrainKeyExtention.ShouldBeNull();
            serviceCallEvent.GrainKeyGuid.ToString().ShouldBe(id);
            serviceCallEvent.GrainKeyString.ShouldBeNull();
            serviceCallEvent.GrainKeyLong.ShouldBeNull();


            id = await GrainFactory.GetGrain<IUserGrainWithLong>(123).GetIdentety();
            await Task.Delay(150);
            serviceCallEvent = eventPublisher.Events.OfType<GrainCallEvent>().Last();
            serviceCallEvent.GrainKeyLong.ShouldBe(123);
            serviceCallEvent.GrainKeyString.ShouldBeNull();
            serviceCallEvent.GrainKeyGuid.ShouldBeNull();
            serviceCallEvent.GrainKeyExtention.ShouldBeNull();


            id = await GrainFactory.GetGrain<IUserGrainWithString>("test").GetIdentety();
            await Task.Delay(150);
            serviceCallEvent = eventPublisher.Events.OfType<GrainCallEvent>().Last();

            serviceCallEvent.GrainKeyString.ShouldBe("test");
            serviceCallEvent.GrainKeyExtention.ShouldBeNull();
            serviceCallEvent.GrainKeyLong.ShouldBeNull();
            serviceCallEvent.GrainKeyGuid.ShouldBeNull();


        }


    }

}