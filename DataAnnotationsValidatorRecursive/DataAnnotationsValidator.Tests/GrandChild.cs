using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DataAnnotationsValidator.Tests
{
    public class GrandChild : IValidatableObject
    {
        [Required]
        [Range(0, 10, ErrorMessage = "GrandChild PropertyA not within range")]
        public int? PropertyA { get; set; }

        [Required]
        [Range(0, 10, ErrorMessage = "GrandChild PropertyB not within range")]
        public int? PropertyB { get; set; }

        [SaveValidationContext]
        public bool HasNoRealValidation { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (PropertyA.HasValue && PropertyB.HasValue && (PropertyA + PropertyB > 10))
                yield return new ValidationResult("GrandChild PropertyA and PropertyB cannot add up to more than 10");
        }
    }
}
