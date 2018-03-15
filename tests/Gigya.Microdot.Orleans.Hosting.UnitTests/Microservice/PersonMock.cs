using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice
{
    public class PersonMock
    {
        public int ID { get; set; } = 100;

        [NonSensitive]
        public string Name { get; set; } = "Eli";

        [Sensitive(Secretive = false)]
        public bool IsMale { get; set; } = true;

        [Sensitive(Secretive = true)]
        public string Password { get; set; } = "password";
    }
}
