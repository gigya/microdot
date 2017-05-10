using System;

using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.SharedLogic.Utils
{

    public static class Extensions
    {

        public static string RawMessage(this Exception ex) => (ex as SerializableException)?.RawMessage ?? ex.Message;


        /// <summary>
        /// Returns a function that, when called, checks if the instance of the object you're calling this on was
        /// garbage collected or not (only a weak reference to the object is held, so it can be garbage collected). If
        /// it wasn't collected, the function call your lambda, passing it the object, and returns you lambda's result.
        /// Otherwise, the function returns the default value of your lambda's return type.
        /// </summary>
        internal static Func<V> IfNotGarbageCollected<T, V>(this T instance, Func<T, V> getter) where T:class
        {
            var weakRef = new WeakReference<T>(instance);
            return () => {
                T inst;
                if (weakRef.TryGetTarget(out inst))
                    return getter(inst);
                else
                    return default(V);
            };
        } 
    }
}
