using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Common.Services
{
    public class MessageStateService : IMessageStateService
    {
        private List<Command> _commands;
        private List<Report> _reports;
        private static readonly int _maxHistory = 25;
        Dictionary<string, List<Report>> _history;

        public MessageStateService() 
        {
            Reset(true);
        }

        public IEnumerable<Command> Commands { get { return _commands; } }
        public IEnumerable<Report> Reports { get { return _reports; } }

        public IEnumerable<Report> GetHistory(string reportId, int? count = null)
        {
            if (_history.ContainsKey(reportId)) 
            {
                var reports = _history[reportId].OrderByDescending(x => x.TimeStamp).ToList();
                if (count == null)
                {
                    return reports;
                }
                else 
                {
                    return reports.GetRange(0, count.Value > reports.Count() ? reports.Count() : count.Value);
                }
            }

            return null;
        }

        private void addToHistory(Report report)
        {
            if (!_history.ContainsKey(report.Id))
                _history.Add(report.Id, new List<Report>());

            _history[report.Id].Add(report);

            if (_history[report.Id].Count > _maxHistory) 
                _history[report.Id].RemoveAt(0);

        }

        public void SetState(Report report, bool maintainHistory = false)
        {
            var existingReport = _reports.FirstOrDefault(x => x.Id == report.Id);
            if(existingReport != null)
                _reports.Remove(existingReport);

            _reports.Add(report);

            if(maintainHistory)
                addToHistory(report);
        }

        public void SetState(Command command)
        {
            var existingCommand = _commands.FirstOrDefault(x => x.Id == command.Id);
            if (existingCommand != null)
                _commands.Remove(existingCommand);

            _commands.Add(command);
        }

        public void Reset(bool includeState = false)
        {
            if (includeState) 
            {
                _commands = new List<Command>();
                _reports = new List<Report>();
            }

            _history = new Dictionary<string, List<Report>>();
        }
    }
}