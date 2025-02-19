using RIoT2.Core.Models;
using RIoT2.Core.Interfaces.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using Rule = RIoT2.Core.Models.Rule;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using RIoT2.Core.Utils;
using RIoT2.Core.Interfaces;
using Newtonsoft.Json.Linq;

namespace RIoT2.Core.Services
{
    public class RuleProcessorService : IRuleProcessorService
    {
        private readonly IOrchestratorConfigurationService _configuration;
        private readonly IFunctionService _functionService;
        private readonly IStoredObjectService _storedObjects;
        private readonly IMessageStateService _messages;
        private readonly ILogger<RuleProcessorService> _logger;

        public RuleProcessorService(ILogger<RuleProcessorService> logger, IOrchestratorConfigurationService configuration, IFunctionService functionService, IStoredObjectService storedObjectService, IMessageStateService messageStateService) 
        {
            _messages = messageStateService;
            _functionService = functionService;
            _configuration = configuration;
            _storedObjects = storedObjectService;
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
                var rule = _storedObjects.GetAll<Rule>().FirstOrDefault(x => x.Id == id);
                if (rule == null)
                    return new List<EventItem>();

                var ruleDataModel = prepareTriggerData(rule, data);

                RuleEvaluation r = new RuleEvaluation(_functionService, _configuration.GetCommandTemplates().ToList(), _storedObjects.GetAll<Variable>().ToList(), rule, ruleDataModel, true);
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

            //if rule model is entity
            if (rule.Model.Type == Core.ValueType.Entity)
            {
                var json = rule.Model.ToJson();
                //if trigger-value is defined -> inject trigger to it
                if (json.Contains("{trigger-value}"))
                {
                    json = json.Replace(@"""{trigger-value}""", triggerData.ToJson());
                    json = injectAdditionalDataToModel(json);
                    return new ValueModel(json);
                }

                ValueModel model = null;
                //if trigger data is not entity -> append new property "trigger"
                if (triggerData.Type != Core.ValueType.Entity)
                {
                    model = rule.Model.UpdateOrAddProperty(triggerData, "trigger");
                }
                else //if trigger data is also entity merge it -> override properties with same name and add new the ones that no not exist
                {
                    model = rule.Model.Merge(triggerData);
                }

                json = model.ToJson();
                json = injectAdditionalDataToModel(json);

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
             
            if (rule.Model.Type == Core.ValueType.TextArray)
            {
                return rule.Model.AppendModelToArray(triggerData);
            }

            return new ValueModel(triggerData.ToJson());
        }


        /// <summary>
        /// This method injects data to ValueModel Entity
        /// Injectting Report, Command: {R:entityId}, {C:entityId}
        /// Additional data to inject:
        /// {date} -> dd-MM-yyyy
        /// {time} -> HH:mm:ss
        /// {weekday} -> ddd -> mon, tues, wed
        /// </summary>
        /// <returns></returns>
        private string injectAdditionalDataToModel(string json) 
        {
            if (json.Contains("{time}")) 
                json = json.Replace(@"""{time}""", DateTime.Now.ToString("HH:mm:ss"));

            if (json.Contains("{date}"))
                json = json.Replace(@"""{date}""", DateTime.Now.ToString("dd-MM-yyyy"));

            if (json.Contains("{weekday}"))
                json = json.Replace(@"""{weekday}""", DateTime.Now.ToString("ddd").ToLower());

            //find Reports from json: {R:guid}
            json = findAndReplaceGUIDPlaceHolders("R", json, _messages.Reports);

            //find Commands from json: {C:guid}
            json = findAndReplaceGUIDPlaceHolders("C", json, _messages.Commands);

            //fix possible double quote issue
            json = json.Replace(@"""""", @""""); 

            return json;
        }

        private string findAndReplaceGUIDPlaceHolders(string id, string str, IEnumerable<IMessage> messages) 
        {
            //{id:guid}
            Regex reg = new Regex("[{]("+id+":)([a-z0-9]{8}[-][a-z0-9]{4}[-][a-z0-9]{4}[-][a-z0-9]{4}[-][a-z0-9]{12})[}]", RegexOptions.IgnoreCase);
            var matches = reg.Matches(str);
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    var matchLast = match.Value.Split(':').Last();
                    var guid = matchLast.Remove(matchLast.Length - 1);
                    var msg = messages.FirstOrDefault(x => x.Id.ToLower() == guid.ToLower());
                    if (msg != null)
                    {
                        str = str.Replace(match.Value, msg.Value.ToJson());
                    }
                }
            }
            return str;
        }
            
        private async Task process(Rule rule, Report report, Func<List<RuleEvaluationResult>, Task> outputHandler)
        {
            List<RuleEvaluationResult> outputs = new List<RuleEvaluationResult>();
            await Task.Factory.StartNew(() =>
            {
                var ruleDataModel = prepareTriggerData(rule, report.Value);

                var commands = _configuration.GetCommandTemplates().ToList();
                var variables = _storedObjects.GetAll<Variable>().ToList();

                RuleEvaluation r = new RuleEvaluation(_functionService, commands, variables, rule, ruleDataModel);
                if (r.Results != null && r.Results.Count > 0)
                    outputs.AddRange(r.Results);
            });

            await outputHandler(outputs);
        }
    }
}