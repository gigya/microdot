using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Interfaces;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Hosting.Validators
{
    public class ConfigObjectTypeValidator : IValidator
    {
        private IAssemblyProvider _assemblyProvider;

        public ConfigObjectTypeValidator(IAssemblyProvider assemblyProvider)
        {
            _assemblyProvider = assemblyProvider;
        }
        public void Validate()
        {
            List<Type> configValueTypes = _assemblyProvider.GetAllTypes().Where(t => t.GetTypeInfo().ImplementedInterfaces.Any(i => i == typeof(IConfigObject) && 
                                                                                                                                           t.GetTypeInfo().IsValueType)).ToList();

            if (configValueTypes.Count > 0)
            {
                throw new ProgrammaticException(
                    $"The type/s {string.Join(", ", configValueTypes.Select(t => t.Name))} are value types abd cannot implement IConfigObject interfaces");
            }
        }
    }
}
