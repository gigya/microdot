using System;
using NUnit.Framework;
using Orleans;
using Orleans.Core;
using Orleans.Runtime;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice.WarmupTestService
{
    public class TestGrain : Grain, IGrainWithIntegerKey
    {
        public TestGrain(IGrainIdentity grainIdentity, IGrainRuntime graintRuntime, TestType t, int i) : base(grainIdentity, graintRuntime)
        {}
    }

    public class UsualGrain : Grain, IGrainWithIntegerKey
    {
        public UsualGrain(UsualType u)
        {}
    }

    public class TestType
    {
        public TestType()
        {
            Assert.Fail("Should not warm up grain constructor for testing");
        }
    }

    public class UsualType
    {
        public UsualType()
        {
            Console.WriteLine("UsualGrain is warmed as expected");
        }
    }
}
