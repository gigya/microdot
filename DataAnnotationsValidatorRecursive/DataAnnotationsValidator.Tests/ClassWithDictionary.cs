using System.Collections.Generic;

namespace DataAnnotationsValidator.Tests
{
    public class ClassWithDictionary
    {
        public List<Dictionary<string, Child>> Objects { get; set; }
    }
}