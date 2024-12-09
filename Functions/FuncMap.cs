using System.Collections.Generic;
using RIoT2.Core.Abstracts;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Models;

namespace RIoT2.Core.Functions
{
    public class FuncMap : FunctionBase, IFunction
    {
        public FuncMap() : base(
            "5E43086F-4258-4F74-942A-C62DF8CAB9AE",
            "Map",
            "Method maps incoming Datamodel to new Datamodel",
            new List<ParameterTemplate>()
            {
                new ParameterTemplate()
                {
                    Name = "target",
                    IsOptional = false,
                    Type = ValueType.Entity,
                    Description = "The target datamodel in json format"
                },
                new ParameterTemplate()
                {
                    Name = "map",
                    IsOptional = false,
                    Type = ValueType.TextArray,
                    Description = "Array of mapping rules. Format: [datamodel.path=target.property]"
                }
        }) { }

        public ValueModel Run(ValueModel data, IEnumerable<Parameter> parameters)
        {
            ValueModel target = GetPropertyValueAsDataModel<object>(parameters, "target", data);
            ValueModel map = GetPropertyValueAsDataModel<object>(parameters, "map", data);

            if (target == null || map == null)
                throw new System.Exception("Missing mandatory parameter(s)");

            if(target.Type != ValueType.Entity)
                throw new System.Exception("Target is not valid.");

            foreach (var key in map.GetStrings()) 
            {
                var keyArr = key.Split('=');
                if (keyArr.Length != 2)
                    continue;

                var value = data.GetValue<string>(keyArr[0]);
                target = target.Update(new ValueModel(value), keyArr[1]);
            }
            
            return target;
        }
    }
}
