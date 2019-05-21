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
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.Testing.Shared;
using Ninject.Syntax;
using NUnit.Framework;

    [SetUpFixture]
    public class AssemblyInitialize
    {

        public static IResolutionRoot ResolutionRoot { get; private set; }

        private TestingKernel<ConsoleLog> kernel;

        [OneTimeSetUp]
        public void SetUp()
        {
            try
            {
                Environment.SetEnvironmentVariable("GIGYA_CONFIG_ROOT", AppDomain.CurrentDomain.BaseDirectory, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("REGION", "us1", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("ZONE", "us1a", EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("ENV", "_Test", EnvironmentVariableTarget.Process);

                kernel = new TestingKernel<ConsoleLog>((kernel) =>
                {
                    var revokingManager = new FakeRevokingManager();
                    kernel.Rebind<IRevokeListener>().ToConstant(revokingManager);
                    kernel.Rebind<ICacheRevoker>().ToConstant(revokingManager);
                    kernel.Rebind<IMetricsInitializer>().To<MetricsInitializerFake>();
                    kernel.Rebind<IEventPublisher>().To<SpyEventPublisher>().InSingletonScope();

                });            
                ResolutionRoot = kernel;
            }
            catch(Exception ex)
            {
                Console.Write(ex);
                throw;
            }
        
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            kernel.Dispose();
        }
    }
