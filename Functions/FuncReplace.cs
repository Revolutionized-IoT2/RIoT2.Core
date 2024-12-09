using System;
using System.Collections.Generic;
using RIoT2.Core.Abstracts;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;
using System.Text.RegularExpressions;

namespace RIoT2.Core.Functions
{
    public class FuncReplace : FunctionBase, IFunction
    {
        public FuncReplace() : base(
            "F7A3BFB7-8879-4C12-917F-3A66F989ED50",
            "Replace",
            "Method replaces defined parameters value according to pattern with value in replacement",
            new List<ParameterTemplate>()
            {
                new ParameterTemplate()
                {
                    Name = "modelParam",
                    IsOptional = true,
                    Type = ValueType.Text,
                    Description = "the parameter name in the datamodel which value is replaced. If not defined whole datamodel is replaced."
                },
                new ParameterTemplate()
                {
                    Name = "pattern",
                    IsOptional = true,
                    Type = ValueType.Text,
                    Description = "Regex pattern for selecting target from the value. If not defined whole value is replaced with newValue"
                },
                new ParameterTemplate()
                {
                    Name = "newValue",
                    IsOptional = false,
                    Type = ValueType.Text,
                    Description = "New value for replacing the target. Use syntax: {param} to refer a value in Datamodel"
                }
        }) { }

        public ValueModel Run(ValueModel data, IEnumerable<Parameter> parameters)
        {
            string modelParam = GetPropertyValue<string>(parameters, "modelParam", data);
            string pattern = GetPropertyValue<string>(parameters, "pattern", data);
            string newValue = GetPropertyValue<string>(parameters, "newValue", data);

            if (string.IsNullOrEmpty(newValue))
                throw new System.Exception("Missing mandatory value 'newValue'");

            return replace(data, modelParam, pattern, newValue);
        }

        private static ValueModel replace(ValueModel data, string modelParam, string pattern, string newValue)
        {
            var strToEdit = data.GetValue<string>(modelParam);

            if (!String.IsNullOrEmpty(strToEdit))
            {
                string replacedValue = Regex.Replace(strToEdit, pattern, newValue);
                data = data.Update(new ValueModel(replacedValue), modelParam);
            }
            return data;
        }
    }
}
