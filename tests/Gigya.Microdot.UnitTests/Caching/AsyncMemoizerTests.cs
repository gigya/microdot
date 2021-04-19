using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Gigya.Common.Contracts.Attributes;
using Gigya.Common.Contracts.Exceptions;
using Gigya.Microdot.Fakes;
using Gigya.Microdot.ServiceDiscovery.Config;
using Gigya.Microdot.ServiceProxy.Caching;
using Gigya.Microdot.SharedLogic.Events;
using Gigya.Microdot.SharedLogic.SystemWrappers;
using Metrics;

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

using Shouldly;

// ReSharper disable ConsiderUsingConfigureAwait (not relevant for tests)
namespace Gigya.Microdot.UnitTests.Caching
{
    // Calls to NSubstitute's .Received() method on async methods generate this warning.


    [TestFixture,Parallelizable(ParallelScope.Fixtures)]
    public class AsyncMemoizerTests
    {
        private MethodInfo ThingifyInt { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyInt));
        private MethodInfo ThingifyThing { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyThing));
        private MethodInfo ThingifyTask { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyTask));
        private MethodInfo ThingifyTaskThing { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyTaskThing));

        private MethodInfo ThingifyTaskInt { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyTaskInt));
        private MethodInfo ThingifyVoidMethod { get; } = typeof(IThingFrobber).GetMethod(nameof(IThingFrobber.ThingifyVoid));
        private IThingFrobber EmptyThingFrobber { get; } = Substitute.For<IThingFrobber>();

        private IMethodCachingSettings GetCachingSettings(double expirationTime = 60 * 60 * 6, 
                                                          double refreshTimeSeconds = 60, 
                                                          double failedRefreshDelaySeconds = 1,
                                                          RequestGroupingBehavior groupingBehavior = RequestGroupingBehavior.Enabled,
                                                          RefreshMode refreshMode = RefreshMode.UseRefreshes,
                                                          CacheResponsesWhenSupressedBehavior cacheResponsesWhenSupressedBehavior = CacheResponsesWhenSupressedBehavior.Enabled,
                                                          RefreshBehavior refreshBehavior = RefreshBehavior.UseOldAndFetchNewValueInBackground,
                                                          ResponseKinds responseKindsToCache = ResponseKinds.NonNullResponse | ResponseKinds.NullResponse,
                                                          ResponseKinds responseKindsToIgnore = ResponseKinds.EnvironmentException | ResponseKinds.OtherExceptions | ResponseKinds.RequestException | ResponseKinds.TimeoutException,
                                                          NotIgnoredResponseBehavior notIgnoredResponseBehavior = NotIgnoredResponseBehavior.KeepCachedResponse)
            => new MethodCachingPolicyConfig
            {
                ExpirationTime = TimeSpan.FromSeconds(expirationTime),
                RefreshTime = TimeSpan.FromSeconds(refreshTimeSeconds),
                FailedRefreshDelay = TimeSpan.FromSeconds(failedRefreshDelaySeconds),

                Enabled = true,
                ResponseKindsToCache = responseKindsToCache,
                ResponseKindsToIgnore = responseKindsToIgnore,
                RequestGroupingBehavior = groupingBehavior,
                RefreshBehavior = refreshBehavior,
                RevokedResponseBehavior = RevokedResponseBehavior.FetchNewValueNextTime,  //Tested in AsyncMemoizerRevokesTests
                CacheResponsesWhenSupressedBehavior = cacheResponsesWhenSupressedBehavior,
                RefreshMode = refreshMode,
                ExpirationBehavior = ExpirationBehavior.DoNotExtendExpirationWhenReadFromCache, //Tested in CachingProxyTests
                NotIgnoredResponseBehavior = notIgnoredResponseBehavior
            };


        private DateTimeFake TimeFake { get; set; } = new DateTimeFake { UtcNow = DateTime.UtcNow };

        private AsyncCache CreateCache(ISourceBlock<string> revokeSource = null)
        {
            var revokeListener=new EmptyRevokeListener();
            if(revokeSource != null)
                revokeListener.RevokeSource = revokeSource;

            var revokeCache = Substitute.For<IRecentRevokesCache>();
            revokeCache.TryGetRecentlyRevokedTime(Arg.Any<string>(), Arg.Any<DateTime>()).Returns((DateTime?)null);

            return new AsyncCache(new ConsoleLog(), Metric.Context("AsyncCache"), TimeFake, revokeListener, revokeCache);
        }
        
        private IMemoizer CreateMemoizer(AsyncCache cache)
        {
            var metadataProvider = new MetadataProvider();
            return new AsyncMemoizer(cache, metadataProvider, Metric.Context("Tests"));
        }

        private IThingFrobber CreateDataSource(params object[] results)
        {
            var dataSource = Substitute.For<IThingFrobber>();
          
            var taskResults = new List<Task<Thing>>();

            if (results == null)
                results = new object[] { null };

            foreach (var result in results)
            {
                if (result == null)
                    taskResults.Add(Task.FromResult(default(Thing)));
                else if (result is string)
                    taskResults.Add(Task.FromResult(new Thing { Id = (string)result }));
                else if (result is int)
                    taskResults.Add(Task.FromResult(new Thing { Id = ((int)result).ToString() }));
                else if (result is TaskCompletionSource<Thing>)
                    taskResults.Add(((TaskCompletionSource<Thing>)result).Task);
                else
                    throw new ArgumentException();
            }

            if (results.Any())
                dataSource.ThingifyTaskThing("someString").Returns(taskResults.First(), taskResults.Skip(1).ToArray());
            
            var intResults = results.OfType<int>().ToArray();

            if (intResults.Any())
                dataSource.ThingifyTaskInt("someString").Returns(intResults.First(), intResults.Skip(1).ToArray());

            return dataSource;
        }

        private IThingFrobber CreateDataSourceByResponseKind(ResponseKinds responseKind)
        {
            var dataSource = Substitute.For<IThingFrobber>();
            dataSource.ThingifyTaskThing("someString").Returns<Task<Thing>>(async i =>
            {
                switch (responseKind)
                {
                    case ResponseKinds.NonNullResponse:
                        return new Thing { Id = "response" };
                    case ResponseKinds.NullResponse:
                        return null;
                    case ResponseKinds.RequestException:
                        throw new RequestException("ex");
                    case ResponseKinds.EnvironmentException:
                        throw new EnvironmentException("ex");
                    case ResponseKinds.TimeoutException:
                        throw new TimeoutException("ex");
                    case ResponseKinds.OtherExceptions:
                        throw new Exception("ex");
                    default:
                        throw new ArgumentOutOfRangeException(nameof(responseKind), responseKind, null);
                }
            });

            return dataSource;
        }

        private async Task ExecuteCallAndVerifyResult(ResponseKinds responseKind, Func<Task<Thing>> func)
        {
            Exception exceptionResult = null;
            Thing nonExceptionResult = null;

            try
            {
                nonExceptionResult = await func();
            }
            catch (Exception e)
            {
                exceptionResult = e;
            }

            switch (responseKind)
            {
                case ResponseKinds.NonNullResponse:
                    nonExceptionResult.Id.ShouldNotBeNullOrEmpty();
                    break;
                case ResponseKinds.NullResponse:
                    nonExceptionResult.ShouldBeNull();
                    break;
                case ResponseKinds.RequestException:
                    exceptionResult.GetType().ShouldBe(typeof(RequestException));
                    break;
                case ResponseKinds.EnvironmentException:
                    exceptionResult.GetType().ShouldBe(typeof(EnvironmentException));
                    break;
                case ResponseKinds.TimeoutException:
                    exceptionResult.GetType().ShouldBe(typeof(TimeoutException));
                    break;
                case ResponseKinds.OtherExceptions:
                    exceptionResult.GetType().ShouldBe(typeof(Exception));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(responseKind), responseKind, null);
            }
        }

        [Test]
        public async Task MemoizeAsync_FirstCall_UsesDataSource()
        {
            string firstValue = "first Value";
            var dataSource = CreateDataSource(firstValue);
            
            var actual = await (Task<Thing>)CreateMemoizer(CreateCache()).Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings());
            
            actual.Id.ShouldBe(firstValue);
            dataSource.Received(1).ThingifyTaskThing("someString");
        }

        [Test]
        [TestCase(ResponseKinds.NonNullResponse)]
        [TestCase(ResponseKinds.NullResponse)]
        [TestCase(ResponseKinds.EnvironmentException)]
        [TestCase(ResponseKinds.TimeoutException)]
        [TestCase(ResponseKinds.RequestException)]
        [TestCase(ResponseKinds.OtherExceptions)]
        public async Task MemoizeAsync_ResponseIsCachedAccordingToResponseKinds(ResponseKinds responseKind)
        {
            var dataSource = CreateDataSourceByResponseKind(responseKind);
            var memoizer = CreateMemoizer(CreateCache());

            //Call service and cache results
            await ExecuteCallAndVerifyResult(responseKind, () => (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: responseKind, responseKindsToIgnore: 0)));

            //Use cached results
            await ExecuteCallAndVerifyResult(responseKind, () => (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: responseKind, responseKindsToIgnore: 0)));

            //Only one call to data source!
            dataSource.Received(1).ThingifyTaskThing("someString");
        }

        [Test]
        [TestCase(ResponseKinds.NonNullResponse)]
        [TestCase(ResponseKinds.NullResponse)]
        [TestCase(ResponseKinds.EnvironmentException)]
        [TestCase(ResponseKinds.TimeoutException)]
        [TestCase(ResponseKinds.RequestException)]
        [TestCase(ResponseKinds.OtherExceptions)]
        public async Task MemoizeAsync_ResponseIsNotCachedAccordingToResponseKinds(ResponseKinds responseKind)
        {
            var dataSource = CreateDataSourceByResponseKind(responseKind);
            var memoizer = CreateMemoizer(CreateCache());
            ResponseKinds responseKindsToCache = 0; //Do not cache any response

            //Call service - results should not be cached (according to responseKindsToCache)
            await ExecuteCallAndVerifyResult(responseKind, () => (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: responseKindsToCache, responseKindsToIgnore: 0)));

            //Should trigger another call to service
            await ExecuteCallAndVerifyResult(responseKind, () => (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: responseKindsToCache, responseKindsToIgnore: 0)));

            //Two calls to data source!
            dataSource.Received(2).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_DataSourceCallReturnsAnIgnoredResponseKind_DataSourceResponseIsIgnoredAndCachedValueIsReturned()
        {
            var result1 = "result1";
            var dataSource = CreateDataSource(result1, null);
            var memoizer = CreateMemoizer(CreateCache());

            //Call service and cache result
            var result = await (Task<Thing>) memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] {"someString"},                  
                GetCachingSettings(responseKindsToCache: ResponseKinds.NonNullResponse, responseKindsToIgnore: ResponseKinds.NullResponse,
                                   refreshTimeSeconds:0)); //will trigger a refresh in next call
            result.Id.ShouldBe(result1);

            //This call to the service will return a response (null) which we want to ignore, and return cached value
            result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: ResponseKinds.NonNullResponse, responseKindsToIgnore: ResponseKinds.NullResponse,
                                   refreshBehavior: RefreshBehavior.TryFetchNewValueOrUseOld)); //so we wont run as a backround refresh
            result.Id.ShouldBe(result1);

            //Verify null response was not cached
            result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: ResponseKinds.NonNullResponse, responseKindsToIgnore: ResponseKinds.NullResponse));
            result.Id.ShouldBe(result1);

            dataSource.Received(2).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_DataSourceCallReturnsAnIgnoredResponseKindAndNoCachedValue_DataSourceResponseIsReturnedAndNotCached()
        {
            var secondResult = "second result";
            var dataSource = CreateDataSource(null, secondResult);
            var memoizer = CreateMemoizer(CreateCache());

            //Call service - null result will not be cached (responseKindsToIgnore: ResponseKinds.NullResponse)
            var result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },               
                GetCachingSettings(responseKindsToCache: ResponseKinds.NonNullResponse, responseKindsToIgnore: ResponseKinds.NullResponse));
            result.ShouldBeNull();

            //Call service and cache the valid result (secondResult)
            result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },                  
                GetCachingSettings(responseKindsToCache: ResponseKinds.NonNullResponse, responseKindsToIgnore: ResponseKinds.NullResponse));
            result.Id.ShouldBe(secondResult);

            //Use cached value
            result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: ResponseKinds.NonNullResponse, responseKindsToIgnore: ResponseKinds.NullResponse));
            result.Id.ShouldBe(secondResult);

            dataSource.Received(2).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_DataSourceCallReturnsANoneCachedAndNoneIgnoreResponseKindAndNotIgnoredResponseBehaviorIsRemoveCachedResponse_ReturnResponseAndRemovePrevCachedValue()
        {
            var firstResult = "first result";
            var secondResult = "second result";
            var dataSource = CreateDataSource(firstResult, null, secondResult);
            var memoizer = CreateMemoizer(CreateCache());

            //Call service and cache result
            var result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: ResponseKinds.NonNullResponse, refreshTimeSeconds: 0)); //trigger a refresh in next call
            result.Id.ShouldBe(firstResult);

            //Call service and get a null result, return it and remove previously-cached value
            result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(notIgnoredResponseBehavior: NotIgnoredResponseBehavior.RemoveCachedResponse,
                                   responseKindsToCache: ResponseKinds.NonNullResponse, refreshBehavior:RefreshBehavior.TryFetchNewValueOrUseOld));
            result.ShouldBeNull();

            //This will trigger a call to the service because prev call removed the item from the cache
            result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: ResponseKinds.NonNullResponse, responseKindsToIgnore: ResponseKinds.NullResponse));
            result.Id.ShouldBe(secondResult);

            dataSource.Received(3).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_DataSourceCallReturnsANoneCachedAndNoneIgnoreResponseKindAndNotIgnoredResponseBehaviorIsKeepCachedResponse_ReturnResponseAndKeepPrevCachedValue()
        {
            var firstResult = "first result";
            var dataSource = CreateDataSource(firstResult, null);
            var memoizer = CreateMemoizer(CreateCache());

            //Call service and cache result
            var result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: ResponseKinds.NonNullResponse, refreshTimeSeconds: 0)); //trigger a refresh in next call
            result.Id.ShouldBe(firstResult);

            //Call service and get a null result, return it and keep previously-cached value
            result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(notIgnoredResponseBehavior: NotIgnoredResponseBehavior.KeepCachedResponse,
                    responseKindsToCache: ResponseKinds.NonNullResponse, refreshBehavior: RefreshBehavior.TryFetchNewValueOrUseOld));
            result.ShouldBeNull();

            //This will not trigger a call to the service, because prev call did not remove the item from the cache
            //Returns previously cached value
            result = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(responseKindsToCache: ResponseKinds.NonNullResponse, responseKindsToIgnore: ResponseKinds.NullResponse));
            result.Id.ShouldBe(firstResult);

            dataSource.Received(2).ThingifyTaskThing("someString");
        }

        [Test]
        public void MemoizeAsync_CallFailsWhenNotAlreadyCached_Throws()
        {
            var failedTask = new TaskCompletionSource<Thing>();
            var dataSource = CreateDataSource(failedTask);

            failedTask.SetException(new Exception("Boo!!"));
            Should.Throw<Exception>(() => (Task)CreateMemoizer(CreateCache()).Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings()));
        }

        [Test]
        public async Task MemoizeAsync_MultipleCalls_UsesDataSourceOnlyOnce()
        {
            string firstValue = "first Value";
            var dataSource = CreateDataSource(firstValue);
            var memoizer = CreateMemoizer(CreateCache());

            for (int i = 0; i < 100; i++)
            {
                var actual = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings());
                actual.Id.ShouldBe(firstValue);
            }

            dataSource.Received(1).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_MultipleCallsWithSuppressCaching_UsesDataSourceForEverySuppressedCacheCall()
        {
            string firstValue = "first Value";
            var dataSource = CreateDataSource(firstValue);
            var memoizer = CreateMemoizer(CreateCache());

            //SuppressCaching - option 1 (50 calls)
            using (TracingContext.SuppressCaching(CacheSuppress.RecursiveAllDownstreamServices))
            {
                for (int i = 0; i < 50; i++)
                {
                    var actual = await (Task<Thing>) memoizer.Memoize(dataSource, ThingifyTaskThing,
                        new object[] {"someString"}, GetCachingSettings());
                    actual.Id.ShouldBe(firstValue);
                }
            }

            //SuppressCaching - option 2 (50 calls)
            using (TracingContext.SuppressCaching(CacheSuppress.UpToNextServices))
            {
                for (int i = 0; i < 50; i++)
                {
                    var actual = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing,
                        new object[] { "someString" }, GetCachingSettings());
                    actual.Id.ShouldBe(firstValue);
                }
            }

            //Use cache (we are not in suppressCaching block
            for (int i = 0; i < 50; i++)
            {
                var actual = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings());
                actual.Id.ShouldBe(firstValue);
            }

            dataSource.Received(100).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_CallsWithSuppressCachingAndResponseCachingIsDisabledByConfig_ResponseIsNotCachedWhileCacheIsSuppressed()
        {
            string firstValue  = "first Value";
            string secondValue = "second Value";
            string thirdValue  = "third Value";
            string fourthValue = "fourth Value";
            var dataSource = CreateDataSource(firstValue, secondValue, thirdValue, fourthValue);
            var memoizer = CreateMemoizer(CreateCache());

            //SuppressCaching - option 1 
            using (TracingContext.SuppressCaching(CacheSuppress.RecursiveAllDownstreamServices))
            {
                (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, 
                GetCachingSettings(cacheResponsesWhenSupressedBehavior: CacheResponsesWhenSupressedBehavior.Disabled))).Id.ShouldBe(firstValue);
            }

            //SuppressCaching - option 2 
            using (TracingContext.SuppressCaching(CacheSuppress.UpToNextServices))
            {
                (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
                GetCachingSettings(cacheResponsesWhenSupressedBehavior: CacheResponsesWhenSupressedBehavior.Disabled))).Id.ShouldBe(secondValue);
            }

            //Because previous calls did not cache the response, the following call will trigger another call to the data source
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
            GetCachingSettings())).Id.ShouldBe(thirdValue);

            //Previous call response was cached (it was out of suppressed cache block)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" },
            GetCachingSettings())).Id.ShouldBe(thirdValue);

            dataSource.Received(3).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_ParallelSuppressedAndNonSuppressedCallsWithSuppressCaching_UsesDataSourceForEverySuppressedCacheCallX()
        {
            string firstValue = "first Value";
            var dataSource = CreateDataSource(firstValue);
            var memoizer = CreateMemoizer(CreateCache());

            //call data source and cache value
            await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings());

            List<Task<Thing>> tasks = new List<Task<Thing>>();

            //calls should use cache, although they run in parallel to suppressed cached calls (but not under using)
            for (int i = 0; i < 10; i++)
            {
                var task = new Task<Thing>(() => ((Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings())).Result);
                tasks.Add(task);
            }

            //SuppressCaching
            using (TracingContext.SuppressCaching(CacheSuppress.RecursiveAllDownstreamServices))
            {
                //start the tasks here, so they will run in parallel to cache suppress 
                foreach (var task in tasks)
                {
                    task.Start();
                }

                //10 suppressed cached calls should call data source
                for (int i = 0; i < 10; i++)
                {
                    await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings());
                }
            }

            await Task.WhenAll(tasks.ToArray());

            dataSource.Received(11).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_MultipleCallsWithDoNotSuppressCaching_UseCacheForDoNotSuppressCalls()
        {
            string firstValue = "first Value";
            var dataSource = CreateDataSource(firstValue);
            var memoizer = CreateMemoizer(CreateCache());

            var actual = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings());
            actual.Id.ShouldBe(firstValue);

            using (TracingContext.SuppressCaching(CacheSuppress.DoNotSuppress))
            {
                for (int i = 0; i < 100; i++)
                {
                    actual = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings());
                    actual.Id.ShouldBe(firstValue);
                }
            }

            dataSource.Received(1).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_MultipleFailingCallsWithSuppressCaching_ThrowExceptionForEachSuppressedCacheCallAndReturnCachedValueForNonSuppressedCachedCalls()
        {
            string firstValue = "first Value";
            var refreshTask = new TaskCompletionSource<Thing>();
            var dataSource = CreateDataSource(firstValue, refreshTask);
            var memoizer = CreateMemoizer(CreateCache());

            using (TracingContext.SuppressCaching(CacheSuppress.RecursiveAllDownstreamServices))
            {
                //Cache result for a successful call 
                var actual = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings()); //1 call to data source
                actual.Id.ShouldBe(firstValue);

                //Should throw for each call to the data source
                refreshTask.SetException(new Exception("Boo!!"));

                //because we are in SuppressCaching using block, all calls will try to go to data source and fail
                for (int i = 0; i < 10; i++) //10 calls to data source
                {
                    var task = (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings());
                    task.ShouldThrow<Exception>();
                }
            }

            for (int i = 0; i < 5; i++) 
            {
                //We are not in SuppressCaching using block, get cached result (NOT FROM DATA SOURCE)
                var value = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings()); //should not call data source
                value.Id.ShouldBe(firstValue);
            }

            //We have total of 11 calls to data source: first one which cached the result, another failing 10 under SuppressCaching using block
            dataSource.Received(11).ThingifyTaskThing("someString");
        }


        [Test]
        public async Task MemoizeAsync_MultipleCallsReturningNull_UsesDataSourceOnlyOnce()
        {
            var dataSource = CreateDataSource(null);
            var memoizer = CreateMemoizer(CreateCache());

            for (int i = 0; i < 100; i++)
            {
                var actual = await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings());
                actual.ShouldBeNull();
            }

            dataSource.Received(1).ThingifyTaskThing("someString");
        }

        [Test]
        public async Task MemoizeAsync_MultipleCallsPrimitive_UsesDataSourceOnlyOnce()
        {
            var dataSource = CreateDataSource(5);
            var memoizer = CreateMemoizer(CreateCache());

            for (int i = 0; i < 100; i++)
            {
                var actual = await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetCachingSettings());
                actual.ShouldBe(5);
            }

            dataSource.Received(1).ThingifyTaskInt("someString");
        }

        [Test]
        public async Task MemoizeAsync_CallsWithDifferentParams_UsesSeparateCacheSlots()
        {
            string firstValue = "first Value";
            string secondValue = "second Value";
            var dataSource = CreateDataSource(firstValue);
            dataSource.ThingifyTaskThing("otherString").Returns(Task.FromResult(new Thing { Id = secondValue }));
            IMemoizer memoizer = CreateMemoizer(CreateCache());

            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings())).Id.ShouldBe(firstValue);
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "otherString" }, GetCachingSettings())).Id.ShouldBe(secondValue);

            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings())).Id.ShouldBe(firstValue);
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "otherString" }, GetCachingSettings())).Id.ShouldBe(secondValue);

            dataSource.Received(1).ThingifyTaskThing("someString");
            dataSource.Received(1).ThingifyTaskThing("otherString");
        }

        [Test]
        [Order(1)] //If test does not run first, it fails (as standalone it pass), probably due to other tests that use the MemoryCache,
                   //and internally the MC may set some expiration static field which effects this test
                   //https://stackoverflow.com/questions/12630168/memorycache-absoluteexpiration-acting-strange
        public async Task MemoizeAsync_TwoCalls_RespectsCachingPolicy()
        {
            string firstValue = "first Value";
            string secondValue = "second Value";
            var dataSource = CreateDataSource(firstValue, secondValue);
            IMemoizer memoizer = CreateMemoizer(CreateCache());

            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings(2))).Id.ShouldBe(firstValue);
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings(2))).Id.ShouldBe(firstValue);
            await Task.Delay(TimeSpan.FromMilliseconds(2000)); //wait for item to be expired

            //Item is expired and a new call to data source is made
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, new object[] { "someString" }, GetCachingSettings())).Id.ShouldBe(secondValue);

            dataSource.Received(2).ThingifyTaskThing("someString");
        }

        [Test]        
        public async Task MemoizeAsync_CallAfterRefreshTime_RefreshBehaviorIsUseOldAndFetchNewValueInBackground_RefreshOnBackground()
        {
            TimeSpan refreshTime = TimeSpan.FromMinutes(1);
            var refreshTask = new TaskCompletionSource<Thing>();
            var args = new object[] { "someString" };
            string firstValue = "first Value";
            string secondValue = "second Value";
            var dataSource = CreateDataSource(firstValue, refreshTask);

            IMemoizer memoizer = CreateMemoizer(CreateCache());
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(refreshBehavior: RefreshBehavior.UseOldAndFetchNewValueInBackground))).Id.ShouldBe(firstValue);

            // fake that refreshTime has passed
            TimeFake.UtcNow += refreshTime;

            // Refresh task hasn't finished, should get old value (5)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(refreshBehavior: RefreshBehavior.UseOldAndFetchNewValueInBackground))).Id.ShouldBe(firstValue); // value is not refreshed yet. it is running on background
            
            // Complete refresh task and verify new value
            refreshTask.SetResult(new Thing { Id = secondValue, Name = "seven" });
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(refreshBehavior: RefreshBehavior.UseOldAndFetchNewValueInBackground))).Id.ShouldBe(secondValue); // new value is expected now
        }

        [Test]
        public async Task MemoizeAsync_CallAfterRefreshTime_RefreshBehaviorIsTryFetchNewValueOrUseOld_RefreshImmediately()
        {
            TimeSpan refreshTime = TimeSpan.FromMinutes(1);
            var args = new object[] { "someString" };
            int firstValue = 1;
            int secondValue = 2;

            var dataSource = CreateDataSource();                                    //delay refresh call (to ensure that we actually await it)
            dataSource.ThingifyTaskInt("someString").Returns(async i => firstValue, async i => { await Task.Delay(100); return secondValue; });
            IMemoizer memoizer = CreateMemoizer(CreateCache());

            (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args, GetCachingSettings(refreshBehavior:RefreshBehavior.TryFetchNewValueOrUseOld))).ShouldBe(firstValue);

            //fake that refreshTime has passed
            TimeFake.UtcNow += refreshTime;

            //Refresh will run immediately and fetch new value 
            (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args, GetCachingSettings(refreshBehavior: RefreshBehavior.TryFetchNewValueOrUseOld))).ShouldBe(secondValue);

            //Cached value is used
            (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args, GetCachingSettings(refreshBehavior: RefreshBehavior.TryFetchNewValueOrUseOld))).ShouldBe(secondValue);

            dataSource.Received(2).ThingifyTaskInt(Arg.Any<string>());
        }

        [Test]
        public async Task MemoizeAsync_CallAfterRefreshTimeAndRefreshReceivesAnException_RefreshBehaviorIsTryFetchNewValueOrUseOld_ExceptionIsIgnoredAndCachedValueIsReturned()
        { 
            var args = new object[] { "someString" };
            int firstValue = 1;
            int secondValue = 2;

            var dataSource = CreateDataSource();                                   
            dataSource.ThingifyTaskInt("someString").Returns(i => firstValue, i => throw new Exception(), i => secondValue);
            IMemoizer memoizer = CreateMemoizer(CreateCache());

            (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args, GetCachingSettings(refreshBehavior: RefreshBehavior.TryFetchNewValueOrUseOld))).ShouldBe(firstValue);

            //fake that refreshTime has passed
            TimeFake.UtcNow += TimeSpan.FromMinutes(1);

            //Refresh will run immediately, will get an exception, and return old cached value
            (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args, GetCachingSettings(refreshBehavior: RefreshBehavior.TryFetchNewValueOrUseOld))).ShouldBe(firstValue);

            //Old cached value is used
            (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args, GetCachingSettings(refreshBehavior: RefreshBehavior.TryFetchNewValueOrUseOld))).ShouldBe(firstValue);

            //fake that FailedRefreshDelay has passed
            TimeFake.UtcNow += TimeSpan.FromSeconds(2);

            //New call to data source will fetch new value
            (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args, GetCachingSettings(refreshBehavior: RefreshBehavior.TryFetchNewValueOrUseOld))).ShouldBe(secondValue);

            dataSource.Received(3).ThingifyTaskInt(Arg.Any<string>());
        }

        [Test]
        [TestCase(RefreshMode.DoNotUseRefreshes)]
        [TestCase(RefreshMode.UseRefreshesWhenDisconnectedFromCacheRevokesBus)]
        public async Task MemoizeAsync_DoNotUseRefreshes_CallAfterRefreshTime_DataSourceIsNotCalledAndResultIsNotRefreshed(RefreshMode refreshMode)
        {
            TimeSpan refreshTime = TimeSpan.FromMinutes(1);
            var args = new object[] { "someString" };
            string firstValue = "first Value";
            string secondValue = "second Value";
            var dataSource = CreateDataSource(firstValue, secondValue);

            IMemoizer memoizer = CreateMemoizer(CreateCache());
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(refreshMode: refreshMode))).Id.ShouldBe(firstValue);

            // fake that refreshTime has passed
            TimeFake.UtcNow += refreshTime;

            for (int i = 0; i < 10; i++)
            {
                (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(refreshMode: refreshMode))).Id.ShouldBe(firstValue);
            }

            dataSource.Received(1).ThingifyTaskThing(Arg.Any<string>());
        }

        [Test]
        public async Task MemoizeAsync_BackgroundRefreshFails_LastGoodValueStillReturned()
        {
            TimeSpan refreshTime = TimeSpan.FromMinutes(1);
            var refreshTask = new TaskCompletionSource<Thing>();
            var args = new object[] { "someString" };
            string firstValue = "only value";
            
            var dataSource = CreateDataSource(firstValue, refreshTask);

            IMemoizer memoizer = CreateMemoizer(CreateCache());
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings())).Id.ShouldBe(firstValue);

            // fake that refreshTime has passed
            TimeFake.UtcNow += refreshTime;

            // Refresh task hasn't finished, should get old value (5)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings())).Id.ShouldBe(firstValue); // value is not refreshed yet. it is running on background

            // Complete refresh task and verify new value
            refreshTask.SetException(new Exception("Boo!!"));
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings())).Id.ShouldBe(firstValue); 
        }

        [Test]
        public async Task MemoizeAsync_BackgroundRefreshFails_NextRequestAfterDelayTriggersRefresh()
        {
            TimeSpan refreshTime = TimeSpan.FromMinutes(1);
            TimeSpan failedRefreshDelay = TimeSpan.FromSeconds(1);
            var refreshTask = new TaskCompletionSource<Thing>();
            var args = new object[] { "someString" };
            string firstValue = "first Value";
            string secondValue = "second Value";
            var dataSource = CreateDataSource(firstValue, refreshTask, secondValue);

            IMemoizer memoizer = CreateMemoizer(CreateCache());
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings())).Id.ShouldBe(firstValue);

            // fake that refreshTime has passed
            TimeFake.UtcNow += refreshTime;

            // Should trigger refresh task that won't be completed yet, should get old value 
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings())).Id.ShouldBe(firstValue);

            // Fail the first refresh task and verify old value still returned.
            refreshTask.SetException(new Exception("Boo!!"));
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings())).Id.ShouldBe(firstValue);

            TimeFake.UtcNow += TimeSpan.FromMilliseconds(failedRefreshDelay.TotalMilliseconds * 0.7);

            // FailedRefreshDelay still hasn't passed so shouldn't trigger refresh.
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings())).Id.ShouldBe(firstValue);

            TimeFake.UtcNow += TimeSpan.FromMilliseconds(failedRefreshDelay.TotalMilliseconds * 0.7);

            // FailedRefreshDelay passed so should trigger refresh in the background (and get old value)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings())).Id.ShouldBe(firstValue);

            // Second refresh should succeed and get new value 
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings())).Id.ShouldBe(secondValue);
        }


        [Test]
        public async Task MemoizeAsync_CallAfterRefreshTime_TTLNotExpired()
        {
            string firstValue= "first Value";
            string secondValue = "second Value";
            string lastValue = "last Value";
            var dataSource = CreateDataSource(firstValue, secondValue, lastValue);
            var args = new object[] { "someString" };

            var revokeCache = Substitute.For<IRecentRevokesCache>();
            revokeCache.TryGetRecentlyRevokedTime(Arg.Any<string>(), Arg.Any<DateTime>()).Returns((DateTime?)null);

            IMemoizer memoizer = new AsyncMemoizer(new AsyncCache(new ConsoleLog(), Metric.Context("AsyncCache"), new DateTimeImpl(), new EmptyRevokeListener(), revokeCache), new MetadataProvider(), Metric.Context("Tests"));

            // T = 0s. No data in cache, should retrieve value from source (5).
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(refreshTimeSeconds: 1))).Id.ShouldBe(firstValue);

            await Task.Delay(TimeSpan.FromSeconds(2));

            // T = 2s. Refresh just triggered (in background), should get old value 
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(refreshTimeSeconds: 1))).Id.ShouldBe(firstValue);

            // Refresh task should have completed by now, verify new value. Should not trigger another refresh.
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(refreshTimeSeconds: 1))).Id.ShouldBe(secondValue); 

            await Task.Delay(TimeSpan.FromSeconds(2));

            // T = 4s. Second TTL passed (after it was updated) and refresh triggered in the background, should get old value  
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(refreshTimeSeconds: 1))).Id.ShouldBe(secondValue);

            // Refresh task should have completed by now, verify new value
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(refreshTimeSeconds: 1))).Id.ShouldBe(lastValue);

            dataSource.Received(3).ThingifyTaskThing(Arg.Any<string>());
        }


        [Test]
        public async Task MemoizeAsync_BackgroundRefreshFails_TTLNotExtended()
        {
            string firstValue = "first Value";
            string secondValue = "second Value after ex";
       
            var args = new object[] { "someString" };
            var refreshTask = new TaskCompletionSource<Thing>();
            refreshTask.SetException(new MissingFieldException("Boo!!"));
            var dataSource = CreateDataSource(firstValue, refreshTask, secondValue);

            var revokeCache = Substitute.For<IRecentRevokesCache>();
            revokeCache.TryGetRecentlyRevokedTime(Arg.Any<string>(), Arg.Any<DateTime>()).Returns((DateTime?)null);

            IMemoizer memoizer = new AsyncMemoizer(new AsyncCache(new ConsoleLog(), Metric.Context("AsyncCache"), new DateTimeImpl(), new EmptyRevokeListener(), revokeCache), new MetadataProvider(), Metric.Context("Tests"));

            // T = 0s. No data in cache, should retrieve value from source (870).
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(5, 1, 100))).Id.ShouldBe(firstValue);

            await Task.Delay(TimeSpan.FromSeconds(2));

            // T = 2s. Past refresh time (1s), this triggers refresh in background (that will fail), should get existing value (870)
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(5, 1, 100))).Id.ShouldBe(firstValue);

            await Task.Delay(TimeSpan.FromSeconds(2));

            // T = 4s. Background refresh failed, but TTL (5s) not expired yet. Should still give old value (870) but won't
            // trigger additional background refresh because of very long FailedRefreshDelay that was spcified (100s).
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(5, 1, 100))).Id.ShouldBe(firstValue);

            await Task.Delay(TimeSpan.FromSeconds(2));

            // T = 6s. We're past the original TTL (5s), and refresh task failed. Items should have been evicted from cache by now
            // according to 5s expiery from T=0s, not from T=2s of the failed refresh. New item (1002) should come in.
            (await (Task<Thing>)memoizer.Memoize(dataSource, ThingifyTaskThing, args, GetCachingSettings(5, 1))).Id.ShouldBe(secondValue);
            dataSource.Received(3).ThingifyTaskThing(Arg.Any<string>());
        }


        [Test]
        [TestCase(RequestGroupingBehavior.Enabled, 1)]
        [TestCase(RequestGroupingBehavior.Disabled, 3)]
        public async Task MemoizeAsync_MultipleIdenticalCallsBeforeFirstCompletes_NumOfDataSourceCallsIsAccordingToGroupingBehavior(RequestGroupingBehavior groupingBehavior, int expectedDataSourceCalls)
        {
            var dataSource = CreateDataSource();
            dataSource.ThingifyTaskInt("someString").Returns(async i => { await Task.Delay(100); return 1; }, async i => { await Task.Delay(100); return 2; });
            IMemoizer memoizer = CreateMemoizer(CreateCache());

            var task1 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetCachingSettings(groupingBehavior: groupingBehavior));
            var task2 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetCachingSettings(groupingBehavior: groupingBehavior));
            var task3 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetCachingSettings(groupingBehavior: groupingBehavior));

            await Task.WhenAll(task1, task2, task3);

            dataSource.Received(expectedDataSourceCalls).ThingifyTaskInt("someString");
        }

        [Test]
        public void MemoizeAsync_MultipleIdenticalCallsBeforeFirstFails_GroupingIsEnabled_TeamsWithFirstCall()
        {
            var dataSource = Substitute.For<IThingFrobber>();
            dataSource.ThingifyTaskInt("someString").Returns<Task<int>>(async i => { await Task.Delay(100); throw new InvalidOperationException(); }, async i => 2);
            IMemoizer memoizer = CreateMemoizer(CreateCache());

            var task1 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetCachingSettings());
            var task2 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetCachingSettings());
            var task3 = (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] { "someString" }, GetCachingSettings());

            task1.ShouldThrow<InvalidOperationException>();
            task2.ShouldThrow<InvalidOperationException>();
            task3.ShouldThrow<InvalidOperationException>();

            dataSource.Received(1).ThingifyTaskInt("someString");
        }

        [Test]
        public async Task MemoizeAsync_MultipleIdenticalCallsBeforeFirstFails_GroupingIsDisabled_SeperateCallsToDataSource()
        {
            int sourceValidResult = 2;
            var dataSource = Substitute.For<IThingFrobber>();
            dataSource.ThingifyTaskInt("someString").Returns<Task<int>>(async i => { await Task.Delay(100); throw new InvalidOperationException(); }, async i => { await Task.Delay(100); return sourceValidResult; });
            IMemoizer memoizer = CreateMemoizer(CreateCache());

            var tasks = new List<Task<int>>
            {
                (Task<int>) memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] {"someString"}, GetCachingSettings(groupingBehavior: RequestGroupingBehavior.Disabled)),
                (Task<int>) memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] {"someString"}, GetCachingSettings(groupingBehavior: RequestGroupingBehavior.Disabled)),
                (Task<int>) memoizer.Memoize(dataSource, ThingifyTaskInt, new object[] {"someString"}, GetCachingSettings(groupingBehavior: RequestGroupingBehavior.Disabled))
            };

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception e) { }

            var numOfFaulted = 0;
            var nonFaultedResult = 0;

            foreach (var task in tasks)
            {
                if (task.IsFaulted)
                    numOfFaulted++;
                else
                    nonFaultedResult = task.Result;
            }

            numOfFaulted.ShouldBe(1); //Only first call throws
            nonFaultedResult.ShouldBe(sourceValidResult); //All other should get a valid result

            dataSource.Received(3).ThingifyTaskInt("someString");
        }

        [Test]
        public void MemoizeAsync_NonCacheableMethods_Throws()
        {
            Should.Throw<ArgumentException>(() => CreateMemoizer(CreateCache()).Memoize(EmptyThingFrobber, ThingifyInt, new object[] { "someString" }, GetCachingSettings()));
            Should.Throw<ArgumentException>(() => CreateMemoizer(CreateCache()).Memoize(EmptyThingFrobber, ThingifyThing, new object[] { "someString" }, GetCachingSettings()));
            Should.Throw<ArgumentException>(() => CreateMemoizer(CreateCache()).Memoize(EmptyThingFrobber, ThingifyTask, new object[] { "someString" }, GetCachingSettings()));
            Should.Throw<ArgumentException>(() => CreateMemoizer(CreateCache()).Memoize(EmptyThingFrobber, ThingifyVoidMethod, new object[] { "someString" }, GetCachingSettings()));
        }


        [Test]
        public async Task MemoizeAsync_DataSourceReturnsDifferentResponseOnEveryCallAndRefreshBehaviorIsTryFetchNewValueOrUseOld_ReturnsNewValueOnEveryCall()
        {
            var args = new object[] { "someString" };
            int callIndex = 0;

            var dataSource = CreateDataSource();
            dataSource.ThingifyTaskInt("someString").Returns(i => callIndex++);

            IMemoizer memoizer = CreateMemoizer(CreateCache());

            //all calls will trigger a call to data source
            //expected results: 0, 1, 2, 3, 4... (return current data source response)
            for (int i = 0; i < 100; i++)
            {
                (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args,
                    GetCachingSettings(refreshTimeSeconds: 0, failedRefreshDelaySeconds: 0, refreshBehavior: RefreshBehavior.TryFetchNewValueOrUseOld))).ShouldBe(i);
            }

            dataSource.Received(100).ThingifyTaskInt("someString");
        }

        [Test]
        public async Task MemoizeAsync_DataSourceReturnsDifferentResponseOnEveryCallAndRefreshBehaviorIsUseOldAndFetchNewValueInBackground_ReturnsOldCachedValueOnEveryCallAndTriggerABackroundRefresh()
        {
            var args = new object[] { "someString" };
            int callIndex = 0;

            var dataSource = CreateDataSource();
            dataSource.ThingifyTaskInt("someString").Returns(i => callIndex++);

            IMemoizer memoizer = CreateMemoizer(CreateCache());

            //all calls will trigger a call to data source
            //expected results: 0, 0, 1, 2, 3, 4... (return previous data source response)
            for (int i = 0; i < 100; i++)
            {
                var expectedValue = i == 0 ? 0 : i -1;

                (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args,
                    GetCachingSettings(refreshTimeSeconds: 0, failedRefreshDelaySeconds: 0, refreshBehavior: RefreshBehavior.UseOldAndFetchNewValueInBackground))).ShouldBe(expectedValue);
            }

            dataSource.Received(100).ThingifyTaskInt("someString");
        }

        [Test]
        [TestCase(RefreshBehavior.UseOldAndFetchNewValueInBackground)]
        [TestCase(RefreshBehavior.TryFetchNewValueOrUseOld)]
        public async Task MemoizeAsync_FlackyDataSource_OldCachedValueIsUsed(RefreshBehavior refreshBehavior)
        {
            var args = new object[] { "someString" };
            int firstValue = 1;
            int callIndex = 0;

            var dataSource = CreateDataSource();
            dataSource.ThingifyTaskInt("someString").Returns(i =>
            {
                callIndex++;

                if (callIndex % 2 == 0)
                    throw new EnvironmentException(""); 
                else
                    return firstValue; 
            });

            IMemoizer memoizer = CreateMemoizer(CreateCache());

            //first call will return firstValue and cache it
            //all calls will trigger a call to data source
            for (int i = 0; i < 100; i++)
            {
                (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args,
                    GetCachingSettings(refreshTimeSeconds: 0, failedRefreshDelaySeconds:0, refreshBehavior: refreshBehavior))).ShouldBe(firstValue);
            }

            dataSource.Received(100).ThingifyTaskInt("someString");
        }

        [Test]
        [TestCase(RefreshBehavior.UseOldAndFetchNewValueInBackground)]
        [TestCase(RefreshBehavior.TryFetchNewValueOrUseOld)]
        public async Task MemoizeAsync_DataSourceNotAvailableAndCachedValueExist_OldCachedValueIsUsed(RefreshBehavior refreshBehavior)
        {
            var args = new object[] { "someString" };
            int firstValue = 1;

            var dataSource = CreateDataSource();
            dataSource.ThingifyTaskInt("someString").Returns(i => firstValue, i => throw new EnvironmentException(""));

            IMemoizer memoizer = CreateMemoizer(CreateCache());

            //first call will return firstValue and cache it
            //all calls will trigger a call to data source
            for (int i = 0; i < 100; i++)
            {
                (await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args,
                    GetCachingSettings(refreshTimeSeconds: 0, failedRefreshDelaySeconds: 0, refreshBehavior: refreshBehavior))).ShouldBe(firstValue);
            }

            dataSource.Received(100).ThingifyTaskInt("someString");
        }

        [Test]
        [TestCase(RefreshBehavior.UseOldAndFetchNewValueInBackground)]
        [TestCase(RefreshBehavior.TryFetchNewValueOrUseOld)]
        public async Task MemoizeAsync_DataSourceNotAvailableAndCachedValueDoesNotExist_Exception(RefreshBehavior refreshBehavior)
        {
            var args = new object[] { "someString" };
            var dataSource = CreateDataSource();
            dataSource.ThingifyTaskInt("someString").Returns<Task<int>>(async i => throw new EnvironmentException(""));

            IMemoizer memoizer = CreateMemoizer(CreateCache());

            //all calls will trigger a call to data source
            for (int i = 0; i < 100; i++)
            {
                Exception ex = null;

                try
                {
                    await (Task<int>)memoizer.Memoize(dataSource, ThingifyTaskInt, args,
                        GetCachingSettings(refreshTimeSeconds: 0, failedRefreshDelaySeconds: 0, refreshBehavior: refreshBehavior));
                }
                catch (Exception e)
                {
                    ex = e;
                }

                ex.GetType().ShouldBe(typeof(EnvironmentException));
            }

            dataSource.Received(100).ThingifyTaskInt("someString");
        }
    }
}
;