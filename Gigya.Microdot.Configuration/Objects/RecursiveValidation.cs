using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Gigya.Microdot.Configuration.Objects
{
    public interface IDataAnnotationsValidator
    {
        bool TryValidateObject(object obj, ICollection<ValidationResult> results, IDictionary<object, object> validationContextItems = null);
        bool TryValidateObjectRecursive<T>(T obj, List<ValidationResult> results, IDictionary<object, object> validationContextItems = null);
    }

    public class DataAnnotationsValidator : IDataAnnotationsValidator
    {
        private const string JObject = "Newtonsoft.Json.Linq.JObject";
        private static readonly HashSet<string> IgnoredTypeNames = new HashSet<string>{ JObject };

        private bool ShouldIgnoreType(Type type)
        {
            return IgnoredTypeNames.Contains(type.FullName);
        }

        public bool TryValidateObject(object obj, ICollection<ValidationResult> results, IDictionary<object, object> validationContextItems = null)
        {
            if (ShouldIgnoreType(obj.GetType()))
                return true;
            return Validator.TryValidateObject(obj, new ValidationContext(obj, null, validationContextItems), results, true);
        }

        public bool TryValidateObjectRecursive<T>(T obj, List<ValidationResult> results, IDictionary<object, object> validationContextItems = null)
        {
            return TryValidateObjectRecursive(obj, results, new HashSet<object>(), validationContextItems, 500);
        }

        //max recursion is hard code to 100, 
        private bool TryValidateObjectRecursive<T>(T obj, ICollection<ValidationResult> results, ISet<object> validatedObjects, IDictionary<object, object> validationContextItems = null, int recursionLevels = 100)
        {
            //short-circuit to avoid infinit loops on cyclical object graphs
            if (validatedObjects.Contains(obj))
            {
                return true;
            }

            var remainingRecursionLevels = --recursionLevels;
            //TODO: consider enlarging or making the max recursion level lower as needed
            if (remainingRecursionLevels <= 0)
                throw new InvalidOperationException("There is something wrong with the validated object, it is too deep!");

            validatedObjects.Add(obj);
            bool result = TryValidateObject(obj, results, validationContextItems);

            //prevent static fields from being picked up for the 
            var properties = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(
                prop => prop.CanRead
                && !prop.GetCustomAttributes(typeof(SkipRecursiveValidation), false).Any()
                && prop.GetIndexParameters().Length == 0).ToList();

            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(string) || property.PropertyType.IsValueType || ShouldIgnoreType(property.PropertyType)) continue;

                object value = null;
                try
                {
                    value = obj.GetPropertyValue(property.Name);
                }
                catch
                {
                    //Getting a property shouldn't throw if it does it is probably NotImplementedException, to be safe we ignore all exceptions.
                    continue;
                }

                List<ValidationResult> nestedResults;
                switch (value)
                {
                    case null:
                        continue;
                    case IEnumerable asEnumerable:
                        foreach (var enumObj in asEnumerable)
                        {
                            if (enumObj == null) continue;
                            nestedResults = new List<ValidationResult>();
                            if (!TryValidateObjectRecursive(enumObj, nestedResults, validatedObjects, validationContextItems, remainingRecursionLevels))
                            {
                                result = false;
                                foreach (var validationResult in nestedResults)
                                {
                                    var property1 = property;
                                    results.Add(new ValidationResult(validationResult.ErrorMessage, validationResult.MemberNames.Select(x => property1.Name + '.' + x)));
                                }
                            }
                        }

                        break;
                    default:
                        nestedResults = new List<ValidationResult>();
                        if (!TryValidateObjectRecursive(value, nestedResults, validatedObjects, validationContextItems, remainingRecursionLevels))
                        {
                            result = false;
                            foreach (var validationResult in nestedResults)
                            {
                                var property1 = property;
                                results.Add(new ValidationResult(validationResult.ErrorMessage, validationResult.MemberNames.Select(x => property1.Name + '.' + x)));
                            }
                        }
                        break;
                }
            }

            return result;
        }
    }

    public class SkipRecursiveValidation : Attribute
    {
    }

    public static class ObjectExtensions
    {
        public static object GetPropertyValue(this object o, string propertyName)
        {
            object objValue = string.Empty;

            var propertyInfo = o.GetType().GetProperty(propertyName);
            if (propertyInfo != null)
                objValue = propertyInfo.GetValue(o, null);

            return objValue;
        }
    }
}
