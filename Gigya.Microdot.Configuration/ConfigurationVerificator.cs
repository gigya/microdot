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
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.SharedLogic.Exceptions;
using System.Text;
using Gigya.Microdot.Interfaces.Logging;

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
			/// Indicates the success of verification.
			/// </summary>
			/// <returns>False if at least one of types failed to pass the verification or any other failure, else True</returns>
			public bool IsSuccess => _failedList.Any();
			/// <summary>
			/// The total time in ms the verification took.
			/// </summary>
			public long ElapsedMs;
			private readonly List<KeyValuePair<Type, Tuple<string,string>>> _failedList;
			private readonly List<Type> _passedList;
			private readonly bool _duringBuild;

			/// <summary>
			/// </summary>
			public Results()
			{
				_failedList = new List<KeyValuePair<Type, Tuple<string, string>>>();
				_passedList = new List<Type>();

				// Recognize when running under the TeamCity
				// https://confluence.jetbrains.com/display/TCD9/Predefined+Build+Parameters
				_duringBuild = Environment.GetEnvironmentVariable("BUILD_NUMBER") != null;

			}

			/// <summary>
			/// Add indication the type passede verification
			/// </summary>
			/// <param name="configType"></param>
			public void AddSuccess(Type configType)
			{
				_passedList.Add(configType);
			}

			/// <summary>
			/// Add indication the type isn't passed verification with more details.
			/// </summary>
			/// <param name="configType"></param>
			/// <param name="configPath"></param>
			/// <param name="validationErrors"></param>
			public void AddFailure(Type configType, string configPath, string validationErrors)
			{
				_failedList.Add(new KeyValuePair<Type, Tuple<string, string>>(configType, new Tuple<string, string>(configPath, validationErrors)));
			}

			/// <summary>
			/// Format to string for console or TeamCity
			/// </summary>
			public override string ToString()
			{
				var buffer = new StringBuilder();

				if (!_duringBuild)
				{
					buffer.AppendLine();
					if (_failedList.Count > 0)
						buffer.AppendLine($"--->>>> Configuration objects failed to pass the verification <<<<-----".ToUpper());

					_failedList.ForEach(failure =>
					{
						buffer.AppendLine($"TYPE: {failure.Key?.FullName}");
						buffer.AppendLine($"       PATH :  {failure.Value.Item1}");
						buffer.AppendLine($"       ERROR:  {failure.Value.Item2}");
					});

					if (_passedList.Count > 0)
						buffer.AppendLine($"The following {_passedList.Count} configuration objects passed the verification:");

					_passedList.ForEach(item => buffer.AppendLine($"    {item.FullName}"));
				}
				else
				{
					buffer.AppendLine();
					// TODO: format the message with details accorfing to TC expectations
					// https://confluence.jetbrains.com/display/TCD18/Build+Script+Interaction+with+TeamCity#BuildScriptInteractionwithTeamCity-ReportingTests
				}

				return buffer.ToString();
			}
		}

		private readonly Func<Type, ConfigObjectCreator> _configCreatorFunc;
		private readonly IAssemblyProvider _configTypesProvider;
		private readonly ILog _log;

		/// <summary>
		/// </summary>
		public ConfigurationVerificator (Func<Type, ConfigObjectCreator> configCreatorFunc, IAssemblyProvider configTypesProvider, ILog log)
		{
			_configCreatorFunc = configCreatorFunc;
			_configTypesProvider = configTypesProvider;
			_log = log;
		}

		/// <summary>
		/// Run the discovery of IConfigObject descendants, instanciate them and grab validation or any other errors.
		/// </summary>
		public Results Verify()
		{
			var result = new Results();
			var watch = Stopwatch.StartNew();
			try
			{
				// Get ConfigObject types in related binaries
				var configObjectTypes = _configTypesProvider.GetAllTypes()
					.Where(t => t.IsClass && !t.IsAbstract && typeof(IConfigObject).IsAssignableFrom(t))
					.AsEnumerable();

				// Instanciate and grab validation issues
				foreach (var configType in configObjectTypes)
				{
					try
					{
						var creator = _configCreatorFunc(configType);
						creator.Init();
						creator.GetLatest();

						// throw new ConfigurationException("the type is not valid message", unencrypted: new Tags
						// {
						// 	{ "ConfigObjectPath", "some path to config" },
						// 	{ "ValidationErrors", "very interesting error" }
						// });

						result.AddSuccess(configType);
					}
					catch (ConfigurationException ex)
					{
						result.AddFailure(configType, ex.UnencryptedTags?["ConfigObjectPath"], ex.UnencryptedTags?["ValidationErrors"]);
					}
				}
			}
			catch (Exception ex)
			{
				result.AddFailure(null, "FAILED TO LOAD TYPES, check the service log for the details.", ex.Message);
				_log.Error(_ => _($"Failed to retrieve the IConfigObject descendants for verification", exception:ex, includeStack:true));
			}

			result.ElapsedMs = watch.ElapsedMilliseconds;
			return result;
		}
	}
}