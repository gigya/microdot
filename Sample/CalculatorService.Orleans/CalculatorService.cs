﻿using CalculatorService.Interface;
using Gigya.ServiceContract.HttpService;
using Orleans;
using Orleans.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace CalculatorService.Orleans
{

    public interface ICalculatorServiceGrain : ICalculatorService, IGrainWithIntegerKey
    {
    }

    [StatelessWorker, Reentrant]
    public class CalculatorService : Grain, ICalculatorServiceGrain
    {
        public CalculatorService()
        {
         
        }
        public Task<string> Add(int a, int b)
        {
            return Task.FromResult((a + b).ToString());
        }

        public Task<string> Add_Cached(int a, int b)
        {
            return Task.FromResult((a + b).ToString());
        }

        private int addCachedVersion = 0;
        public Task<string> Add_Cached_WithNewValueAfterServiceCall(int a, int b)
        {
            var version = Interlocked.Increment(ref addCachedVersion);
            return Task.FromResult($"{a + b}_v{version}");
        }

        public Task<Revocable<string>> Add_CachedAndRevocable(int a, int b)
        {
            return Task.FromResult(new Revocable<string>
            {
                Value = (a + b).ToString(),
                RevokeKeys = new[] { "a", "b" }
            });
        }

        private int addCachedAndRevocableVersion = 0;
        public Task<Revocable<string>> Add_CachedAndRevocable_WithNewValueAfterServiceCall(int a, int b)
        {
            var version = Interlocked.Increment(ref addCachedAndRevocableVersion);
            return Task.FromResult(new Revocable<string>
            {
                Value = $"{a + b}_v{version}",
                RevokeKeys = new[] { "a", "b" }
            });
        }
    }
}
