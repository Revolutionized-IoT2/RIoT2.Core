using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RIoT2.Core.Interfaces.Services
{
    public interface IRuleProcessorService
    {
        Task ProcessReportAsync(Report report, IEnumerable<Rule> rules, Func<List<RuleEvaluationResult>, Task> outputHandler);
        List<EventItem> RunRuleSimulation(string id, ValueModel data);
    }
}