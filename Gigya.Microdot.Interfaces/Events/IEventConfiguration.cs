using System.Text.RegularExpressions;

namespace Gigya.Microdot.Interfaces.Events
{
	public interface IEventConfiguration
	{		
		Regex ExcludeStackTraceRule { get; set; }

		bool ExcludeParams { get; set; }

		int ParamTruncateLength { get; set; }
	}
}