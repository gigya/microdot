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
using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.SharedLogic.Utils
{

    public static class Extensions
    {

        public static string RawMessage(this Exception ex) => (ex as SerializableException)?.RawMessage ?? ex.Message;


        /// <summary>
        /// Returns a function that, when called, checks if the instance of the object you're calling this on was
        /// garbage collected or not (only a weak reference to the object is held, so it can be garbage collected). If
        /// it wasn't collected, the function call your lambda, passing it the object, and returns you lambda's result.
        /// Otherwise, the function returns the default value of your lambda's return type.
        /// </summary>
        internal static Func<V> IfNotGarbageCollected<T, V>(this T instance, Func<T, V> getter) where T:class
        {
            var weakRef = new WeakReference<T>(instance);
            return () => {
                T inst;
                if (weakRef.TryGetTarget(out inst))
                    return getter(inst);
                else
                    return default(V);
            };
        } 
    }
}
