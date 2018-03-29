namespace Gigya.Microdot.Hosting.Validators
{
    public class ServiceValidator
    {
        private readonly IValidator[] _validators;

        public ServiceValidator(IValidator[] validators)
        {
            _validators = validators;
        }

        public void Validate()
        {
            foreach (var validator in _validators)
            {
                validator.Validate();
            }
        }
    }
}