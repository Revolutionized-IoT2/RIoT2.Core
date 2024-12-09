using System;
using System.Collections.Generic;
using RIoT2.Core.Abstracts;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;

namespace RIoT2.Core.Functions
{
    public class FuncGetDatetime : FunctionBase, IFunction
    {
        public FuncGetDatetime() : base(
            "89D0E33B-7E9F-4096-BEF8-1F33D9F2A438",
            "GetDatetime",
            "Method writes current datetime into defined parameter. If no parameter is defined, parameter datetime is added.",
            new List<ParameterTemplate>()
            {
                new ParameterTemplate()
                {
                    Name = "modelParam",
                    IsOptional = true,
                    Type = ValueType.Text,
                    Description = "parameter for datetime"
                },
                new ParameterTemplate()
                {
                    Name = "isEpoch",
                    IsOptional = true,
                    Type = ValueType.Boolean,
                    Description = "If set to true, epoch time is returned. If not provided value defaults to false"
                }
        }) { }

        public ValueModel Run(ValueModel data, IEnumerable<Parameter> parameters)
        {
            string paramNameInData = GetPropertyValue<string>(parameters, "modelParam", data);
            bool isEpoch = GetPropertyValue<bool>(parameters, "isEpoch", data);

            return getDatetime(data, paramNameInData, isEpoch);
        }

        private static ValueModel getDatetime(ValueModel data, string param, bool isEpoch)
        {
            string result = "";
            if (isEpoch)
                result = DateTime.Now.ToEpoch().ToString();
            else 
                result = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            SetData(ref data, param, "datetime", new ValueModel(result));

            return data;
        }
    }
}
