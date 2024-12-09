using System;
using System.Collections.Generic;
using RIoT2.Core.Abstracts;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;

namespace RIoT2.Core.Functions
{
    public class FuncAdd : FunctionBase, IFunction
    {
        public FuncAdd() : base(
            "A47453F5-B67C-4431-B4BB-37D6244E297C",
            "Add",
            "Method adds defined amount to parameter",
            new List<ParameterTemplate>()
            {
                new ParameterTemplate()
                {
                    Name = "modelParam",
                    IsOptional = false,
                    Type = ValueType.Text,
                    Description = "the parameter name in the datamodel to be added"
                },
                new ParameterTemplate()
                {
                    Name = "amount",
                    IsOptional = false,
                    Type = ValueType.Number,
                    Description = "amount to be added to modelParam"
                }
        }) { }

        public ValueModel Run(ValueModel data, IEnumerable<Parameter> parameters)
        {
            //if (!ContainsAllExpectedParameters(parameters))
            //    throw new Exception("Missing mandatory parameter(s)");

            string paramNameInData = GetPropertyValue<string>(parameters, "modelParam", data);
            double amount = GetPropertyValue<double>(parameters, "amount", data);

            if(paramNameInData == null || amount == default)
                throw new Exception("Missing mandatory parameter(s) or parameter(s) invalid");

            return add(data, paramNameInData, amount);
        }

        private static ValueModel add(ValueModel data, string paramNameInData, double amount)
        {
            if (data == null)
                return data;

            var result = data.GetValue<double>(paramNameInData);

            result = result + amount;
            return data.Update(new ValueModel(result), paramNameInData);
        }
    }
}
