using System;
using System.Globalization;

namespace RIoT2.Core.Models
{
    public class EventItem
    {
        public string Id { get; set; }
        public string Header { get; set; }
        public string SubHeader { get; set; }
        public string Text { get; set; }
        public bool Expandable { get; set; }
        public bool IsExpanded { get; set; }
        public string Icon { get; set; }
        public string IconColor { get; set; }
        public string IconBgColor { get; set; }
        public string TimeStamp { get; set; }

        public DateTime? GetTimeStampAsDateTime(string format)
        {
            DateTime dateTime;
            if (!DateTime.TryParseExact(TimeStamp, format, null, DateTimeStyles.None, out dateTime))
                return null;
            else
                return dateTime;
        }
    }
}
