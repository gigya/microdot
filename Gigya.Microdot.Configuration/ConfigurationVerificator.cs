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
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.SharedLogic.Exceptions;


namespace Gigya.Microdot.Configuration
{
    /// <summary>
    /// Incapsulates logic of IConfigObject descendants verification.
    /// </summary>
    public class ConfigurationVerificator
    {
        /// <summary>
        /// Incapsulates logic of storing and formatting results of verification run.
        /// </summary>
        public class Results
        {

            /// <summary>
            /// The summary of verification formatting strategy
            /// </summary>
            public enum SummaryFormat
            {
                /// <summary>
                /// Plaint console output
                /// </summary>
                Console,
                /// <summary>
                /// The TeamCity structured output
                /// </summary>
                TeamCity
            }

            /// <summary>
            /// A summary of specific type verification (a success or failure)
            /// </summary>
            public class ResultPerType
            {
                /// <summary>
                /// The verified config type
                /// </summary>
                public Type Type;
                /// <summary>
                /// The path of config file having an issue
                /// </summary>
                public string Path;
                /// <summary>
                /// The details of an issue with instantiating the config type
                /// </summary>
                public string Details;

                /// <summary>
                /// </summary>
                public ResultPerType (Type configType, string configPath, string validationErrors)
                {
                    Type = configType;
                    Path = configPath;
                    Details = validationErrors;
                }
            }

            private class TestSuiteContext : IDisposable
            {
                private readonly string _groupName;
                private readonly StringBuilder _buffer;
                private int _dispose;

                public TestSuiteContext(string groupName, StringBuilder buffer)
                {
                    if (groupName.Contains("'")) 
                        throw new ArgumentException("groupName can't contain '");
                    
                    if (groupName.Contains(Environment.NewLine)) 
                        throw new ArgumentException("groupName NewLine");

                    _groupName = groupName;
                    _buffer = buffer;

                    WriteLine($"##teamcity[testSuiteStarted  name='{_groupName}']");

                }

                public TestContext AddTest(string name)
                {
                    var result = new TestContext(name, _buffer);
                    return result;
                }

                public void Dispose()
                {
                    if (Interlocked.CompareExchange(ref _dispose, 1, 0) == 1) 
                        return;
                    
                    WriteLine($"##teamcity[testSuiteFinished  name='{_groupName}']");
                }

                private void WriteLine(string text)
                {
                    _buffer.AppendLine(text);
                }
            }

            private class TestContext : IDisposable
            {
                private readonly string _name;
                private int _dispose = 0;
                private readonly StringBuilder _buffer;

                internal TestContext(string testName, StringBuilder buffer)
                {
                    //  if (testName.Length > 50) throw new ArgumentException("testName is to long");
                    if (testName.Contains("'")) throw new ArgumentException("testName can't contain '");
                    if (testName.Contains(Environment.NewLine)) throw new ArgumentException("testName NewLine");
                    _name = testName;
                    _buffer = buffer;
                    WriteLine($"##teamcity[testStarted name='{_name}']");
                }

                public void ReportFailure(string message, string details)
                {
                    WriteLine($"##teamcity[testFailed name='{_name}'  message='{message}' details='{details}']");

                }
                public void ReportFailure(string details)
                {
                    WriteLine($"##teamcity[testFailed name='{_name}'  details='{details}']");
                }

                public void WriteMessage(string text)
                {
                    WriteLine($"##teamcity[testStdOut name='{_name}'  out='{text}']");
                }

                public void Dispose()
                {
                    if (Interlocked.CompareExchange(ref _dispose, 1, 0) == 0)
                        WriteLine($"##teamcity[testFinished  name='{_name}']");
                }

                private void WriteLine(string text)
                {
                    _buffer.AppendLine(text);
                }
            }

            /// <summary>
            /// Summarize the success of verification. False if at least one of types
            /// failed to pass the verification or any other failure, else True.
            /// </summary>
            public bool IsSuccess => _failedList.Any() == false;

            /// <summary>
            /// The total time in ms the verification took.
            /// </summary>
            public long ElapsedMs;
            private readonly List<ResultPerType> _failedList;
            private readonly List<Type> _passedList;
            private readonly string _environment;

            public IEnumerable<ResultPerType> Failed => _failedList;
            public IEnumerable<Type> Passed => _passedList;
            public SummaryFormat Format { get; private set; }

            /// <summary>
            /// </summary>
            /// <param name="environment">The environment where results were gethered.</param>
            public Results(string environment)
            {
                _environment = environment;
                _failedList = new List<ResultPerType>();
                _passedList = new List<Type>();
                Format = SummaryFormat.Console;
            }

            /// <summary>
            /// Add indication the type passed the verification
            /// </summary>
            public void AddSuccess(Type configType)
            {
                _passedList.Add(configType);
            }

            /// <summary>
            /// Add indication the type didn't pass the verification with more details.
            /// </summary>
            public void AddFailure(Type configType, string configPath, string validationErrors)
            {
                _failedList.Add(new ResultPerType(configType, configPath, validationErrors));
            }

            /// <summary>
            /// Summarize verification results into a string for Console or TeamCity.
            /// </summary>
            public string Summarize(SummaryFormat? format = null)
            {
                // Recognize when running under the TeamCity
                // https://confluence.jetbrains.com/display/TCD9/Predefined+Build+Parameters
                format = format ?? (Environment.GetEnvironmentVariable("BUILD_NUMBER") != null 
                                            ? SummaryFormat.TeamCity 
                                            : SummaryFormat.Console);

                this.Format = format.Value;

                var summary = new StringBuilder();

                switch (format)
                {
                    case SummaryFormat.Console:
                    {
                        if (_failedList.Count > 0)
                            summary.AppendLine($"--->>>> Configuration objects FAILED to pass the verification <<<<-----".ToUpper());

                        _failedList.ForEach(failure =>
                        {
                            summary.AppendLine($"TYPE: {failure.Type?.FullName}");
                            summary.AppendLine($"       PATH :  {failure.Path}");
                            summary.AppendLine($"       ERROR:  {failure.Details}");
                        });

                        if (_passedList.Count > 0)
                            summary.AppendLine($"The following {_passedList.Count} configuration objects passed the verification:");

                        _passedList.ForEach(item => summary.AppendLine($"    {item.FullName}"));
                        
                        return summary.ToString();
                    }
                    case SummaryFormat.TeamCity:
                    {
                        // The title of suite describes the current environment
                        using (var suite = new TestSuiteContext(_environment, summary))
                        {
                            _failedList.ForEach(failure =>
                            {
                                using (var test = suite.AddTest(failure.Type?.FullName))
                                    test.ReportFailure($"Path: {failure.Path}", failure.Details);
                            });
                            _passedList.ForEach(item =>
                            {
                                using (var test = suite.AddTest(item.FullName))
                                    test.WriteMessage("Verified successfully.");
                            });
                        }
                        // Avoid additional newlines, as TC is sensitive for
                        return summary.ToString().Trim();
                    }
                    default:
                        throw new ArgumentException($"Unsupported summary format: {format}");
                }
            }
        }

        private readonly Func<Type, ConfigObjectCreator> _configCreatorFunc;
        private readonly IAssemblyProvider _assemblyProvider;
        private readonly IEnvironmentVariableProvider _envProvider;

        /// <summary>
        /// </summary>
        public ConfigurationVerificator (Func<Type, ConfigObjectCreator> configCreatorFunc, IAssemblyProvider assemblyProvider, IEnvironmentVariableProvider envProvider)
        {
            _configCreatorFunc = configCreatorFunc;
            _assemblyProvider = assemblyProvider;
            _envProvider = envProvider;
        }

        /// <summary>
        /// Run the discovery of IConfigObject descendants, instanciate them and grab validation or any other errors.
        /// </summary>
        /// <remarks>
        /// Throwing, except <see cref="ConfigurationException"/>. On purpose to expose any exceptions except this one.
        /// </remarks>
        public Results Verify()
        {
            var environment = $"{_envProvider.DataCenter}-{_envProvider.DeploymentEnvironment}";
            var result = new Results(environment);
            var watch = Stopwatch.StartNew();

            // Get ConfigObject types in related binaries
            var configObjectTypes = _assemblyProvider.GetAllTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IConfigObject).IsAssignableFrom(t))
                .AsEnumerable();

            // Instanciate and grab validation issues
            foreach (var configType in configObjectTypes)
            {
                try
                {
                    var creator = _configCreatorFunc(configType);
                    creator.Init();

                    // ReSharper disable once UnusedVariable
                    // Only to review in debugging session
                    var objConfig = creator.GetLatest();

                    result.AddSuccess(configType);
                }
                catch (ConfigurationException ex)
                {
                    var path = ex.UnencryptedTags?["ConfigObjectPath"] ?? ex.Message;
                    var error = ex.UnencryptedTags?["ValidationErrors"] ?? ex.InnerException?.Message;
                    result.AddFailure(configType, path, error);
                }
            }

            result.ElapsedMs = watch.ElapsedMilliseconds;
            return result;
        }
    }
}