using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Configuration.Objects;
using Gigya.Microdot.Hosting.Validators;
using Gigya.Microdot.Interfaces;
using Ninject;

namespace Gigya.Microdot.Ninject.SystemInitializer
{
    public abstract class SystemInitializerBase
    {
        protected IKernel _kernel;

        public SystemInitializerBase(IKernel kernel)
        {
            _kernel = kernel;
        }

        public void Init()
        {
            RunValidations();
            SearchAssembliesAndRebindIConfig();
        }

        private void RunValidations()
        {
            _kernel.Get<ServiceValidator>().Validate();
        }

        private void SearchAssembliesAndRebindIConfig()
        {
            IAssemblyProvider aProvider = _kernel.Get<IAssemblyProvider>();
            foreach (Assembly assembly in aProvider.GetAssemblies())
            {
                foreach (Type configType in assembly.GetTypes().Where(ConfigObjectCreator.IsConfigObject))
                {
                    IConfigObjectCreator configObjectCreator = _kernel.Get<Func<Type, IConfigObjectCreator>>()(configType);

                    dynamic getLatestLambda = configObjectCreator.GetLambdaOfGetLatest(configType);
                    _kernel.Rebind(typeof(Func<>).MakeGenericType(configType)).ToMethod(t => getLatestLambda());

                    Type sourceBlockType = typeof(ISourceBlock<>).MakeGenericType(configType);
                    _kernel.Rebind(sourceBlockType).ToMethod(t => configObjectCreator.ChangeNotifications);

                    dynamic changeNotificationsLambda = configObjectCreator.GetLambdaOfChangeNotifications(sourceBlockType);
                    _kernel.Rebind(typeof(Func<>).MakeGenericType(sourceBlockType)).ToMethod(t => changeNotificationsLambda());

                    _kernel.Rebind(configType).ToMethod(t => configObjectCreator.GetLatest());
                }
            }
        }
    }
}
