using Gigya.ServiceContract.Attributes;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.MockData
{
    public class PersonMock
    {
        public int ID { get; set; } = 100;

        [NonSensitive]
        public string Name { get; set; } = "Eli";

        [Sensitive(Secretive = false)]
        public string Gender { get; set; } = "Man";

        [Sensitive(Secretive = true)]
        public string Password { get; set; } = "password";

        public InnerCarMockClass InnerCarMockClass { get; set; } = new InnerCarMockClass();
    }


    public class InnerCarMockClass
    {
        [NonSensitive] public int Year { get; set; } = 100;

        [NonSensitive] public string LisencePlates { get; set; } = "11 -222-33";
    }




}
