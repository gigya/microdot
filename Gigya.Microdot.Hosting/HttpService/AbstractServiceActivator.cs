using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Gigya.Common.Contracts;

namespace Gigya.Microdot.Hosting.HttpService
{
    public abstract class AbstractServiceActivator : IActivator
    {
        public async Task<InvocationResult> Invoke(ServiceMethod serviceMethod, IDictionary args)
        {
            var arguments = GetParametersByName(serviceMethod, args);

            return await Invoke(serviceMethod, arguments).ConfigureAwait(false);
        }

        private static object[] GetParametersByName(ServiceMethod serviceMethod, IDictionary args)
        {
            return serviceMethod.ServiceInterfaceMethod
                .GetParameters()
                .Select(p => args[p.Name] ?? (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null))
                .ToArray();
        }

        public async Task<InvocationResult> Invoke(ServiceMethod serviceMethod, object[] arguments)
        {
            var invokeTarget = GetInvokeTarget(serviceMethod);

            ConvertArgumentTypes(serviceMethod.ServiceInterfaceMethod, arguments);

            var sw = Stopwatch.StartNew();
            var task = (Task)serviceMethod.ServiceInterfaceMethod.Invoke(invokeTarget, arguments);
            await task.ConfigureAwait(false);
            var executionTime = sw.Elapsed;
            var result = task.GetType().GetProperty("Result").GetValue(task);

            return new InvocationResult { Result = result, ExecutionTime = executionTime };
        }

        private static void ConvertArgumentTypes(MethodInfo method, object[] arguments)
        {
            var parameters = method.GetParameters();

            for (int i = 0; i < parameters.Length; i++)
                arguments[i] = JsonHelper.ConvertWeaklyTypedValue(arguments[i], parameters[i].ParameterType);
        }

        protected abstract object GetInvokeTarget(ServiceMethod serviceMethod);        
    }
}