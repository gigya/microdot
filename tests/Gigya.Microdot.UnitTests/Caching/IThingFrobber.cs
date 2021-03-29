using System.Collections.Generic;
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

    public class ThingFrobber : IThingFrobber
    {
        public int DelayInMili { get; }

        public ThingFrobber(int delayInMili, List<Revocable<Thing>> thingifyTaskRevokableResults = null)
        {
            DelayInMili = delayInMili;

            if (thingifyTaskRevokableResults != null)
                ThingifyTaskRevokableResults = thingifyTaskRevokableResults.ToArray();
        }

        public int ThingifyInt(string s)
        {
            throw new System.NotImplementedException();
        }

        public Thing ThingifyThing(string s)
        {
            throw new System.NotImplementedException();
        }

        public Task ThingifyTask(string s)
        {
            throw new System.NotImplementedException();
        }

        public void ThingifyVoid(string s)
        {
            throw new System.NotImplementedException();
        }

        public Task<Thing> ThingifyTaskThing(string s)
        {
            throw new System.NotImplementedException();
        }

        public Task<int> ThingifyTaskInt(string s)
        {
            throw new System.NotImplementedException();
        }

        private int ThingifyTaskRevokableResultsIndex = 0;
        private Revocable<Thing>[] ThingifyTaskRevokableResults;
        public async Task<Revocable<Thing>> ThingifyTaskRevokable(string s)
        {
            await Task.Delay(DelayInMili);

            var result = ThingifyTaskRevokableResults[ThingifyTaskRevokableResultsIndex];
            ThingifyTaskRevokableResultsIndex++;

            return result;
        }
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