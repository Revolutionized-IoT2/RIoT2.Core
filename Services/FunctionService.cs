using RIoT2.Core.Functions;

using System;
using System.Collections.Generic;
using System.Linq;
using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Interfaces;

namespace RIoT2.Core.Services
{
    public class FunctionService : IFunctionService
    {
        private List<IFunction> _functions;
        private IServiceProvider _services;

        public FunctionService(IServiceProvider services)
        {
            _services = services;
            _functions = new List<IFunction>();
            loadFunctions();
        }

        private void loadFunctions()
        {
            IMessageStateService deviceStateService = (IMessageStateService)_services.GetService(typeof(IMessageStateService));

            _functions.Add(new FuncReplace());
            _functions.Add(new FuncGetDatetime());
            _functions.Add(new FuncAdd());
           // _functions.Add(new FuncGetCode());

            if(deviceStateService != null)
                _functions.Add(new FuncGetReportValue(deviceStateService));
        }

        public IEnumerable<IFunction> GetFunctions()
        {
            return _functions;
        }

        public IFunction GetFunction(string id)
        {
            return _functions.FirstOrDefault(x => x.Id == id);
        }
    }
}