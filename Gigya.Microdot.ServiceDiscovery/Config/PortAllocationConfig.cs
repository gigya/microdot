using System.ComponentModel.DataAnnotations;

using Gigya.Microdot.SharedLogic;

namespace Gigya.Microdot.ServiceDiscovery.Config
{
    public class PortAllocationConfig 
    {
        public bool IsSlotMode { get; set; } = false;

        [Required]
        public int BasePort { get; set; } = 40000;

        [Required]
        public int RangeSize { get; set; } = 1000;


        public int? GetPort(int? slotNumber, PortOffsets protocol)
        {
            if(slotNumber == null)
                return null;

            return BasePort + RangeSize * (int)protocol + slotNumber;
        }
    }
}
