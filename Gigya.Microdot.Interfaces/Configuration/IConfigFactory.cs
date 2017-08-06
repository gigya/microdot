using System;

namespace Gigya.Microdot.Interfaces.Configuration
{
    public interface IConfigFactory
    {
        Func<T> CreateConfig<T>() where T : IConfigObject;
    }
}