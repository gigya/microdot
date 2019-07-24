using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

using Gigya.Common.Contracts.Attributes;
using Gigya.ServiceContract.HttpService;

namespace Gigya.Microdot.UnitTests.Caching
{
    public interface IThingFrobber
    {
        int ThingifyInt(string s);
        Thing ThingifyThing(string s);
        Task ThingifyTask(string s);
        void ThingifyVoid(string s);

        [Cached] Task<Thing> ThingifyTaskThing(string s);
        [Cached] Task<int> ThingifyTaskInt(string s);

        [Cached] Task<Revocable<Thing>> ThingifyTaskRevokable(string s);
    }


    public class Thing
    {
        [Key]
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class NonRevokableThing
    {
        public long Id { get; set; }
        public string Name { get; set; }
    }

}