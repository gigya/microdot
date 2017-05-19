using System;
using System.IO;

using Gigya.Common.Contracts.Exceptions;

namespace Gigya.Microdot.SharedLogic.Utils
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns true if <paramref name="path"/> starts with the path <paramref name="baseDirPath"/>.
        /// The comparison is case-insensitive, handles / and \ slashes as folder separators and
        /// only matches if the base dir folder name is matched exactly ("c:\foobar\file.txt" is not a sub path of "c:\foo").
        /// </summary>
        public static bool IsSubPathOf(this string path, string baseDirPath)
        {
            string normalizedPath = Path.GetFullPath(path.Replace('/', '\\')
                                                         .WithEnding("\\"));

            string normalizedBaseDirPath = Path.GetFullPath(baseDirPath.Replace('/', '\\')
                                                                       .WithEnding("\\"));

            return normalizedPath.StartsWith(normalizedBaseDirPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns <paramref name="str"/> with the minimal concatenation of <paramref name="ending"/> (starting from end) that
        /// results in satisfying .EndsWith(ending).
        /// </summary>
        /// <example>"hel".WithEnding("llo") returns "hello", which is the result of "hel" + "lo".</example>
        public static string WithEnding(this string str, string ending)
        {
            if (str == null)
                return ending;

            string result = str;

            // Right() is 1-indexed, so include these cases
            // * Append no characters
            // * Append up to N characters, where N is ending length
            for (int i = 0; i <= ending.Length; i++)
            {
                string tmp = result + ending.Right(i);
                if (tmp.EndsWith(ending))
                    return tmp;
            }

            return result;
        }

        /// <summary>Gets the rightmost <paramref name="length" /> characters from a string.</summary>
        /// <param name="value">The string to retrieve the substring from.</param>
        /// <param name="length">The number of characters to retrieve.</param>
        /// <returns>The substring.</returns>
        public static string Right(this string value, int length)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "Length is less than zero");
            }

            return length < value.Length ? value.Substring(value.Length - length) : value;
        }
    }


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
