using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DataAnnotationsValidator.Tests
{
    public class SaveValidationContextAttribute: ValidationAttribute
    {
        public static IList<ValidationContext> SavedContexts = new List<ValidationContext>();

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            SavedContexts.Add(validationContext);
            return ValidationResult.Success;
        }
    }
}