using System;
using System.Threading.Tasks;
using Gigya.Microdot.SharedLogic.Rewrite;

namespace Gigya.Microdot.ServiceDiscovery.Rewrite
{
    public class ReachabilityCheck // replace with delegate?
    {
        private readonly Func<INode, Task<bool>> _check;

        public ReachabilityCheck(Func<INode, Task<bool>> check)
        {
            _check = check;
        }

        public Task<bool> Check(INode node) => _check(node);
    }
}