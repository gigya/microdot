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

namespace Gigya.Microdot.SharedLogic.Utils
{
    public class Lazy<TValue, TParam>
    {
        private readonly object _syncObj = new object();

        private TValue _value;
        private bool _hasValue;
        private readonly Func<TParam, TValue> _func;

        public Lazy(Func<TParam, TValue> func)
        {
            _func = func;
        }

        public TValue GetValue(TParam parameter)
        {
            if (_hasValue) return _value;

            lock (_syncObj)
            {
                if (_hasValue) return _value;

                _value = _func(parameter);
                _hasValue = true;
            }

            return _value;
        }

        public TValue SetValue(TValue value)
        {
            if (_hasValue) return _value;

            lock (_syncObj)
            {
                if (_hasValue) return _value;

                _value = value;
                _hasValue = true;
            }

            return _value;
        }
    }
}
