using System;
using System.Threading.Tasks;

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

        public static T ThrowAndCatchAsync<T>(this T exception) where T : Exception
        {
            async Task AsyncThrow()
            {
                await Task.FromResult(0);
                throw exception;
            }

            try
            {
                AsyncThrow().GetAwaiter().GetResult();
            }
            catch (T ex)
            {
                return ex;
            }

            return null;
        }
    }
}