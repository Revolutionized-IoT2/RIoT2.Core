using Quartz;
using System;
using System.Collections.Generic;

namespace RIoT2.Core.Models
{
    public class DeviceConfiguration
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ClassFullName { get; set; }
        public List<CommandTemplate> CommandTemplates { get; set; }
        public List<ReportTemplate> ReportTemplates { get; set; }
        public Dictionary<string, string> DeviceParameters { get; set; }
        public string RefreshSchedule { get; set; }
        
        public SchedulerTrigger GetDeviceRefreshSchedule() 
        {
            if (!String.IsNullOrEmpty(RefreshSchedule)) 
            {
                if (CronExpression.IsValidExpression(RefreshSchedule))
                {
                    return new SchedulerTrigger()
                    {
                        CronSchedule = RefreshSchedule,
                        Group = Id,
                        Name = Id
                    };
                } 
            }
            return null;
        }

    }
}
