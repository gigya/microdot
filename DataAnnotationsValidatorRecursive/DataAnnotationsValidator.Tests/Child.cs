using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DataAnnotationsValidator.Tests
{
    public class Child : IValidatableObject
    {
        [Required(ErrorMessage = "Child Parent is required")]
        public Parent Parent { get; set; }

        [Required(ErrorMessage = "Child PropertyA is required")]
        [Range(0, 10, ErrorMessage = "Child PropertyA not within range")]
        public int? PropertyA { get; set; }

        [Required(ErrorMessage = "Child PropertyB is required")]
        [Range(0, 10, ErrorMessage = "Child PropertyB not within range")]
        public int? PropertyB { get; set; }

        public IEnumerable<GrandChild> GrandChildren { get; set; }

        [SaveValidationContext]
        public bool HasNoRealValidation { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (PropertyA.HasValue && PropertyB.HasValue && (PropertyA + PropertyB > 10))
                yield return new ValidationResult("Child PropertyA and PropertyB cannot add up to more than 10");
        }
    }
}
