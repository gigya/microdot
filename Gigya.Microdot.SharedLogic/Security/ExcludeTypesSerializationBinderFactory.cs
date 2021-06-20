using System.Collections.Generic;

namespace Gigya.Microdot.SharedLogic.Security
{
    public class ExcludeTypesSerializationBinderFactory : IExcludeTypesSerializationBinderFactory
    {
        private readonly Dictionary<string, ExcludeTypesSerializationBinder> _excludeTypesSerializationBinders = new Dictionary<string, ExcludeTypesSerializationBinder>();
        public ExcludeTypesSerializationBinder GetOrCreateExcludeTypesSerializationBinder(string commaSeparatedExcludeNames)
        {
            if (_excludeTypesSerializationBinders.TryGetValue(commaSeparatedExcludeNames,
                out var excludeTypesSerializationBinder) == false)
            {
                excludeTypesSerializationBinder = new ExcludeTypesSerializationBinder();
                excludeTypesSerializationBinder.ParseCommaSeparatedToExcludeTypes(commaSeparatedExcludeNames);
                _excludeTypesSerializationBinders.Add(commaSeparatedExcludeNames, excludeTypesSerializationBinder);
            }

            return excludeTypesSerializationBinder;
        }
    }
}
