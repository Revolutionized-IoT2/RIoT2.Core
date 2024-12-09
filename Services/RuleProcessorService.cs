using RIoT2.Core.Models;
using RIoT2.Core.Interfaces.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using Rule = RIoT2.Core.Models.Rule;
using Microsoft.Extensions.Logging;

namespace RIoT2.Common.Services
{
    public class RuleProcessorService : IRuleProcessorService
    {
        private readonly IOrchestratorConfigurationService _configuration;
        private readonly IFunctionService _functionService;
        private readonly IStoredObjectService _storedObjecs;
        private readonly ILogger<RuleProcessorService> _logger;

        public RuleProcessorService(ILogger<RuleProcessorService> logger, IOrchestratorConfigurationService configuration, IFunctionService functionService, IStoredObjectService storedObjectService) 
        {
            _functionService = functionService;
            _configuration = configuration;
            _storedObjecs = storedObjectService;
            _logger = logger;
        }

        public async Task ProcessReportAsync(Report report, IEnumerable<Rule> rules, Func<List<RuleEvaluationResult>, Task> outputHandler)
        {
            List<Task> tasks = new List<Task>();
            foreach (var rule in rules)
                tasks.Add(process(rule, report, outputHandler));

            await Task.WhenAll(tasks);
        }

        public List<EventItem> RunRuleSimulation(string id, ValueModel data)
        {
            try
            {
                var rule = _storedObjecs.GetAll<Rule>().FirstOrDefault(x => x.Id == id);
                if (rule == null)
                    return new List<EventItem>();

                var ruleDataModel = prepareTriggerData(rule, data);

                RuleEvaluation r = new RuleEvaluation(_functionService, _configuration.GetCommandTemplates().ToList(), _storedObjecs.GetAll<Variable>().ToList(), rule, ruleDataModel, true);
                return r.Summary;
            }
            catch (Exception x)
            {
                _logger.LogError("Error occured in RunRuleSimulation", x);
                return new List<EventItem>();
            }
        }

        /// <summary>
        /// 1. If Rule datamodel is null or empty, use trigger data as input data
        /// 2. If Rule model contains {trigger-value} -> inject trigger data rule model
        /// 3. If Rule datamodel is primitive, try converting trigger data to rule data
        /// 4. If Rule datamodel is Entity and trigger is not, add 'trigger' -property and inject data
        /// 5. If Rule and trigger datamodels are both Entities, merge trigger -> rule model
        /// 6. If Rule datamodel is TextArray, try appending trigger data to array
        /// </summary>
        /// <param name="rule"></param>s
        /// <param name="triggerData"></param>
        /// <returns></returns>
        private ValueModel prepareTriggerData(Rule rule, ValueModel triggerData) 
        {
            if(rule.Model == null)
                return triggerData;

            //handle injection
            var json = rule.Model.ToJson();
            if (json.Contains("{trigger-value}")) 
            {
                json = json.Replace(@"""{trigger-value}""", triggerData.ToJson());
                json = json.Replace(@"""""", @""""); //fix possible double quote issue
                return new ValueModel(json);
            }

            //Try to convert number 
            if (rule.Model.Type == Core.ValueType.Number)  
            {
                if(triggerData.Type == Core.ValueType.Number)
                    return triggerData;

                if (decimal.TryParse(triggerData.ToJson(), out var decNumber))
                    return new ValueModel(decNumber);

                if (Int32.TryParse(triggerData.ToJson(), out var intNumber))
                    return new ValueModel(intNumber);
            }

            //try to convert bool
            if (rule.Model.Type == Core.ValueType.Boolean)            
            {
                if (triggerData.Type == Core.ValueType.Boolean)
                    return triggerData;

                if (bool.TryParse(triggerData.ToJson(), out var b))
                    return new ValueModel(b);
            }

            //if rule model is entity and trigger in primitive-> a
            if (rule.Model.Type == Core.ValueType.Entity) 
            {
                if (triggerData.Type != Core.ValueType.Entity)
                {
                    return rule.Model.UpdateOrAddProperty(triggerData, "trigger");
                }
                else 
                {
                    return rule.Model.Merge(triggerData);
                }
            }

            if (rule.Model.Type == Core.ValueType.TextArray)
            {
                return rule.Model.AppendModelToArray(triggerData);
            }

            return new ValueModel(triggerData.ToJson());
        }
            
        private async Task process(Rule rule, Report report, Func<List<RuleEvaluationResult>, Task> outputHandler)
        {
            List<RuleEvaluationResult> outputs = new List<RuleEvaluationResult>();
            await Task.Factory.StartNew(() =>
            {
                var ruleDataModel = prepareTriggerData(rule, report.Value);

                var commands = _configuration.GetCommandTemplates().ToList();
                var variables = _storedObjecs.GetAll<Variable>().ToList();

                RuleEvaluation r = new RuleEvaluation(_functionService, commands, variables, rule, ruleDataModel);
                if (r.Results != null && r.Results.Count > 0)
                    outputs.AddRange(r.Results);
            });

            await outputHandler(outputs);
        }
    }
}