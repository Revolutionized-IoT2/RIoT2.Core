using System;

namespace RIoT2.Core.Models
{
    public class DeviceCode
    {
        public string Code { get; set; }
        public int? TimesValid { get; set; }
        public int TimesUsed { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public bool IsValid
        {
            get
            {
                if (TimesValid.HasValue)
                {
                    if (TimesValid.Value - TimesUsed <= 0)
                        return false;
                }

                if (From.HasValue)
                {
                    if (DateTime.Now < From)
                        return false;
                }

                if (To.HasValue)
                {
                    if (DateTime.Now > To)
                        return false;
                }

                return true;
            }
        }
    }
}
