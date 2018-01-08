using System.Threading.Tasks;
using Orleans;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.CalculatorService
{
    public interface IUserGrainWithGuid : IIdentety, IGrainWithGuidKey { }
    public interface IUserGrainWithString : IIdentety, IGrainWithStringKey { }
    public interface IUserGrainWithLong : IIdentety, IGrainWithIntegerKey { }

    public interface IIdentety
    {
      Task<string> GetIdentety();
    }


    public class UserGrainWithGuid : Grain, IUserGrainWithGuid
    {
        public Task<string>  GetIdentety()
        {
            return Task.FromResult(this.GetPrimaryKey().ToString());
        }
    }
    public class UserGrainWithString : Grain, IUserGrainWithString
    {
        public Task<string>  GetIdentety()
        {
            return Task.FromResult(this.GetPrimaryKeyString());
        }
    }

    public class UserGrainWithLong : Grain, IUserGrainWithLong
    {
        public Task<string>  GetIdentety()
        {
            return Task.FromResult(this.GetPrimaryKeyLong().ToString());
        }
    }


}