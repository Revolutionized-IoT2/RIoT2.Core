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
            get => IsValidAt(DateTime.Now);
        }

        internal bool IsValidAt(DateTime now)
        {
            if (TimesValid.HasValue && TimesUsed >= TimesValid.Value)
                return false;
            if (From.HasValue && now < From.Value)
                return false;
            if (To.HasValue && now > To.Value)
                return false;
            return true;
        }
    }
}
