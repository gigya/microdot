using System;
using System.Collections.Generic;
using System.Text;

namespace Gigya.Microdot.LanguageExtensions
{
    public static class MiscExtensions
    {
        public static bool TryDispose(this IDisposable disposable)
        {
            if (disposable == null)
                return false;

            try
            {
                disposable.Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns a function that, when called, checks if the instance of the object you're calling this on was
        /// garbage collected or not (only a weak reference to the object is held, so it can be garbage collected). If
        /// it wasn't collected, the function call your lambda, passing it the object, and returns you lambda's result.
        /// Otherwise, the function returns the default value of your lambda's return type.
        /// </summary>
        // TODO: Isn't used by Microdot, should be moved to Infra
        internal static Func<V> IfNotGarbageCollected<T, V>(this T instance, Func<T, V> getter) where T : class
        {
            var weakRef = new WeakReference<T>(instance);
            return () =>
            {
                if (weakRef.TryGetTarget(out T inst))
                    return getter(inst);
                else
                    return default(V);
            };
        }

        public static U To<T, U>(this T obj, Func<T, U> fn) => fn(obj);
    }
}
