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

using Gigya.Microdot.Interfaces.Events;
using Gigya.Microdot.Interfaces.SystemWrappers;
using System;

namespace Gigya.Microdot.SharedLogic.Events
{
    public class EventFactory<T> : IEventFactory<T>  where T : IEvent
    {
        private readonly CurrentApplicationInfo _appInfo;
        private readonly IEnvironment _environment;
        private readonly Func<T> _eventFactory;

        public EventFactory(Func<T> eventFactory, CurrentApplicationInfo appInfo, IEnvironment environment)
        {
            _appInfo = appInfo;
            _environment = environment;
            _eventFactory = eventFactory;
        }

        public T CreateEvent()
        {
            var evt = _eventFactory();

            // Add Application information
            evt.ServiceName = _appInfo.Name;
            evt.ServiceInstanceName = _environment.InstanceName;
            evt.ServiceVersion = _appInfo.Version.ToString(4);
            evt.InfraVersion = _appInfo.InfraVersion.ToString(4);

            return  evt;
        }
    }
}