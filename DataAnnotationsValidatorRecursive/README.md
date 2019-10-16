# DataAnnotationsValidatorRecursive

The helper will recursively traverse your object graph and invoke validation against DataAnnotations.
This originated from following Stackoverflow answer: http://stackoverflow.com/a/8090614/605586

## Installation

Available as NuGet-Package `dataannotationsvalidator`:

    Install-Package dataannotationsvalidator

## Usage

See file `DataAnnotationsValidator/DataAnnotationsValidator.Tests/DataAnnotationsValidatorTests.cs`

Short example:

    var validator = new DataAnnotationsValidator.DataAnnotationsValidator();
    var validationResults = new List<ValidationResult>();

    validator.TryValidateObjectRecursive(modelToValidate, validationResults);
