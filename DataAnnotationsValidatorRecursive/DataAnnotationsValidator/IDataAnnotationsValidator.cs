using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DataAnnotationsValidator
{
    public interface IDataAnnotationsValidator
    {
        bool TryValidateObject(object obj, ICollection<ValidationResult> results, IDictionary<object, object> validationContextItems = null);
        bool TryValidateObjectRecursive<T>(T obj, List<ValidationResult> results, IDictionary<object, object> validationContextItems = null);
    }
}
