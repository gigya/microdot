//#region Copyright 
//// Copyright 2017 Gigya Inc.  All rights reserved.
//// 
//// Licensed under the Apache License, Version 2.0 (the "License"); 
//// you may not use this file except in compliance with the License.  
//// You may obtain a copy of the License at
//// 
////     http://www.apache.org/licenses/LICENSE-2.0
//// 
//// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
//// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
//// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
//// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
//// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
//// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
//// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
//// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
//// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
//// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
//// POSSIBILITY OF SUCH DAMAGE.
//#endregion


//using System.Collections.Generic;
//using System.Linq;
//using Gigya.Microdot.Configuration;
//using Gigya.Microdot.Fakes;
//using Gigya.Microdot.Interfaces.Events;
//using Gigya.Microdot.Interfaces.Logging;
//using Gigya.Microdot.Ninject;
//using Gigya.Microdot.Orleans.Ninject.Host;
//using Gigya.Microdot.UnitTests.Caching.Host;
//using Ninject;
//using Ninject.Syntax;
//using Shouldly;



//namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService
//{
//    //Calculator - Inheritance should be removed!
//    public class WithAgeLimitServiceHost : CalculatorServiceHost
//    {


//        protected override string ServiceName => "AgeLimitService";
//        protected override void Configure(IKernel kernel, OrleansCodeConfig commonConfig)
//        {
//            var configDic = new Dictionary<string, string> {
//                { "OrleansConfig.defaultGrainAgeLimitInMins", "30" },
//                { "OrleansConfig.GrainAgeLimits.SiteService.grainAgeLimitInMins", "1"} ,
//                { "OrleansConfig.GrainAgeLimits.SiteService.grainType"          , "Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService.GrainAgeLimitService"}};



//            kernel.Rebind<IConfigItemsSource, OverridableConfigItems>()
//                .To<OverridableConfigItems>()
//                .InSingletonScope()
//                .WithConstructorArgument("data", configDic);

//            base.Configure(kernel, commonConfig);
//        }
//    }

//}