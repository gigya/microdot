using System.ComponentModel.DataAnnotations;
using Gigya.Microdot.Interfaces.Configuration;

namespace Gigya.Microdot.UnitTests.Configuration.Verificator
{
	[ConfigurationRoot("VerifiedConfig1", RootStrategy.ReplaceClassNameWithPath)]
	public class VerifiedConfig1 : IConfigObject
	{
		public string ValueLoaded { get; set; }
	}

	[ConfigurationRoot("VerifiedConfig2", RootStrategy.ReplaceClassNameWithPath)]
	public class VerifiedConfig2 : IConfigObject
	{
		[Required]
		public string Required { get; set; }
	}

	[ConfigurationRoot("VerifiedConfig3", RootStrategy.ReplaceClassNameWithPath)]
	public class VerifiedConfig3 : IConfigObject
	{
		public int TheInt { get; set; }
	}

}
