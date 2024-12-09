using RIoT2.Core.Abstracts;
using RIoT2.Core.Interfaces;
using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Functions
{
    public class FuncGetReportValue : FunctionBase, IFunction
    {
        private IMessageStateService _stateService;
        public FuncGetReportValue(IMessageStateService stateService) : base(
            "65FE9358-18C2-4366-A22F-7CBD8E12E00D",
            "Read Report State",
            "Method reads defined device report and writes its value to datamodels property defined in modelParam",
            new List<ParameterTemplate>()
            {
                new ParameterTemplate()
                {
                    Name = "reportId",
                    IsOptional = false,
                    Type = ValueType.Text,
                    Description = "Report id to fetch"
                },
                new ParameterTemplate()
                {
                    Name = "modelParam",
                    IsOptional = true,
                    Type = ValueType.Text,
                    Description = "Datamodel parameter name. If not defined data is written to report"
                },
                new ParameterTemplate()
                {
                    Name = "reportParam",
                    IsOptional = true,
                    Type = ValueType.Text,
                    Description = "Report data parameter name. If not defined data is written as whole"
                }
        })
        {
            _stateService = stateService;
        }

        public ValueModel Run(ValueModel data, IEnumerable<Parameter> parameters)
        {
            string reportId = GetPropertyValue<string>(parameters, "reportId", data);
            string modelParam = GetPropertyValue<string>(parameters, "modelParam", data);
            string reportParam = GetPropertyValue<string>(parameters, "reportParam", data);

            if (string.IsNullOrEmpty(reportId))
                throw new System.Exception("Missing mandatory value 'reportId'");

            var report = _stateService.Reports.FirstOrDefault(x => x.Id == reportId);

            if (report == null) 
                return data;

            SetData(ref data, modelParam, "report", report.Value);

            return data;
        }
    }
}
