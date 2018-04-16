using System;

namespace Gigya.Microdot.Orleans.Hosting.UnitTests
{
	public interface IBusinessEntity
	{
		string Name { get; set; }
		int Number { get; set; }
	}

    [Serializable]
	public class BusinessEntity : IBusinessEntity
	{
		public string Name { get; set; }
		public int Number { get; set; }

		public override bool Equals(object obj)
		{
			BusinessEntity other = (BusinessEntity)obj;
			return Name == other.Name && Number == other.Number;
		}

		public override int GetHashCode() { unchecked { return 0; } }
	}
}