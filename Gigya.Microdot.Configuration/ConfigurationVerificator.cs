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
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Gigya.Microdot.SharedLogic.Exceptions;

// ReSharper disable RedundantDefaultMemberInitializer
#pragma warning disable 1591 // XML docs for public members

namespace Gigya.Microdot.Configuration
{
    /// <summary>
    /// Encapsulates logic of IConfigObject descendants verification.
    /// </summary>
    public class ConfigurationVerificator
    {
        /// <summary>
        /// A summary of specific type verification (a success or failure)
        /// </summary>
        [DebuggerDisplay("{Success}:{Type.FullName}")]
        public class ResultPerType
        {
            /// <summary>
            /// The verified config type
            /// </summary>
            public Type Type;
            /// <summary>
            /// The path of config file having an issue
            /// </summary>
            public string Path = null;
            /// <summary>
            /// The details of an issue with instantiating the config type
            /// </summary>
            public string Details = null;
            /// <summary>
            /// Indicates the type passed the verification
            /// </summary>
            public bool Success;
            /// <summary>
            /// Contextual metadata
            /// </summary>
            public string MetaData;

            public override string ToString()
            {
                string status = Success ? "OK": "ERROR";
                string detail = Details?.Replace("\t", " ");
                return $"{status}\t{MetaData}\t{Type.FullName}\t{Path}\t{detail}";
            }
        }

        private readonly Func<Type, IConfigObjectCreator> _configCreatorFunc;
        private readonly IAssemblyProvider _assemblyProvider;
        private readonly IEnvironment _envProvider;

        /// <summary>
        /// </summary>
        public ConfigurationVerificator (Func<Type, IConfigObjectCreator> configCreatorFunc, IAssemblyProvider assemblyProvider, IEnvironment envProvider)
        {
            _configCreatorFunc = configCreatorFunc;
            _assemblyProvider = assemblyProvider;
            _envProvider = envProvider;
        }

        /// <summary>
        /// Run the discovery of IConfigObject descendants, instantiate them and grab validation or any other errors.
        /// </summary>
        /// <remarks>
        /// Throwing, except <see cref="ConfigurationException"/>. On purpose to expose any exceptions except this one.
        /// </remarks>
        public ICollection<ResultPerType> Verify()
        {
            var metadata = $"{_envProvider.Zone}-{_envProvider.DeploymentEnvironment}";
            var results = new List<ResultPerType>();

            // Get ConfigObject types in related binaries
            var configObjectTypes = _assemblyProvider.GetAllTypes().Where(ConfigObjectCreator.IsConfigObject).AsEnumerable();

            // Instantiate and grab validation issues
            foreach (var configType in configObjectTypes)
            {
                try
                {
                    var creator = _configCreatorFunc(configType);

                    // ReSharper disable once UnusedVariable
                    // Only to review in debugging session
                    var objConfig = creator.GetLatest();

                    results.Add(new ResultPerType
                    {
                        Success = true,
                        Type = configType,
                        MetaData = metadata
                    });
                }
                catch (ConfigurationException ex)
                {
                    var path = ex.UnencryptedTags?["ConfigObjectPath"] ?? ex.Message;
                    var error = ex.UnencryptedTags?["ValidationErrors"] ?? ex.InnerException?.Message;
                    results.Add(new ResultPerType
                    {
                        Success = false,
                        Type = configType,
                        MetaData = metadata,
                        Path = path,
                        Details = error
                    });
                }
            }

            return results;
        }
    }
}