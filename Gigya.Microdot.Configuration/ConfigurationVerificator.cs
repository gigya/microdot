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
			private readonly bool _duringBuild;

			public IEnumerable<ResultPerType> Failed => _failedList;
			public IEnumerable<Type> Passed => _passedList;

			/// <summary>
			/// </summary>
			public Results()
			{
				_failedList = new List<ResultPerType>();
				_passedList = new List<Type>();

				// Recognize when running under the TeamCity, https://confluence.jetbrains.com/display/TCD9/Predefined+Build+Parameters
				_duringBuild = Environment.GetEnvironmentVariable("BUILD_NUMBER") != null;
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
			/// Format to string for console or TeamCity
			/// </summary>
			public override string ToString()
			{
				var buffer = new StringBuilder();
				
				//
				buffer.AppendLine($"Is under TC build? : {_duringBuild }");
				
				if (_failedList.Count > 0)
					buffer.AppendLine($"--->>>> Configuration objects failed to pass the verification <<<<-----".ToUpper());

				_failedList.ForEach(failure =>
				{
					buffer.AppendLine($"TYPE: {failure.Type?.FullName}");
					buffer.AppendLine($"       PATH :  {failure.Path}");
					buffer.AppendLine($"       ERROR:  {failure.Details}");
				});

				if (_passedList.Count > 0)
					buffer.AppendLine($"The following {_passedList.Count} configuration objects passed the verification:");

				_passedList.ForEach(item => buffer.AppendLine($"    {item.FullName}"));

				return buffer.ToString();
			}
		}

		private readonly Func<Type, ConfigObjectCreator> _configCreatorFunc;
		private readonly IAssemblyProvider _assemblyProvider;

		/// <summary>
		/// </summary>
		public ConfigurationVerificator (Func<Type, ConfigObjectCreator> configCreatorFunc, IAssemblyProvider assemblyProvider)
		{
			_configCreatorFunc = configCreatorFunc;
			_assemblyProvider = assemblyProvider;
		}

		/// <summary>
		/// Run the discovery of IConfigObject descendants, instanciate them and grab validation or any other errors.
		/// </summary>
		/// <remarks>
		/// Throwing, except <see cref="ConfigurationException"/>. On purpose to expose any exceptions except this one.
		/// </remarks>
		public Results Verify()
		{
			var result = new Results();
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
					// Only for review in debugging  session
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