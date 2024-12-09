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
        /// Returns refresh schedules for device. If device level schedule is defined, it will override report specifig schedules
        /// </summary>
        /// <returns>List of trigger schedules for device</returns>
        public IEnumerable<SchedulerTrigger> GetRefreshSchedules() 
        {
            if (!String.IsNullOrEmpty(RefreshSchedule)) 
            {
                yield return new SchedulerTrigger()
                {
                    CronSchedule = RefreshSchedule,
                    Group = Id,
                    Name = Id
                };
                yield break;
            }

            foreach (var reportTemplate in ReportTemplates) 
            {
                if(!String.IsNullOrEmpty(reportTemplate.RefreshSchedule))
                    yield return new SchedulerTrigger() 
                    {
                        CronSchedule = reportTemplate.RefreshSchedule,
                        Group = Id,
                        Name = reportTemplate.Id
                    };
            }
        }
    }
}
