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

using Gigya.Microdot.Orleans.Hosting.Logging;
using Microsoft.Extensions.Logging;

namespace Gigya.Microdot.Orleans.Ninject.Host.NinjectOrleansBinding
{
    /// <summary>
    /// Replacing the original Microsoft Logger factory to avoid blocking code.
    /// Ninject using lock by scope which leading to deadlock in this scenario.
    /// </summary>
    public class NonBlockingLoggerFactory : ILoggerFactory
    {
        // ReSharper disable once InconsistentNaming
        private ILoggerProvider LoggerProvider;
        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public ILogger CreateLogger(string categoryName)
        {
            return LoggerProvider.CreateLogger(categoryName);
        }

        public void AddProvider(ILoggerProvider provider)
        {
            LoggerProvider = provider;
        }

        public NonBlockingLoggerFactory(OrleansLogProvider provider)
        {
            LoggerProvider = provider;
        }
    }
}