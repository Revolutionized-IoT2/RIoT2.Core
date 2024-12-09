using System;
using System.Collections.Generic;
using System.Globalization;
using RIoT2.Core.Abstracts;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;

namespace RIoT2.Core.Functions
{
    public class FuncGetCode : FunctionBase, IFunction
    {
        private readonly ICodeProviderService _codeProviderService;
        public FuncGetCode(ICodeProviderService codeProviderService) : base(
            "DA8A5E28-CEAD-4CEA-A976-9134D018F975",
            "GetCode",
            "Gets a code that can be used to validate external commands",
            new List<ParameterTemplate>()
            {
                new ParameterTemplate()
                {
                    Name = "modelParam",
                    IsOptional = true,
                    Type = ValueType.Text,
                    Description = "Defines parameter in data where set code value. If none is defined, parameter 'code' is added to root"
                },
                new ParameterTemplate()
                {
                    Name = "timesValid",
                    IsOptional = true,
                    Type = ValueType.Number,
                    Description = "Defines how many times code can be used. No value, no limit"
                },
                new ParameterTemplate()
                {
                    Name = "from",
                    IsOptional = true,
                    Type = ValueType.Text,
                    Description = "Defines code validity start time. Format: yyyy-MM-dd HH:mm"
                },
                new ParameterTemplate()
                {
                    Name = "to",
                    IsOptional = true,
                    Type = ValueType.Text,
                    Description = "Defines code validity¨end time. Format: yyyy-MM-dd HH:mm"
                }
        }) {
            _codeProviderService = codeProviderService;
        }

        public ValueModel Run(ValueModel data, IEnumerable<Parameter> parameters)
        {
            string modelParam = GetPropertyValue<string>(parameters, "modelParam", data);
            int tv = GetPropertyValue<int>(parameters, "timesValid", data);
            string fromStr = GetPropertyValue<string>(parameters, "from", data);
            string toStr = GetPropertyValue<string>(parameters, "to", data);

            DateTime? to = toDateTime(toStr);
            DateTime? from = toDateTime(fromStr);
            int? timesValid = tv != default(int) ? (int?)tv : null;

            //no mandatory values...

            return getCode(data, modelParam, from, to, timesValid);
        }

        private ValueModel getCode(ValueModel data, string modelParam, DateTime? from, DateTime? to, int? timesValid)
        {
            var code = _codeProviderService.CreateCode(timesValid, from, to);

            SetData(ref data, modelParam, "code", new ValueModel(code));

            return data;
        }

        private static DateTime? toDateTime(string s)
        {
            if (String.IsNullOrEmpty(s))
                return null;

            DateTime dateTime;

            if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
                return dateTime;

            return null;
        }
    }
}
