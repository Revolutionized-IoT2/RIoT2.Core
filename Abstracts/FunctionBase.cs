using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Abstracts
{
    public abstract class FunctionBase
    {
        public FunctionBase(string id, string name, string description, List<ParameterTemplate> expectedParameters)
        {
            Id = id;
            Name = name;
            Description = description;
            ExpectedParameters = expectedParameters;
        }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public IEnumerable<ParameterTemplate> ExpectedParameters { get; private set; }
        protected T GetPropertyValue<T>(IEnumerable<Parameter> parameters, string name, ValueModel data)
        {
            var datamodel = GetPropertyValueAsDataModel<T>(parameters, name, data);
            if (datamodel == null)
                return default;

            return datamodel.GetValue<T>();
        }

        protected ValueModel GetPropertyValueAsDataModel<T>(IEnumerable<Parameter> parameters, string name, ValueModel data)
        {
            var parameter = parameters.FirstOrDefault(p => p.Name == name);

            if (parameter == null)
                return null;

            var expectedParameter = ExpectedParameters.FirstOrDefault(p => p.Name == name);

            if (expectedParameter == null)
                return null;

            ValueModel parameterDataModel = new ValueModel(parameter.Value);

            string variable = parameterDataModel.GetVariable();

            if (expectedParameter.Type != parameterDataModel.Type && String.IsNullOrEmpty(variable))
                return null;

            if (variable != null) //if parameter is variable-> find value from data
            {
                return new ValueModel(data.GetValue<T>(variable));
            }
            else // data is not variable -> Use value as is
            {
                return parameterDataModel;
            }
        }
        protected bool ContainsAllExpectedParameters(IEnumerable<Parameter> parameters)
        {
            foreach (var p in parameters)
            {
                if (ExpectedParameters.Where(x => !x.IsOptional).FirstOrDefault(x => x.Name == p.Name) == null)
                    return false;
            }
            return true;
        }

        protected static void SetData(ref ValueModel model, string param, string defaultName, ValueModel value)
        {
            if (model.Type == ValueType.Entity)
            {
                model = model.Update(value, String.IsNullOrEmpty(param) ? defaultName : param);
            }
            else 
            {
                model = new ValueModel(value.ToString());
            }
        }
    }
}
