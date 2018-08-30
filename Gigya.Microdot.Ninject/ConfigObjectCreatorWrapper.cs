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
using System.Linq.Expressions;
using System.Reflection;
using Gigya.Microdot.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Ninject;
#pragma warning disable 1591

namespace Gigya.Microdot.Ninject
{
    public class ConfigObjectCreatorWrapper : IConfigObjectCreatorWrapper
    {
        private IConfigObjectCreator _configObjectCreator;
        private readonly Type _configType;
        private readonly IKernel _kernel;
        private readonly object _lockObject = new object();

        public ConfigObjectCreatorWrapper(IKernel kernel, Type type)
        {
            _kernel = kernel;
            _configType = type;
        }

        public object GetLatest()
        {
            EnsureCreator();
            return _configObjectCreator.GetLatest();
        }

        public Func<T> GetTypedLatestFunc<T>() where T : class => () => GetLatest() as T;
        public Func<T> GetChangeNotificationsFunc<T>() where T : class => () => GetChangeNotifications() as T;

        public object GetChangeNotifications()
        {
            EnsureCreator();
            return _configObjectCreator.ChangeNotifications;
        }

        public dynamic GetGenericFuncCompiledLambda(Type configType, string functionName)
        {//happens only once while loading, but can be optimized by creating Method info before sending to this function, if needed
            MethodInfo func = typeof(IConfigObjectCreatorWrapper).GetMethod(functionName).MakeGenericMethod(configType);
            Expression instance = Expression.Constant(this);
            Expression callMethod = Expression.Call(instance, func);
            Type delegateType = typeof(Func<>).MakeGenericType(configType);
            Type parentExpressionType = typeof(Func<>).MakeGenericType(delegateType);

            dynamic lambda = Expression.Lambda(parentExpressionType, callMethod).Compile();

            return lambda;
        }

        private void EnsureCreator()
        {
            if (_configObjectCreator == null)
            {
                //Resolving from ninject should be performed out of the lock, to avoid potential dead locks, caused by locking, performed by ninject itself
                Func<Type, IConfigObjectCreator> getCreator = _kernel.Get<Func<Type, IConfigObjectCreator>>();
                IConfigObjectCreator uninitializedCreator = getCreator(_configType);

                lock (_lockObject)
                {
                    if (_configObjectCreator == null)
                    {
                        uninitializedCreator.Init();

                        _configObjectCreator = uninitializedCreator;
                    }
                }
            }
        }
    }
}
