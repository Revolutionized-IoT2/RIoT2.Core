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
        
        /// <summary>
        /// Builds a scheduler trigger from the configured refresh schedule.
        /// </summary>
        /// <returns>A <see cref="SchedulerTrigger"/> when <c>RefreshSchedule</c> is a valid cron expression; otherwise, <c>null</c>.</returns>
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
