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

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Gigya.Microdot.Hosting")]
[assembly: AssemblyProduct("Gigya.Microdot.Hosting")]

[assembly: Guid("27abc89f-fe0c-44df-b0db-ac951015d281")]

[assembly: InternalsVisibleTo("Gigya.Common.Application.UnitTests")]
[assembly: InternalsVisibleTo("Gigya.Microdot.UnitTests")]
[assembly: InternalsVisibleTo("Gigya.Common.Application.FunctionalTests")]
[assembly: InternalsVisibleTo("Gigya.Common.OrleansInfra.FunctionalTests")]


[assembly: InternalsVisibleTo("Gigya.Common.OrleansInfra")]
[assembly: InternalsVisibleTo("Gigya.Common.TestingHelpers")]
[assembly: InternalsVisibleTo("Gigya.MySql.Client")]
[assembly: InternalsVisibleTo("Gigya.Common.Application.Ninject")]
[assembly: InternalsVisibleTo("Gigya.Common.OrleansInfra.Ninject")]

[assembly: InternalsVisibleTo("LINQPadQuery")]

