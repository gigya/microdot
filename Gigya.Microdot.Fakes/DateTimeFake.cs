﻿#region Copyright 
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
using Gigya.Microdot.Interfaces.SystemWrappers;

namespace Gigya.Microdot.Fakes
{
    public class DateTimeFake: IDateTime
    {
        public DateTime UtcNow { get; set; }

        private TaskCompletionSource<bool> _delayTask = new TaskCompletionSource<bool>();

        public List<TimeSpan> DelaysRequested { get; } = new List<TimeSpan>();

        public DateTimeFake() : this(true)
        {
        }

        private readonly bool _manualDelay;
        /// <param name="manualDelay">whether delays should be handled manually. If True, the delay method will return a task which will be finished only after calling the "StopDelay" method</param>
        public DateTimeFake(bool manualDelay)
        {
            _manualDelay = manualDelay;
        }

        public Task Delay(TimeSpan delay)
        {
            DelaysRequested.Add(delay);
            return _manualDelay ? _delayTask.Task : Task.Delay(delay);
        }

        /// <summary>
        /// Stop current delay
        /// </summary>
        public void StopDelay()
        {
            var previousDelayTask = _delayTask;
            _delayTask = new TaskCompletionSource<bool>();
            previousDelayTask.SetResult(true);
        }
    }
}
