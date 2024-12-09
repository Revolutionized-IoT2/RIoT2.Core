using System;
using System.Collections.Generic;
using System.Linq;
using RIoT2.Core.Models;
using RIoT2.Core.Interfaces.Services;
using RIoT2.Core;
using RIoT2.Core.Utils;

namespace RIoT2.Common.Services
{
    public class RuleEvaluation
    {
        private DateTime _evaluationStartTime;
        private Rule _rule;
        private readonly int _maxLoopCount = 100;
        private List<LoopGuardItem> _loopGuard;
        private bool _createSummary;
        private IFunctionService _functionService;
        private List<CommandTemplate> _commands;
        private List<Variable> _variables;

        public List<RuleEvaluationResult> Results { get; }
        public List<EventItem> Summary { get; }

        public RuleEvaluation(IFunctionService functionService, List<CommandTemplate> commands, List<Variable> variables, Rule rule, ValueModel inputData, bool createSummary = false)
        {
            _commands = commands;
            _variables = variables;
            _functionService = functionService;
            _createSummary = createSummary;
            _evaluationStartTime = DateTime.Now;
            _loopGuard = new List<LoopGuardItem>();
            _rule = rule;
            Summary = new List<EventItem>();
            Results = new List<RuleEvaluationResult>();

            var validationResults = rule.Validate();
            if (validationResults?.ToList().Count > 0) 
            {
                if (_createSummary)
                    addToSummary("0", "FAILED TO VALIDATE RULE", "", rule.Trigger.Name, RuleType.Trigger, true);
                
                return;
            }

            if (rule.RuleItems == null)
                return;

            if (_createSummary)
                addToSummary(rule.Trigger.Id.ToString(), $"Trigger activated with data: {inputData.ToJson()}", $"{rule.Trigger.Description}", rule.Trigger.Name, RuleType.Trigger);

            handleRuleItem(inputData, 1); 
        }

        private void addToSummary(string id, string text, string subheader, string name, RuleType ruleType, bool error = false)
        {
            string icon = "";
            string bgColor = "";
            string color = "white";
            string header = "";

            if (error)
            {
                header = "Error";
                bgColor = "red";
                icon = "block";
            }
            else
            {
                switch (ruleType)
                {
                    case RuleType.Condition:
                        icon = "call_split";
                        bgColor = "#f30218";
                        header = $"Condition [{name}]";
                        break;
                    case RuleType.Function:
                        icon = "functions";
                        bgColor = "#fec203";
                        header = $"Function [{name}]";
                        break;
                    case RuleType.Output:
                        icon = "play_for_work";
                        bgColor = "#00ec39";
                        header = $"Output [{name}]";
                        break;
                    case RuleType.Trigger:
                        icon = "flash_on";
                        bgColor = "#007dff";
                        header = $"Trigger [{name}]";
                        break;
                }
            }

            Summary.Add(new EventItem()
            {
                Expandable = false,
                Header = header,
                Icon = icon,
                IconBgColor = bgColor,
                IconColor = color,
                Id = id,
                IsExpanded = false,
                SubHeader = subheader,
                Text = text,
                TimeStamp = (DateTime.Now - _evaluationStartTime).TotalMilliseconds.ToString() + " ms"
            });
        }

        /// <summary>
        /// LoopGuard returns true if loop should be terminated and false if everything is still ok
        /// </summary>
        private bool loopGuardActivated(int conditionId, ValueModel data)
        {
            var itm = _loopGuard.FirstOrDefault(x => x.Id == conditionId);

            if (itm == null)
            {
                _loopGuard.Add(new LoopGuardItem()
                {
                    Id = conditionId,
                    LoopNumber = 1,
                    Json = data.ToString()
                });
                return false;
            }

            if (data.ToString() != itm.Json) //data has changed -> continue looping until max count has been reached
            {
                if (itm.LoopNumber > _maxLoopCount)
                {
                    if (_createSummary)
                        addToSummary(conditionId.ToString(), $"LoopGuard stopped the rule evaluation process after max loop count {_maxLoopCount} was reached.", "LoopGuard - Process stopped", "error", RuleType.Trigger, true);
                    return true;
                }
                itm.LoopNumber++;
                return false;
            }

            if (_createSummary)
                addToSummary(conditionId.ToString(), "LoopGuard stopped the rule evaluation process due infinite loop.", "LoopGuard - Process stopped", "error", RuleType.Trigger, true);
            return true;
        }

        private void handleRuleItem(ValueModel data, int index)
        {
            if (_rule.RuleItems == null)
                return;

            var ruleItem = _rule.RuleItems[index];
            int nextIndex = index + 1;

            switch (ruleItem.RuleType)
            {
                case RuleType.Condition:
                    var rc = ruleItem as RuleCondition;
                    if (rc != null && !loopGuardActivated(rc.Id, data))
                    {
                        string conditionSummary = "";
                        int nextItem = -1;
                        try 
                        {
                            nextItem = evaluateConditionItem(rc as RuleCondition, data, index, ref conditionSummary);
                            if (_createSummary)
                                addToSummary(ruleItem.Id.ToString(), $"<b>Data:</b><br/> {data.ToJson()} <br/>  {conditionSummary} <br/> <b>Next:</b> {nextItem}", $"{rc.Description}", rc.Name, ruleItem.RuleType);

                        }
                        catch (Exception x) 
                        {
                            if (_createSummary)
                                addToSummary(ruleItem.Id.ToString(), $"Error: {x.Message}", "", rc.Name, ruleItem.RuleType, true);
                        }

                        if (nextItem >= 0 && _rule.RuleItems.Count > nextItem)
                            handleRuleItem(data, nextItem);
                    }
                    break;
                case RuleType.Function:
                    var rf = ruleItem as RuleFunction;
                    if (rf != null)
                    {
                        ValueModel inputData = new ValueModel("");
                        string inputParameters = "";
                        if (_createSummary)
                        {
                            if (rf.Parameters != null) 
                            {
                                foreach (var p in rf.Parameters)
                                    inputParameters += $"{p.Name}={p.Value};";
                            }
                            inputData = data;
                        }

                        var function = _functionService.GetFunction(rf.FunctionId);
                        if (function != null) 
                        {
                            try
                            {
                                data = function.Run(data, rf.Parameters);
                            }
                            catch (Exception ex) 
                            {
                                //terminate if error -> add to rule settings
                                if (_createSummary)
                                    addToSummary(ruleItem.Id.ToString(), $"Error: {ex.Message}", $"{rf.Description}", rf.Name, ruleItem.RuleType, true);
                            }
                        }

                        if (_createSummary)
                            addToSummary(ruleItem.Id.ToString(), $"Data: {inputData.ToString()}. Parameters: {inputParameters} <br/> <b>Result:</b> {data.ToString()}.", $"{rf.Description}", rf.Name, ruleItem.RuleType);

                        if (_rule.RuleItems.Count > nextIndex)
                            handleRuleItem(data, nextIndex);
                    }
                    break;
                case RuleType.Output:
                    var ro = ruleItem as RuleOutput;
                    if (ro != null)
                    {
                        ValueModel outputModel = data.Copy();
                        var result = outputModel;
                        var createdResult = false;
                        var command = _commands.FirstOrDefault(x => x.Id == ro.CommandId);
                        
                        var map = ro.DataModelMappingAsValue;
                        if (ro.UseMapping && map != null) //use mapping object from rule output
                        {
                            var cmdValueModel = new ValueModel(command.Model);
                            if(cmdValueModel != null)
                                result = new ValueModel(map.CreateMappedObjectJson(outputModel, cmdValueModel.Type));
                        }

                        if (command != null)
                        {
                            Results.Add(new RuleEvaluationResult()
                            {
                                CommandId = ro.CommandId,
                                Operation = ro.Operation,
                                Value = result,
                            });
                            createdResult = true;
                        }
                        else //Check if variable and handle it
                        {
                            var variable = _variables.FirstOrDefault(x => x.Id == ro.CommandId);
                            if (variable != null)
                            {
                                Results.Add(new RuleEvaluationResult()
                                {
                                    CommandId = ro.CommandId,
                                    Operation = OutputOperation.Variable,
                                    Value = result
                                });
                                createdResult = true;
                            }
                        }

                        if (_createSummary) 
                        {
                            var mapping = "No Mapping.";
                            if (ro.UseMapping)
                            {
                                mapping = $"Mapping model: {ro.DataModelMapping.ToJson()}";
                            }

                            if (createdResult)
                                addToSummary(ruleItem.Id.ToString(), $"Action: {ro.Operation.ToString()} <br/> {mapping} <br/> Data to output: {result.ToJson()}", $"{ro.Description}", ro.Name, ruleItem.RuleType);
                            else
                                addToSummary(ruleItem.Id.ToString(), $"Could not find Command or Variable with ID: {ro.CommandId}","ERROR", ro.Name, ruleItem.RuleType);
                        }
                           

                        if (ro.ContinueFlow) //if continue flow, get next item and handle it
                        {
                            if (_rule.RuleItems.Count > nextIndex)
                                handleRuleItem(data, nextIndex);
                        }
                    }
                    break;
            }
        }

        private int getRuleItemIndexById(int id)
        {
            if(_rule.RuleItems == null)
                return -1;

            for (int i = 0; i < _rule.RuleItems.Count; i++)
            {
                if (_rule.RuleItems[i].Id == id)
                    return i;
            }
            return -1;
        }

        private int evaluateConditionItem(RuleCondition condition, ValueModel data, int currentIndex, ref string summary)
        {
            if (condition.ConditionType == ConditionType.IfElse)
            {
                var clause = Json.InjectDataToText(data.ToJson(), condition.IfClause, "value");
                bool evalResult = Expression.Evaluate<bool>(clause);

                if (_createSummary)
                    summary = $"<b>Clause [{condition.IfClause}]: </b><br/>{clause} -> {evalResult}";

                if (evalResult)
                {
                    switch (condition.ThenOperator)
                    {
                        case FlowOperator.Continue:
                            return currentIndex + 1; //next item in list

                        case FlowOperator.Jump:
                            return getRuleItemIndexById(condition.ThenJumpTo);

                        case FlowOperator.Stop:
                            return -1; // negative number is stop
                    }
                }
                else
                {
                    switch (condition.ElseOperator)
                    {
                        case FlowOperator.Continue:
                            return currentIndex + 1; //next item in list

                        case FlowOperator.Jump:
                            return getRuleItemIndexById(condition.ElseJumpTo);

                        case FlowOperator.Stop:
                            return -1; // negative number is stop
                    }
                }
            }
            else //Switch
            {
                string switchValue = Json.InjectDataToText(data.ToString(), condition.SwitchVariable, "value");
                
                foreach (var switchCase in condition.SwitchCases)
                {
                    if (switchCase?.Case?.ToLower() == switchValue.ToLower())
                    {
                        switch (switchCase.Operation)
                        {
                            case FlowOperator.Continue:
                                return currentIndex + 1; //next item in list

                            case FlowOperator.Jump:
                                return getRuleItemIndexById(switchCase.JumpTo);

                            case FlowOperator.Stop:
                                return -1; // negative number is stop
                        }
                    }
                }
            }
            return -1;
        }
    }
}
