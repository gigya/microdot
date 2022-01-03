using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Gigya.Microdot.Hosting.HttpService.Endpoints;
using Gigya.Microdot.Hosting.HttpService.Endpoints.GCEndpoint;
using Gigya.Microdot.Hosting.Service;
using Gigya.Microdot.Interfaces.Logging;
using Gigya.Microdot.Interfaces.SystemWrappers;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NUnit.Framework;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests.Microservice
{
    [TestFixture]
    [NonParallelizable]
    public class GCEndpointTests
    {
        private IGCEndpointHandler _gcEndpointHandler;
        private IGCCollectionRunner _gcCollectionrSub;
        private ILog _logger;
        private IDateTime _dateTime;
        private IGCTokenHandler _gcTokenHandler;
        private IGCTokenContainer _gcTokenContainer;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _gcEndpointHandler = Substitute.For<IGCEndpointHandler>();
            _gcCollectionrSub = Substitute.For<IGCCollectionRunner>();
            _logger = Substitute.For<ILog>();
            _dateTime = Substitute.For<IDateTime>();
            _gcTokenHandler = Substitute.For<IGCTokenHandler>();
            _gcTokenContainer = Substitute.For<IGCTokenContainer>();
        }
        
        [SetUp]
        public void Setup()
        {
            _gcEndpointHandler.ClearSubstitute();
            _gcCollectionrSub.ClearSubstitute();
            _logger.ClearSubstitute();
            _dateTime.ClearSubstitute();
            _gcTokenHandler.ClearSubstitute();
            _gcTokenHandler.ClearSubstitute();
        }
        
        [Test]
        public async Task HandledGcEndpointTest()
        {
            var gcCollectionResult = new GCCollectionResult(100, 10, 55);
            var gcHandlingResult = 
                new GCHandlingResult(true, "foo bar", gcCollectionResult);
            
            _gcEndpointHandler
                .Handle(Arg.Any<Uri>(), Arg.Any<NameValueCollection>())
                .Returns(Task.FromResult(gcHandlingResult));
            
            var responses = new List<(string, HttpStatusCode, string)>();
            
            var gcEndpoint = new GCCustomEndpoint(_gcEndpointHandler);
            var tryHandleResult = await gcEndpoint.TryHandle(null, async (data, status, type) => responses.Add((data, status, type)));

            var receivedCalls = _gcEndpointHandler
                .Received(1)
                .Handle(Arg.Any<Uri>(), Arg.Any<NameValueCollection>());
            
            Assert.AreEqual(true, tryHandleResult);
            Assert.AreEqual(1, responses.Count());
            
            var deserializedResult = JsonConvert.DeserializeObject<GCHandlingResult>(responses.First().Item1);
            Assert.AreEqual(gcHandlingResult.Message, deserializedResult.Message);
            Assert.AreEqual(gcHandlingResult.GcCollectionResult.TotalMemoryAfterGC, deserializedResult.GcCollectionResult.TotalMemoryAfterGC);
            Assert.AreEqual(gcHandlingResult.GcCollectionResult.TotalMemoryBeforeGC, deserializedResult.GcCollectionResult.TotalMemoryBeforeGC);
        }
        
        [Test]
        public async Task NotHandledGcEndpointTest()
        {
            _gcEndpointHandler
                .Handle(Arg.Any<Uri>(), Arg.Any<NameValueCollection>())
                .Returns(Task.FromResult(new GCHandlingResult(false)));
            
            var responses = new List<(string, HttpStatusCode, string)>();
            
            var gcEndpoint = new GCCustomEndpoint(_gcEndpointHandler);
            var tryHandleResult = await gcEndpoint.TryHandle(null, async (data, status, type) => responses.Add((data, status, type)));

            var receivedCalls = _gcEndpointHandler
                .Received(1)
                .Handle(Arg.Any<Uri>(), Arg.Any<NameValueCollection>());
            
            Assert.AreEqual(false, tryHandleResult);
            Assert.AreEqual(0, responses.Count());
        }
        
        [Test]
        public async Task GCEndpointHandlerTest_Config_Off_Matching_Path()
        {
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = false
            }, _logger, _gcCollectionrSub, _gcTokenHandler);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection()
                {
                    { "gcType", "gen0" }
                });
            
            Assert.IsFalse(gcHandlingResult.Successful);
            Assert.IsNull(gcHandlingResult.Message);
            Assert.IsNull(gcHandlingResult.GcCollectionResult);
        }
        
        [Test]
        public async Task GCEndpointHandlerTest_Config_On_Not_Matching_Path()
        {
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = true
            }, 
                _logger, _gcCollectionrSub, _gcTokenHandler);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/not-matching-path"), new NameValueCollection()
            {
                { "gcType", "someGcType" }
            });
            
            Assert.IsFalse(gcHandlingResult.Successful);
            Assert.IsNull(gcHandlingResult.Message);
            Assert.IsNull(gcHandlingResult.GcCollectionResult);
        }
        
        [Test]
        public async Task GCEndpointHandlerTest_Config_On_Token_Generation_Failed()
        {
            var expectedMessage = "Some Token Generation Failed Message";
            _gcTokenHandler.TryProcessAsTokenGenerationRequest(
                Arg.Any<NameValueCollection>(),
                out Arg.Any<string>())
                .Returns(x =>
                {
                    x[1] = expectedMessage;
                    return false;
                });
            
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = true
            }, _logger, _gcCollectionrSub, _gcTokenHandler);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection());
            
            Assert.IsTrue(gcHandlingResult.Successful);
            Assert.AreEqual(expectedMessage,gcHandlingResult.Message);
            Assert.IsNull(gcHandlingResult.GcCollectionResult);
        }
        
        [Test]
        public async Task GCEndpointHandlerTest_Config_On_Token_Validation_Failed()
        {
            var expectedMessage = "Some Token Validation Failed Message";
            _gcTokenHandler.TryProcessAsTokenGenerationRequest(
                    Arg.Any<NameValueCollection>(),
                    out Arg.Any<string>())
                .Returns(false);

            _gcTokenHandler.ValidateToken(Arg.Any<NameValueCollection>(),
                out Arg.Any<string>()).Returns(x =>
            {
                x[1] = expectedMessage;
                return false;
            });
            
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = true
            }, _logger, _gcCollectionrSub, _gcTokenHandler);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection());
            
            Assert.IsTrue(gcHandlingResult.Successful);
            Assert.AreEqual(expectedMessage,gcHandlingResult.Message);
            Assert.IsNull(gcHandlingResult.GcCollectionResult);
        }
        
        [Test]
        public async Task GCEndpointHandlerTest_Config_On_GcType_Validation_Failed()
        {
            var expectedMessage = "Some GcType Validation Failed Message";

            _gcTokenHandler.TryProcessAsTokenGenerationRequest(Arg.Any<NameValueCollection>(),
                out Arg.Any<string>()).Returns(false);
            
            _gcTokenHandler.ValidateGcType(
                    Arg.Any<NameValueCollection>(),
                    out Arg.Any<string>(), 
                    out Arg.Any<GCType>())
                .Returns(x =>
                {
                    x[1] = expectedMessage;
                    return false;
                });

            _gcTokenHandler.ValidateToken(Arg.Any<NameValueCollection>(),
                out Arg.Any<string>()).Returns(true);
            
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = true
            }, _logger, _gcCollectionrSub, _gcTokenHandler);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection());
            
            Assert.IsTrue(gcHandlingResult.Successful);
            Assert.AreEqual(expectedMessage,gcHandlingResult.Message);
            Assert.IsNull(gcHandlingResult.GcCollectionResult);
        }
        
        [Test]
        public async Task GCTokenHandlerTest_No_GcType()
        {
            var gcTokenHandler =
                new GCTokenHandler(
                    () => new MicrodotHostingConfig(), 
                    _logger, 
                    _dateTime, 
                    _gcTokenContainer);

            var validationResult = 
                gcTokenHandler.ValidateGcType(
                    new NameValueCollection(), 
                    out var additionalInfo, 
                    out var gcType);

            Assert.IsFalse(validationResult);
            Assert.AreEqual("GCEndpoint called with unsupported GCType", additionalInfo);
        }
        
        [Test]
        public async Task GCTokenHandlerTest_Wrong_GcType()
        {
            var gcTokenHandler =
                new GCTokenHandler(
                    () => new MicrodotHostingConfig(), 
                    _logger, 
                    _dateTime, 
                    _gcTokenContainer);

            var nameValueCollection = new NameValueCollection();
            nameValueCollection.Add("gcType", "fooBar");
            
            var validationResult = 
                gcTokenHandler.ValidateGcType(
                    nameValueCollection, 
                    out var additionalInfo, 
                    out var gcType);

            Assert.IsFalse(validationResult);
            Assert.AreEqual("GCEndpoint called with unsupported GCType", additionalInfo);
        }
        
        [Test]
        [TestCase("Gen0")]
        [TestCase("Gen1")]
        [TestCase("Gen2")]
        [TestCase("LOHCompaction")]
        [TestCase("BlockingLohCompaction")]
        public async Task GCTokenHandlerTest_Valid_GcType(string gcTypeString)
        {
            var gcTokenHandler =
                new GCTokenHandler(
                    () => new MicrodotHostingConfig(), 
                    _logger, 
                    _dateTime, 
                    _gcTokenContainer);

            var nameValueCollection = new NameValueCollection();
            nameValueCollection.Add("gcType", gcTypeString);
            
            var validationResult = 
                gcTokenHandler.ValidateGcType(
                    nameValueCollection, 
                    out var additionalInfo, 
                    out var gcType);

            Assert.IsTrue(validationResult);
            Assert.AreEqual(gcType, Enum.Parse(typeof(GCType),gcTypeString));
        }
        
        [Test]
        public async Task GCTokenHandlerTest_ValidateToken_No_Param()
        {
            var gcTokenHandler =
                new GCTokenHandler(
                    () => new MicrodotHostingConfig(), 
                    _logger, 
                    _dateTime, 
                    _gcTokenContainer);

            var nameValueCollection = new NameValueCollection();
            
            
            var validateTokenResult = 
                gcTokenHandler.ValidateToken(
                    nameValueCollection, 
                    out var additionalInfo);

            Assert.IsFalse(validateTokenResult);
            Assert.AreEqual("Illegal or missing token", additionalInfo);
        }
        
        [Test]
        public async Task GCTokenHandlerTest_ValidateToken_UnrecognizedToken()
        {
            var gcTokenHandler =
                new GCTokenHandler(
                    () => new MicrodotHostingConfig(), 
                    _logger, 
                    _dateTime, 
                    _gcTokenContainer);

            var nameValueCollection = new NameValueCollection();
            nameValueCollection.Add("token", Guid.NewGuid().ToString());
            
            var validateTokenResult = 
                gcTokenHandler.ValidateToken(
                    nameValueCollection, 
                    out var additionalInfo);

            Assert.IsFalse(validateTokenResult);
            Assert.AreEqual("Illegal or missing token", additionalInfo);
        }
        
        [Test]
        public async Task GCTokenHandlerTest_ValidateToken_Illegal_Token()
        {
            var gcTokenHandler =
                new GCTokenHandler(
                    () => new MicrodotHostingConfig(), 
                    _logger, 
                    _dateTime, 
                    _gcTokenContainer);

            var nameValueCollection = new NameValueCollection();
            nameValueCollection.Add("token", "foo_bar_buzz");
            
            var validateTokenResult = 
                gcTokenHandler.ValidateToken(
                    nameValueCollection, 
                    out var additionalInfo);

            Assert.IsFalse(validateTokenResult);
            Assert.AreEqual("Illegal or missing token", additionalInfo);
        }
        
        [Test]
        public async Task GCTokenHandlerTest_ValidateToken_RecognizedToken()
        {
            var expectedToken = Guid.NewGuid();
            _gcTokenContainer.ValidateToken(Arg.Is<Guid>(expectedToken)).Returns(true);
            
            var gcTokenHandler =
                new GCTokenHandler(
                    () => new MicrodotHostingConfig(), 
                    _logger, 
                    _dateTime, 
                    _gcTokenContainer);

            var nameValueCollection = new NameValueCollection();
            nameValueCollection.Add("token", expectedToken.ToString());
            
            var validateTokenResult = 
                gcTokenHandler.ValidateToken(
                    nameValueCollection, 
                    out var additionalInfo);

            Assert.IsTrue(validateTokenResult);
        }
        
        [Test]
        public async Task GCTokenHandlerTest_Generate_Token()
        {
            var now = DateTime.UtcNow;
            var generatedToken = Guid.NewGuid();
            _dateTime.UtcNow.Returns(now);
            
            _gcTokenContainer.GenerateToken().Returns(generatedToken);
            _gcTokenContainer.ValidateToken(Arg.Is<Guid>(x => x == generatedToken)).Returns(true);
            
            var gcTokenHandler =
                new GCTokenHandler(
                    () => new MicrodotHostingConfig()
                    {
                        GCGetTokenCooldown = null
                    }, 
                    _logger, 
                    _dateTime, 
                    _gcTokenContainer);

            var nameValueCollection = new NameValueCollection();
            nameValueCollection.Add("getToken", null);
            
            var generateTokenResult = 
                gcTokenHandler.TryProcessAsTokenGenerationRequest(
                    nameValueCollection, 
                    out var additionalInfo);

            Assert.IsTrue(generateTokenResult);

            var logCalls = _logger.ReceivedCalls().Select(callback =>
            {
                var logOperation = callback.GetArguments()[0] as Action<LogDelegate>;

                (string message, object encryptedTags, object unencryptedTags, Exception exception, bool
                    includeStack) callDetails = (null, null, null, null, false); 
                logOperation.Invoke((string message, object encryptedTags , object unencryptedTags , Exception exception , bool includeStack )=>
                {
                    callDetails = (message, encryptedTags, unencryptedTags, exception, includeStack);
                });
                    
                return callDetails;
                
            }).ToList();
            
            Assert.IsTrue(logCalls.Count == 1);

            var tags = logCalls.First().unencryptedTags;
            var loggedToken = tags.GetType().GetProperty("Token").GetValue(tags);
            Assert.AreEqual(generatedToken, loggedToken);
        }
        
        [Test]
        public async Task GCTokenHandlerTest_Empty_No_GetToken()
        {
            var now = DateTime.UtcNow;
            var generatedToken = Guid.NewGuid();
            _dateTime.UtcNow.Returns(now);
            
            _gcTokenContainer.GenerateToken().Returns(generatedToken);
            _gcTokenContainer.ValidateToken(Arg.Is<Guid>(x => x == generatedToken)).Returns(true);
            
            var gcTokenHandler =
                new GCTokenHandler(
                    () => new MicrodotHostingConfig()
                    {
                        GCGetTokenCooldown = null
                    }, 
                    _logger, 
                    _dateTime, 
                    _gcTokenContainer);

            var nameValueCollection = new NameValueCollection();

            var generateTokenResult = 
                gcTokenHandler.TryProcessAsTokenGenerationRequest(
                    nameValueCollection, 
                    out var additionalInfo);

            Assert.IsFalse(generateTokenResult);
            _logger.Received(0).Info(Arg.Any<Action<Delegate>>());
        }
        
        [Test]
        public async Task GCTokenHandlerTest_CoolDown_Test()
        {
            var now = DateTime.UtcNow;
            var generatedToken = Guid.NewGuid();
            _dateTime.UtcNow.Returns(now);
            
            _gcTokenContainer.GenerateToken().Returns(generatedToken);
            _gcTokenContainer.ValidateToken(Arg.Is<Guid>(x => x == generatedToken)).Returns(true);
            
            var gcTokenHandler =
                new GCTokenHandler(
                    () => new MicrodotHostingConfig()
                    {
                        GCGetTokenCooldown = TimeSpan.FromMinutes(10)
                    }, 
                    _logger, 
                    _dateTime, 
                    _gcTokenContainer);

            var nameValueCollection = new NameValueCollection();
            nameValueCollection.Add("getToken", null);
            
            var generateTokenResult = 
                gcTokenHandler.TryProcessAsTokenGenerationRequest(
                    nameValueCollection, 
                    out var additionalInfo);

            Assert.IsTrue(generateTokenResult);
            Assert.AreEqual("GC token generated", additionalInfo);
            
            _dateTime.UtcNow.Returns(now.AddMinutes(1));
            
            generateTokenResult = 
                gcTokenHandler.TryProcessAsTokenGenerationRequest(
                    nameValueCollection, 
                    out additionalInfo);

            Assert.IsTrue(generateTokenResult);
            Assert.AreEqual($"GC getToken cooldown in effect, will be ready in {TimeSpan.FromMinutes(9)}", 
                additionalInfo);

            _dateTime.UtcNow.Returns(now.AddMinutes(11));
            generateTokenResult = 
                gcTokenHandler.TryProcessAsTokenGenerationRequest(
                    nameValueCollection, 
                    out additionalInfo);
            
            Assert.IsTrue(generateTokenResult);
            Assert.AreEqual("GC token generated", additionalInfo);
            Assert.AreEqual(2,_logger.ReceivedCalls().Count());
        }

        [Test]
        public async Task GCTokenContainer_Generate_And_Validate_Test()
        {
            var now = DateTime.Now;
            _dateTime.UtcNow.Returns(now);
            var gcTokenContainer = new GCTokenContainer(_dateTime);
            var generatedToken = gcTokenContainer.GenerateToken();
            Assert.AreNotEqual(generatedToken, Guid.Empty);
            Assert.IsTrue(gcTokenContainer.ValidateToken(generatedToken));
        }
        
        [Test]
        public async Task GCTokenContainer_Generate_And_Validate_Test_Test_Cleanup()
        {
            var now = DateTime.Now;
            _dateTime.UtcNow.Returns(now);
            var gcTokenContainer = new GCTokenContainer(_dateTime);
            var generatedToken = gcTokenContainer.GenerateToken();
            Assert.AreNotEqual(generatedToken, Guid.Empty);
            Assert.IsTrue(gcTokenContainer.ValidateToken(generatedToken));
            _dateTime.UtcNow.Returns(now.AddMinutes(1));
            Assert.IsTrue(gcTokenContainer.ValidateToken(generatedToken));
            _dateTime.UtcNow.Returns(now.AddMinutes(31));
            Assert.IsFalse(gcTokenContainer.ValidateToken(generatedToken));
        }

        [Test]
        [TestCase(GCType.Gen0)]
        [TestCase(GCType.Gen1)]
        [TestCase(GCType.Gen2)]
        [TestCase(GCType.LOHCompaction)]
        [TestCase(GCType.BlockingLohCompaction)]
        public async Task GCEndpointHandlerTest_On_Matching_Path_Right_GcType(GCType gcType)
        {
            var totalMemoryBeforeGc = 500;
            var totalMemoryAfterGc = 70;
            var elapsedMilliseconds = 33;

            _gcTokenHandler.ValidateToken(Arg.Any<NameValueCollection>(), out Arg.Any<string>())
                .Returns(true);
            _gcTokenHandler.TryProcessAsTokenGenerationRequest(Arg.Any<NameValueCollection>(), out Arg.Any<string>())
                .Returns(false);
            _gcTokenHandler.ValidateGcType(
                    Arg.Any<NameValueCollection>(),
                    out Arg.Any<string>(),
                    out Arg.Any<GCType>())
                .Returns(x=>
                {
                    x[2] = gcType;
                    return true;
                });
            
            _gcCollectionrSub.Collect(Arg.Any<GCType>()).Returns(
                new GCCollectionResult(
                    totalMemoryBeforeGc, 
                    totalMemoryAfterGc, 
                    elapsedMilliseconds)
                );
            
            var gcEndpointHandler = new GCEndpointHandler(() => new MicrodotHostingConfig()
            {
                GCEndpointEnabled = true,
            }, _logger, _gcCollectionrSub, _gcTokenHandler);

            var gcHandlingResult = await gcEndpointHandler
                .Handle(new Uri("http://my-host-name/force-traffic-affecting-gc"), new NameValueCollection()
                {
                    { "gcType", gcType.ToString() }
                });
            
            Assert.IsTrue(gcHandlingResult.Successful);
            Assert.AreEqual("GC ran successfully",gcHandlingResult.Message);
            Assert.NotNull(gcHandlingResult.GcCollectionResult);
            Assert.AreEqual(totalMemoryBeforeGc, gcHandlingResult.GcCollectionResult.TotalMemoryBeforeGC);
            Assert.AreEqual(totalMemoryAfterGc, gcHandlingResult.GcCollectionResult.TotalMemoryAfterGC);
            Assert.AreEqual(elapsedMilliseconds, gcHandlingResult.GcCollectionResult.ElapsedMilliseconds);
        }

        [Test]
        [TestCase(GCType.Gen0)]
        [TestCase(GCType.Gen1)]
        [TestCase(GCType.Gen2)]
        [TestCase(GCType.LOHCompaction)]
        [TestCase(GCType.BlockingLohCompaction)]
        public async Task GCCollectionRunner_Sanity(GCType genType)
        {
            var gcCollectionRunner = new GCCollectionRunner();
            Assert.DoesNotThrow(()=>gcCollectionRunner.Collect(genType));
        }
    }
}