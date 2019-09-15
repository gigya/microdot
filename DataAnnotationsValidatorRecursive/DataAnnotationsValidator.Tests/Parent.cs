using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DataAnnotationsValidator.Tests
{
    public class Parent : IValidatableObject
    {
        [Required(ErrorMessage = "Parent PropertyA is required")]
        [Range(0, 10, ErrorMessage = "Parent PropertyA not within range")]
        public int? PropertyA { get; set; }

        [Required(ErrorMessage = "Parent PropertyB is required")]
        [Range(0, 10, ErrorMessage = "Parent PropertyB not within range")]
        public int? PropertyB { get; set; }

        public Child Child { get; set; }

        [SkipRecursiveValidation]
        public Child SkippedChild { get; set; }

        [SaveValidationContext]
        public bool HasNoRealValidation { get; set; }

        IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            if (PropertyA.HasValue && PropertyB.HasValue && (PropertyA + PropertyB > 10))
                yield return new ValidationResult("Parent PropertyA and PropertyB cannot add up to more than 10");
        }
    }
}
