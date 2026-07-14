using RIoT2.Core.Functions;

using System;
using System.Collections.Generic;
using System.Linq;
using RIoT2.Core.Interfaces.Services;
using RIoT2.Core.Interfaces;

namespace RIoT2.Core.Services
{
    /// <summary>
    /// Default <see cref="IFunctionService"/> implementation that discovers and provides the
    /// built-in rule functions.
    /// </summary>
    public class FunctionService : IFunctionService
    {
        private List<IFunction> _functions;
        private IServiceProvider _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="FunctionService"/> class and loads the
        /// available functions.
        /// </summary>
        /// <param name="services">The service provider used to resolve dependencies for functions that require them.</param>
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

        /// <inheritdoc/>
        public IEnumerable<IFunction> GetFunctions()
        {
            return _functions;
        }

        /// <inheritdoc/>
        public IFunction GetFunction(string id)
        {
            return _functions.FirstOrDefault(x => x.Id == id);
        }
    }
}