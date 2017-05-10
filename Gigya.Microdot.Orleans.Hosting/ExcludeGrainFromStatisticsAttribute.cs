using System;

namespace Gigya.Microdot.Orleans.Hosting
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ExcludeGrainFromStatisticsAttribute : Attribute
    {
        
    }
}