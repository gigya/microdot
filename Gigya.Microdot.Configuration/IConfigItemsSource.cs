using System.Threading.Tasks;

namespace Gigya.Microdot.Configuration
{
    public interface IConfigItemsSource
    {
        Task<ConfigItemsCollection> GetConfiguration();
    }
}