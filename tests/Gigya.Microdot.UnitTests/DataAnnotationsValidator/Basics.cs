using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Gigya.Microdot.Configuration.Objects;
using Gigya.UserManagement.Contracts.Internal;
using Gigya.UserManagement.Contracts.Internal.Sites;
using Gigya.UserManagement.Models.User;
using Newtonsoft.Json;

namespace Gigya.Microdot.UnitTests
{
    public static class RequestModelsValidators
    {
        public static Uuid Validate(this Uuid uuid, string argumentName = null)
        {
            if (string.IsNullOrWhiteSpace(uuid?.Value))
            {
                throw new ArgumentException(argumentName ?? nameof(uuid));
            }

            return uuid;
        }

        public static DateTime? Validate(this DateTime? dateTime, string argumentName = null)
        {
            if (dateTime != null &&  dateTime?.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Date kind is not UTC", argumentName ?? nameof(dateTime));
            }

            return dateTime;
        }

        public static SiteInfo Validate(this SiteInfo siteInfo, string argumentName = null)
        {
            if (siteInfo == null)
            {
                throw new ArgumentNullException(argumentName ?? nameof(siteInfo));
            }

            return siteInfo;
        }

        public static T[] Validate<T>(this T[] values, string argumentName)
        {
            if (!values?.Any() ?? true)
            {
                throw new ArgumentException($"'{argumentName}' is null or empty.", argumentName);
            }

            return values;
        }

        public static AccountFlags Validate(this AccountFlags accountFlags, string argumentName = null)
        {
            if (accountFlags == null)
            {
                throw new ArgumentNullException(argumentName ?? nameof(accountFlags));
            }

            return accountFlags;
        }

        public static SystemData Validate(this SystemData systemData, string argumentName = null)
        {
            if (systemData == null)
            {
                throw new ArgumentNullException(argumentName ?? nameof(systemData));
            }

            return systemData;
        }

        public static string Validate(this string value, string argumentName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"'{argumentName}' is null or empty.", argumentName);
            }

            return value;
        }
    }

    public class IdentitiesFilter
    {
        public string[] IncludeProviders { get; }
        public string[] ExcludeProviders { get; }
        public TimeSpan? MaxCacheAge { get; }

        /// <summary>
        /// True if both IncludeProviders and ExcludeProviders are null or empty.
        /// </summary>
        public bool IncludeAllProviders { get; }

        public static IdentitiesFilter Default => new IdentitiesFilter(null, null, null);

        [JsonConstructor]
        public IdentitiesFilter(
            string[] IncludeProviders, 
            string[] ExcludeProviders, 
            TimeSpan? MaxCacheAge = default(TimeSpan?))
        {
            IncludeAllProviders = IsNullOrEmpty(IncludeProviders) && IsNullOrEmpty(ExcludeProviders);
            if (!IncludeAllProviders)
            {
                this.IncludeProviders = IncludeProviders ?? new string[0];
                this.ExcludeProviders = ExcludeProviders ?? new string[0];
            }
            this.MaxCacheAge = MaxCacheAge ?? TimeSpan.MaxValue;
        }

        private static bool IsNullOrEmpty(string[] arr)
        {
            return arr == null || arr.Length == 0;
        }
    }

    public class GetIdentitiesRequest
    {
        public SiteInfo SiteInfo { get; }
        public Uuid Uuid { get; }
        public IdentitiesFilter Filter { get; }

        // xxx - replace IdentitiesFilter.MaxCacheAge with <DateTime> IdentitiesFilter.MinLastUpdated and then remove GetIdentitiesRequest.Now
        public DateTime? Now { get; }

        [JsonConstructor]
        public GetIdentitiesRequest(
            SiteInfo siteInfo, 
            Uuid uuid, 
            IdentitiesFilter filter, 
            DateTime? now = default(DateTime?))
        {
            SiteInfo = siteInfo.Validate();
            Uuid = uuid.Validate();
            Filter = filter ?? IdentitiesFilter.Default;
            Now = now.Validate();
        }
    }

    [TestFixture]
    public class Basics
    {
        [Test]
        public void Verify_no_stackoverflow_for_static_properties()
        {
            var validator = new DataAnnotationsValidator();

            SiteInfo siteID = SitePool.GetSingleSiteInfo();
            Uuid uuid = GetUuid();
            var id = new Identity();

            var request = new GetIdentitiesRequest(siteID, uuid, 
                new IdentitiesFilter(new[] { "foobar" }, new string[0]));
            
            var results = new List<ValidationResult>();
            validator.TryValidateObjectRecursive(request, results);
        }

        private static Uuid GetUuid()
        {
            return new Uuid() { Value = ValuesRandomizer.RandomString(10) };
        }
    }

    public class ValuesRandomizer
    {
        [ThreadStatic]
        static Random _rnd;
        static protected Random rnd
        {
            get
            {
                if (_rnd == null)
                {
                    _rnd = new Random(Guid.NewGuid().GetHashCode());
                }
                return _rnd;
            }
        }

        public static IEnumerable<string> GetRandomArray(int maxLength = 10)
        {

            var arr = new List<string>();
            var randomCount = RandomInteger(maxLength);
            for (var i = 0; i < randomCount; i++)
            {
                arr.Add(RandomString());
            }

            return arr;
        }

        public static bool RandomBool()
        {
            return rnd.Next(100) > 50;
        }
        public static string RandomString(int length = 5)
        {
            var g = Guid.NewGuid().ToString();
            length = length >= g.Length ? g.Length - 1 : length;
            return Guid.NewGuid().ToString().Substring(0, length);
        }
        public static string RandomEmail()
        {
            return String.Format("{0}@gigya-test.com", RandomString());
        }
        public static string RandomURL()
        {
            return String.Format("http://www.gigya.com/{0}", RandomString());
        }
        public static int RandomInteger(int maxValue = int.MaxValue)
        {
            return rnd.Next(maxValue);
        }
        public static uint RandomUInt(int maxValue = int.MaxValue)
        {
            return Convert.ToUInt32(rnd.Next(0, maxValue));
        }
        public static DateTime RandomDateTime()
        {
            return GetNowTime().AddSeconds(-1 * rnd.Next());
        }
        public static DateTime GetNowTime()
        {
            var date = DateTime.Now;

            return new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, date.Kind);
        }
    }

    public static class SitePool
    {
        const ulong _defaultSiteId = 1234567890;
 
        const ulong _member1SiteId = 1234567891;
        const ulong _member2SiteId = 1234567892;
        const ulong _member3SiteId = 1234567893;

        const string _defaultApiKey = "_defaultApiKey";
      
        public static SiteInfo GetOldSite ()=> new SiteInfo(LazyDataMigrationTestsConsts.TestSiteId,_defaultApiKey,null);


        public static SiteInfo GetSingleSiteInfo() => new SiteInfo(_defaultSiteId, _defaultApiKey, null);
        public static SiteInfo GetSiteWithGroup(bool isMaster, bool ssoEnabled = true)
        {
            ulong primarySiteId;
            List<ulong> members = new List<ulong> { _member2SiteId, _member3SiteId };
            if (isMaster)
            {
                primarySiteId = _defaultSiteId;
                members.Add(_member1SiteId);
            }
            else
            {
                primarySiteId = _member1SiteId;
                members.Add(_defaultSiteId);
            }
            return new SiteInfo(primarySiteId, "", new SiteGroupInfo(primarySiteId,"",  ssoEnabled, members.ToArray()));
        }
    }

     public static class LazyDataMigrationTestsConsts
    {
        public const int TestSiteId = 1234567890;

        public const int MigratedTestSiteId = 1234567890;

        public const string SiteIdentityName = "site";


        public const string Phase_0_0 = "Phase_0_0";

        public const string Phase_0_1 = "Phase_0_1";

        public const string Phase_1_0 = "Phase_1_0";

        public const string Phase_1_1 = "Phase_1_1";

        public const string Phase_1_2 = "Phase_1_2";

        public const string Lite_Integration = "Lite_Integration";



        // ****************************************************************************
        // Users for stage (0,0) - Initial stage
        //*****************************************************************************
        public const string User_WithEvents_WithSnapshotV1WhichIsCorupted = "User_WithEvents_WithSnapshotV1WhichIsCorupted";

        public const string User_WithSnapshotV1WhichIsCorupted = "User_WithSnapshotV1WhichIsCorupted";

        public const string User_WithEvents_WithCreateData = "User_WithEvents_WithCreateData";

        public const string User_WithEvents_WithCreateData_WithEmptyUpdate = "User_WithEvents_WithCreateData_WithEmptyUpdate";


        public const string User_WithEvents = "User_WithEvents";

        public const string User_WithAlmostSnapshot = "User_WithAlmostSnapshot";

        public const string User_WithSnapshotV1 = "User_WithSnapshotV1";

        public const string User_WithEvents_WithSnapshotV1 = "User_WithEvents_WithSnapshotV1";

        public const string User_WithEvents_WithSnapshotV1WhenSiteIdentityDataIsNull =
            "User_WithEvents_WithSnapshotV1WhenSiteIdentityDataIsNull";

        public const string User_WithEventsDataIsEmpty_WithSnapshotV1WithSiteIdentityDataIsNull =
            "User_WithEventsDataIsEmpty_WithSnapshotV1WithSiteIdentityDataIsNull";

        public const string User_WithEventsWhenDataIsNull_WithSnapshotV1WithSiteIdentityDataIsNull =
            "User_WithEventsWhenDataIsNull_WithSnapshotV1WithSiteIdentityDataIsNull";


      
        public const string User_WithSnapshotV2 = "User_WithSnapshotV2";

        public const string User_WithEvents_WithSnapshotV2 = "User_WithEvents_WithSnapshotV2";

 
        // ****************************************************************************
        // Users for stage (1,0) - Enabling data fix 
        //*****************************************************************************
        public const string User_WithEvents_WithFixEvent = "User_WithEvents_WithFixEvent";

        public const string User_WithEvents_WithFixEventWhenIsMultiple = "User_WithEvents_WithFixEventWhenIsMultiple";

        public const string User_WithEvents_WithFixEvent_WithSnapshotV1 = "User_WithEvents_WithFixEvent_WithSnapshotV1";

        public const string User_WithFixEvent_WithSnapshotV1 = "User_WithFixEvent_WithSnapshotV1";

        public const string User_WithFixEvent_WithCreateData = "User_WithFixEvent_WithCreateData";

        public const string User_WithEvents_WithFixEvent_WithCreateData = "User_WithEvents_WithFixEvent_WithCreateData";

        //*************************************************************************************************
        // Users for stage (1,1) - These users should have applied fix data , and do not have fixes anymore
        //*************************************************************************************************

        public const string User_WithEvents_WithSnapshotV1_WithSnapshotV2 =
            "User_WithEvents_WithFixEvent_WithSnapshotV1_WithSnapshotV2";

        public const string User_WithSnapshotV1_WithSnapshotV2 =
            "User_WithFixEvent_WithSnapshotV1_WithSnapshotV2";

        //*********************************************************************************************
        //*********************************************************************************************
        // Users for stage Lite integration 
        //**********************************************************************************************
        //**********************************************************************************************

        public const string User_WithEvents_WithLite = "User_WithEvents_WithLite";

        public const string User_WithEvents_WithCreateData_WithLite = "User_WithEvents_WithCreateData_WithLite";

        public const string User_WithEvents_WithLiteWhenLiteIsSeveralTimes = "User_WithEvents_WithLiteWhenLiteIsSeveralTimes";

        public const string User_WithEvents_WithFixEvent_WithLite = "User_WithEvents_WithFixEvent_WithLite";

        public const string User_WithEvents_WithFixEvent_WithCreateData_WithLite = "User_WithEvents_WithFixEvent_WithCreateData_WithLite";

        //****************************************************************************************
        // Users for stage Lite integration - Unique users for  state = (1,2) 
        //****************************************************************************************

        public const string User_WithSnapshotV2_WithLite = "User_WithSnapshotV2_WithLite";

        public const string User_WithEvents_WithSnapshotV2_WithLite = "User_WithEvents_WithSnapshotV2_WithLite";


        //****************************************************************************************
        // Users for stage Lite integration - Unique users for  state = (1,1) 
        //****************************************************************************************

        public const string User_WithSnapshotV1_WithSnapshoV2_WithLite = "User_WithSnapshotV1_WithSnapshoV2_WithLite";

        public const string User_WithEvents_WithSnapshotV1_WithSnapshoV2_WithLite = "User_WithEvents_WithSnapshotV1_WithSnapshoV2_WithLite";

    }
}
