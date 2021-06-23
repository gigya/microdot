namespace Gigya.Microdot.SharedLogic.Security
{
    public interface IExcludeTypesSerializationBinderFactory
    {
        ExcludeTypesSerializationBinder GetOrCreateExcludeTypesSerializationBinder(string commaSeparatedExcludeNames);
    }
}
