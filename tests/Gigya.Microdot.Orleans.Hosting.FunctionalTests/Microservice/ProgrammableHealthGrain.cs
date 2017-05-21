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

using System.Threading.Tasks;
using Gigya.Common.Contracts.HttpService;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Orleans;

namespace Gigya.Microdot.Orleans.Hosting.FunctionalTests.Microservice
{
    [HttpService(6555)]
    public interface IProgrammableHealth { }

    public interface IProgrammableHealthGrain : IProgrammableHealth, IHealthStatus, IGrainWithIntegerKey
    {
        Task SetHealth(bool healthy);
    }

    public class ProgrammableHealthGrain : Grain, IProgrammableHealthGrain
    {
        private HealthStatusResult Health { get; set; }


        public async Task<HealthStatusResult> Status()
        {
            return Health;
            //return new HealthStatusResult("I am not feeling well because I suffer from cold and headache", false);
        }


        public async Task SetHealth(bool healthy)
        {
            Health = healthy ? new HealthStatusResult("I'm healthy") : new HealthStatusResult("I'm not healthy", false);
        }
    }
}