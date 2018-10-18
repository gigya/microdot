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
using System.Reflection;
using System.Reflection.DispatchProxy;

namespace Gigya.Microdot.ServiceProxy.Rewrite
{
    public interface IProxyable
    {
        object Invoke(MethodInfo targetMethod, object[] args);
    }

    public static class ProxyableExtentions
    {
        public static TInterface ToProxy<TInterface>(this IProxyable proxyable)
        {
            var proxy = DispatchProxy.Create<TInterface, DelegatingDispatchProxy>();
            ((DelegatingDispatchProxy)(object)proxy).InvokeDelegate = proxyable.Invoke;
            return proxy;
        }

        public static object ToProxy(this IProxyable proxyable, Type proxyType)
        {
            var createMethod = typeof(DispatchProxy)
                .GetMethod(nameof(DispatchProxy.Create), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(proxyType, typeof(DelegatingDispatchProxy));

            var proxy = (DelegatingDispatchProxy)createMethod.Invoke(null, new object[0]);
            proxy.InvokeDelegate = proxyable.Invoke;

            return proxy;
        }
    }
}