using System.ComponentModel.DataAnnotations;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.UnitTests.Configuration.Verificator
{
	[ConfigurationRoot("VerifiedConfig1", RootStrategy.ReplaceClassNameWithPath)]
	public class VerifiedConfig1 : IConfigObject
	{
		/// <summary>
		/// Expecting the value loaded from config file
		/// </summary>
		public string ValueLoaded { get; set; }
	}

	[ConfigurationRoot("VerifiedConfig2", RootStrategy.ReplaceClassNameWithPath)]
	public class VerifiedConfig2 : IConfigObject
	{
		/// <summary>
		/// Expecting the value remains null and detected as not initialized
		/// </summary>
		[Required]
		public string Required { get; set; }
	}

	[ConfigurationRoot("VerifiedConfig3", RootStrategy.ReplaceClassNameWithPath)]
	public class VerifiedConfig3 : IConfigObject
	{
		/// <summary>
		/// Expecting the string value cannot be converted into an int.
		/// </summary>
		public int TheInt { get; set; }
	}

}
