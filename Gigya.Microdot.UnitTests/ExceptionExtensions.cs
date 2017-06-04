using System;

namespace Gigya.Microdot.UnitTests
{
    public static class ExceptionExtensions
    {
        public static T ThrowAndCatch<T>(this T exception) where T : Exception
        {
            try
            {
                throw exception;
            }
            catch (T ex)
            {
                return ex;
            }
        }
    }
}