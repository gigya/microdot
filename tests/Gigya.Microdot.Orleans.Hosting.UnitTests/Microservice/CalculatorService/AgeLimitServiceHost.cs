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


using System.Collections.Generic;
using Ninject;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService
{
    public class WithAgeLimitServiceHost : CalculatorServiceHost
    {
        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            var originConfig = kernel.Get<OrleansConfig>();
            originConfig.GrainAgeLimits = new Dictionary<string, GrainAgeLimitConfig>
            {
                [ServiceName] = new GrainAgeLimitConfig
                {
                    GrainType =
                        "Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService.GrainAgeLimitService, Gigya.Microdot.Orleans.Hosting.UnitTests",
                    GrainAgeLimitInMins = 10
                }
            };


        }
    }


    public class With10SecondsAgeLimitServiceHost : CalculatorServiceHost
    {
        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            var originConfig = kernel.Get<OrleansConfig>();
            originConfig.GrainAgeLimits = new Dictionary<string, GrainAgeLimitConfig>
            {
                [ServiceName] = new GrainAgeLimitConfig
                {
                    GrainType =
                        "Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService.GrainAgeLimitService, Gigya.Microdot.Orleans.Hosting.UnitTests",
                    GrainAgeLimitInMins = 1 // 10 seconds!
                    //GrainAgeLimitInMins = 0.5 // 10 seconds!
                }
            };


        }
    }

    public class WithInvalidAgeLimitServiceHost : CalculatorServiceHost
    {
        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            var originConfig = kernel.Get<OrleansConfig>();
            originConfig.GrainAgeLimits = new Dictionary<string, GrainAgeLimitConfig>
            {
                [ServiceName] = new GrainAgeLimitConfig
                {
                    GrainType = "Fake - Should throw exception.",
                    GrainAgeLimitInMins = 10
                }
            };


        }
    }

    public class WithNoneAgeLimitServiceHost : CalculatorServiceHost
    {
        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
        {
            var originConfig = kernel.Get<OrleansConfig>();
            originConfig.GrainAgeLimits = null;

            //var copyConfig = JsonConvert.DeserializeObject<OrleansConfig>(JsonConvert.SerializeObject(originConfig));
        }
    }
}