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

namespace Gigya.Microdot.Testing.Orleans.ServiceTester
{
    /// <summary>
    /// OrleansTestingSilo creates an AppDomain for each silo.
    /// This makes functional testing very difficult, because you can not change the behavior of the
    /// silo according to the test.  (every AppDomain is isolated has different static filed and more).
    /// The AppDomainExtentions is made to help change the silo behavior in the middle of the test.
    /// </summary>
    public static class AppDomainExtentions
    {
        /// <summary>
        /// Provide a way to run a delegate on specific AppDomain by using .NET Remoting.
        /// Notice: don't use closures.
        /// </summary>
        public static void RunOnContext(this AppDomain appDomain, Action func)
        {
            if (appDomain == null) throw new ArgumentNullException(nameof(appDomain));
            if (func == null) throw new ArgumentNullException(nameof(func));
            RunInAppDomain(new ActionDelegateWrapper { Func = func }, appDomain);
        }

        /// <summary>
        /// Provide a way to run a delegate on specific AppDomain by using .NET Remoting.
        /// Notice: don't use closures.
        /// </summary>
        /// <param name="parameter1">Must be serializable</param>
        /// <returns>Must be serializable</returns>
        public static TReturn RunOnContext<T1, TReturn>(this AppDomain appDomain, T1 parameter1, Func<T1, TReturn> func)
        {
            if (appDomain == null) throw new ArgumentNullException(nameof(appDomain));
            if (func == null) throw new ArgumentNullException(nameof(func));
            return RunInAppDomain(new FuncDelegateWrapper<T1, TReturn> { Func = func, Parameter1 = parameter1 }, appDomain);
        }

        /// <summary>
        /// Provide a way to run a delegate on specific AppDomain by using .NET Remoting.
        /// Notice: don't use closures.
        /// </summary>
        /// <param name="parameter1">Must be serializable</param>
        /// <param name="parameter2">Must be serializable</param>
        /// <returns>Must be serializable</returns>
        public static TReturn RunOnContext<T1, T2, TReturn>(this AppDomain appDomain, T1 parameter1, T2 parameter2, Func<T1, T2, TReturn> func)
        {
            if (appDomain == null) throw new ArgumentNullException(nameof(appDomain));
            if (func == null) throw new ArgumentNullException(nameof(func));
            return RunInAppDomain(new FuncDelegateWrapper<T1, T2, TReturn> { Func = func, Parameter1 = parameter1, Parameter2 = parameter2 }, appDomain);
        }

        /// <summary>
        /// Provide a way to run a delegate on specific AppDomain by using .NET Remoting.
        /// Notice: don't use closures.
        /// </summary>
        /// <param name="parameter1">Must be serializable</param>
        /// <param name="parameter2">Must be serializable</param>
        /// <param name="parameter3">Must be serializable</param>
        /// <returns>Must be serializable</returns>
        public static TReturn RunOnContext<T1, T2, T3, TReturn>(this AppDomain appDomain, T1 parameter1, T2 parameter2, T3 parameter3, Func<T1, T2, T3, TReturn> func)
        {
            if (appDomain == null) throw new ArgumentNullException(nameof(appDomain));
            if (func == null) throw new ArgumentNullException(nameof(func));
            return RunInAppDomain(new FuncDelegateWrapper<T1, T2, T3, TReturn> { Func = func, Parameter1 = parameter1, Parameter2 = parameter2, Parameter3 = parameter3 }, appDomain);
        }

        /// <summary>
        /// Provide a way to run a delegate on specific AppDomain by using .NET Remoting.
        /// Notice: don't use closures.
        /// </summary>
        /// <returns>Must be serializable</returns>
        public static TReturn RunOnContext<TReturn>(this AppDomain appDomain, Func<TReturn> func)
        {
            if (appDomain == null) throw new ArgumentNullException(nameof(appDomain));
            if (func == null) throw new ArgumentNullException(nameof(func));
            return RunInAppDomain(new FuncDelegateWrapper<TReturn> { Func = func }, appDomain);
        }

        /// <summary>
        /// Provide a way to run a delegate on specific AppDomain by using .NET Remoting.
        /// Notice: don't use closures.
        /// </summary>
        /// <param name="parameter1">Must be serializable</param>
        /// <returns>Must be serializable</returns>
        public static void RunOnContext<T1>(this AppDomain appDomain, T1 parameter1, Action<T1> func)
        {
            if (appDomain == null) throw new ArgumentNullException(nameof(appDomain));
            if (func == null) throw new ArgumentNullException(nameof(func));
            RunInAppDomain(new ActionDelegateWrapper<T1> { Func = func, Parameter1 = parameter1 }, appDomain);
        }

        /// <summary>
        /// Provide a way to run a delegate on specific AppDomain by using .NET Remoting.
        /// Notice: don't use closures.
        /// </summary>
        /// <param name="parameter1">Must be serializable</param>
        /// <param name="parameter2">Must be serializable</param>
        /// <returns>Must be serializable</returns>
        public static void RunOnContext<T1, T2>(this AppDomain appDomain, T1 parameter1, T2 parameter2, Action<T1, T2> func)
        {
            if (appDomain == null) throw new ArgumentNullException(nameof(appDomain));
            if (func == null) throw new ArgumentNullException(nameof(func));
            RunInAppDomain(new ActionDelegateWrapper<T1, T2> { Func = func, Parameter1 = parameter1, Parameter2 = parameter2 }, appDomain);
        }

        /// <summary>
        /// Provide a way to run a delegate on specific AppDomain by using .NET Remoting.
        /// Notice: don't use closures.
        /// </summary>
        /// <param name="parameter1">Must be serializable</param>
        /// <param name="parameter2">Must be serializable</param>
        /// <param name="parameter3">Must be serializable</param>
        /// <returns>Must be serializable</returns>
        public static void RunOnContext<T1, T2, T3>(this AppDomain appDomain, T1 parameter1, T2 parameter2, T3 parameter3, Action<T1, T2, T3> func)
        {
            if (appDomain == null) throw new ArgumentNullException(nameof(appDomain));
            if (func == null) throw new ArgumentNullException(nameof(func));
            RunInAppDomain(new ActionDelegateWrapper<T1, T2, T3> { Func = func, Parameter1 = parameter1, Parameter2 = parameter2, Parameter3 = parameter3 }, appDomain);
        }

        /// <summary>
        /// Provide a way to run a delegate on specific AppDomain by using .NET Remoting.
        /// Notice: don't use closures.
        /// </summary>
        /// <param name="parameter1">Must be serializable</param>
        /// <param name="parameter2">Must be serializable</param>
        /// <param name="parameter3">Must be serializable</param>
        /// <param name="parameter4">Must be serializable</param>
        /// <returns>Must be serializable</returns>
        public static void RunOnContext<T1, T2, T3, T4>(this AppDomain appDomain, T1 parameter1, T2 parameter2, T3 parameter3, T4 parameter4, Action<T1, T2, T3, T4> func)
        {
            if (appDomain == null) throw new ArgumentNullException(nameof(appDomain));
            if (func == null) throw new ArgumentNullException(nameof(func));
            RunInAppDomain(new ActionDelegateWrapper<T1, T2, T3, T4> { Func = func, Parameter1 = parameter1, Parameter2 = parameter2, Parameter3 = parameter3, Parameter4 = parameter4 }, appDomain);
        }

        public const string ToInvoke = "toInvoke";
        public const string Result = "result";

        public static TReturn RunInAppDomain<TReturn>(IDelegateWrapper<TReturn> func, AppDomain appDomain)
        {
            AppDomain domain = appDomain;

            domain.SetData(name: ToInvoke, data: func);
            domain.DoCallBack(() =>
            {
                var delegateWrapper = AppDomain.CurrentDomain.GetData(ToInvoke) as IDelegateWrapper<TReturn>;
                if (delegateWrapper == null) throw new ArgumentNullException(nameof(func));
                AppDomain.CurrentDomain.SetData(Result, delegateWrapper.Run());
            });

            return (TReturn)domain.GetData(Result);
        }

        public interface IDelegateWrapper<out TReturn>
        {
            TReturn Run();
        }

        [Serializable]
        private class FuncDelegateWrapper<TReturn> : IDelegateWrapper<TReturn>
        {
            public Func<TReturn> Func;

            public TReturn Run()
            {
                return Func();
            }
        }

        [Serializable]
        private class FuncDelegateWrapper<T1, TReturn> : IDelegateWrapper<TReturn>
        {
            public T1 Parameter1;
            public Func<T1, TReturn> Func;

            public TReturn Run()
            {
                return Func(Parameter1);
            }
        }

        [Serializable]
        private class FuncDelegateWrapper<T1, T2, TReturn> : IDelegateWrapper<TReturn>
        {
            public T1 Parameter1;
            public T2 Parameter2;
            public Func<T1, T2, TReturn> Func;

            public TReturn Run()
            {
                return Func(Parameter1, Parameter2);
            }
        }

        [Serializable]
        private class FuncDelegateWrapper<T1, T2, T3, TReturn> : IDelegateWrapper<TReturn>
        {
            public T1 Parameter1;
            public T2 Parameter2;
            public T3 Parameter3;
            public Func<T1, T2, T3, TReturn> Func;

            public TReturn Run()
            {
                return Func(Parameter1, Parameter2, Parameter3);
            }
        }

        [Serializable]
        private class ActionDelegateWrapper : IDelegateWrapper<int>
        {
            public Action Func;

            public int Run()
            {
                Func();
                return 0;
            }
        }

        [Serializable]
        private class ActionDelegateWrapper<T> : IDelegateWrapper<int>
        {
            public T Parameter1;
            public Action<T> Func;

            public int Run()
            {
                Func(Parameter1);
                return 0;
            }
        }

        [Serializable]
        private class ActionDelegateWrapper<T1, T2> : IDelegateWrapper<int>
        {
            public T1 Parameter1;
            public T2 Parameter2;
            public Action<T1, T2> Func;

            public int Run()
            {
                Func(Parameter1, Parameter2);
                return 0;
            }
        }

        [Serializable]
        private class ActionDelegateWrapper<T1, T2, T3> : IDelegateWrapper<int>
        {
            public T1 Parameter1;
            public T2 Parameter2;
            public T3 Parameter3;
            public Action<T1, T2, T3> Func;

            public int Run()
            {
                Func(Parameter1, Parameter2, Parameter3);
                return 0;
            }
        }

        [Serializable]
        private class ActionDelegateWrapper<T1, T2, T3, T4> : IDelegateWrapper<int>
        {
            public T1 Parameter1;
            public T2 Parameter2;
            public T3 Parameter3;
            public T4 Parameter4;

            public Action<T1, T2, T3, T4> Func;

            public int Run()
            {
                Func(Parameter1, Parameter2, Parameter3, Parameter4);
                return 0;
            }
        }
    }
}