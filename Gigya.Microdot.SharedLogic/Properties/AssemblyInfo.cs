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

using Gigya.Microdot.SharedLogic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("c88db2a8-a1d2-46f8-8b65-06b9ee3f1662")]

[assembly: InternalsVisibleTo("Gigya.Microdot.Hosting")]
[assembly: InternalsVisibleTo("Gigya.Microdot.Fakes")]
[assembly: InternalsVisibleTo("Gigya.Microdot.Orleans.Hosting")]
[assembly: InternalsVisibleTo("Gigya.Microdot.ServiceProxy")]
[assembly: InternalsVisibleTo("Gigya.Microdot.Logging.NLog")]
[assembly: InternalsVisibleTo("Gigya.Common.Logging")]
[assembly: InternalsVisibleTo("Gigya.Common.Application.Ninject")]
[assembly: InternalsVisibleTo("Gigya.Common.Application.UnitTests")]
[assembly: InternalsVisibleTo("Gigya.Common.Application.FunctionalTests")]
[assembly: InternalsVisibleTo("Gigya.Common.OrleansInfra.FunctionalTests")]
[assembly: InternalsVisibleTo("Gigya.Common.OrleansInfra.TestingTools")]
[assembly: InternalsVisibleTo("Gigya.Common.TestingHelpers")]
[assembly: InternalsVisibleTo("Gigya.Hades.Client")]
[assembly: InternalsVisibleTo("Gigya.Microdot.Orleans.Ninject.Host")]
[assembly: InternalsVisibleTo("Gigya.Microdot.Ninject.Host")]
[assembly: InternalsVisibleTo("Gigya.Microdot.Ninject")]
[assembly: InternalsVisibleTo("Gigya.Microdot.Orleans.Hosting.UnitTests")]

[assembly: TypeForwardedToAttribute(typeof(CurrentApplicationInfo))]