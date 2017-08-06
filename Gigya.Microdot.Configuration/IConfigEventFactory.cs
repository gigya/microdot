using System.Threading.Tasks.Dataflow;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.Configuration
{
    public interface IConfigEventFactory
    {
        ISourceBlock<T> GetChangeEvent<T>() where T: IConfigObject;
    }
 }