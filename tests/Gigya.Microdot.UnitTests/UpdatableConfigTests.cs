using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.Interfaces.Configuration;
using Gigya.Microdot.Testing.Shared;
using Ninject;
using NUnit.Framework;

namespace Gigya.Microdot.UnitTests
{
    /// <summary>
    /// Enables dynamic config updates
    /// Intended for tests only
    /// By default creates TestingKernel during OneTimeSetUp flow
    /// To create TestingKernel during SetUp flow, override the OneTimeSetUp method
    /// </summary>
    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public abstract class UpdatableConfigTests
    {
        protected Dictionary<string, string> _configDic;
        protected TestingKernel<ConsoleLog> _unitTestingKernel;

        private bool _kernelCreatedAsSingleTone;

        /// <summary>
        /// Creates TerstingKernel with ConsoleLog
        /// If TestingKernel already created by OneTimeSetup, does nothing
        /// To create kernel on SetUp, overridde OneTimeSetUp method
        /// </summary>
        [SetUp]
        public virtual void Setup()
        {
            CreateKernel(false);
        }

        /// <summary>
        /// Creates TestingKernel with ConsoleLog
        /// Override this to disable OneTimeSetUp functionality
        /// </summary>
        [OneTimeSetUp]
        public virtual void OneTimeSetUp()
        {
            CreateKernel(true);
        }

        private void CreateKernel(bool createAsSingleTone)
        {
            if (_unitTestingKernel != null && _kernelCreatedAsSingleTone)
            {
                return;
            }

            _configDic = new Dictionary<string, string>();
            _unitTestingKernel = new TestingKernel<ConsoleLog>(AdditionalBindings(), _configDic);

            _kernelCreatedAsSingleTone = createAsSingleTone;
        }

        [TearDown]
        public virtual void TearDown()
        {
            if (!_kernelCreatedAsSingleTone)
            {
                _unitTestingKernel.Dispose();
            }
        }

        [OneTimeTearDown]
        public virtual void OneTimeTearDown()
        {
            if (_kernelCreatedAsSingleTone)
            {
                _unitTestingKernel.Dispose();
            }
        }

        protected abstract Action<IKernel> AdditionalBindings();

        protected async Task<T> ChangeConfig<T>(IEnumerable<KeyValuePair<string, string>> keyValue) where T : IConfigObject
        {
            foreach (KeyValuePair<string, string> keyValuePair in keyValue)
            {
                _configDic[keyValuePair.Key] = keyValuePair.Value;
            }

            return await _unitTestingKernel.Get<ManualConfigurationEvents>().ApplyChanges<T>();
        }

        protected async Task ClearChanges<T>() where T : IConfigObject
        {
            _configDic.Clear();

            await _unitTestingKernel.Get<ManualConfigurationEvents>().ApplyChanges<T>();
        }
    }
}
