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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Fakes.Discovery;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.Exceptions;
using Gigya.Microdot.SharedLogic.HttpService;
using Gigya.Microdot.Testing.Shared;
using Gigya.Microdot.UnitTests.Events;
using Ninject;
using NUnit.Framework;
using Shouldly;

namespace Gigya.Microdot.UnitTests.ServiceProxyTests
{
    [TestFixture]
    public class RewriteTests
    {
        [Test]
        public async Task T()
        {
            var k = new TestingKernel<ConsoleLog>();
            var proxy = k.Get<DemoService.Interface.v1.IDemoService>();

            TracingContext.SetUpStorage();
            TracingContext.SetRequestID("requestId1");
            TracingContext.SetSpan("spanId2", "spanId1");
            Guid g = Guid.NewGuid();

            while (true)
            {
                try
                {
                    var result = await proxy.GetMachineInfo();
                    Debug.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} {result.Environment}");
                }
                catch { }

                await Task.Delay(1000);
            }
        }
    }
}

namespace DemoService.Interface.v1
{
    [HttpService(31500)]
    public interface IDemoService
    {
        Task<string> ToUpper(string str);

        [PublicEndpoint("demo.getMachineInfo", requireHTTPS: false)]
        Task<MachineInfo> GetMachineInfo();

        [PublicEndpoint("demo.guidsCounter", requireHTTPS: false)]
        Task<GuidCounterResult> GuidsCounter(string guid, string meaninglessField);
    }

    public class MachineInfo
    {
        public string HostName { get; set; }
        public string DataCenter { get; set; }
        public string Environment { get; set; }
        public string Version { get; set; }
    }

    public class GuidCounterResult
    {
        public int AmountOfGuidCalls { get; set; }

        public Guid Guid { get; set; }
    }
}
