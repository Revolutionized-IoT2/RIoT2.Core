using RIoT2.Core.Models;
using RIoT2.Core.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Core.Services
{
    /// <summary>
    /// In-memory <see cref="ICodeProviderService"/> implementation that issues and validates
    /// time- and usage-limited codes.
    /// </summary>
    public class CodeProviderService : ICodeProviderService
    {
        private readonly List<DeviceCode> _codes;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeProviderService"/> class.
        /// </summary>
        public CodeProviderService() 
        {
            _codes = new List<DeviceCode>();
        }

        /// <inheritdoc/>
        public string CreateCode(int? timesValid = null, DateTime? from = null, DateTime? to = null)
        {
            var newCode = Guid.NewGuid().ToString().ToLower();
            lock (_lock)
            {
                _codes.Add(new DeviceCode()
                {
                    From = from,
                    TimesUsed = 0,
                    TimesValid = timesValid,
                    To = to,
                    Code = newCode
                });
            }
            return newCode;
        }

        /// <inheritdoc/>
        public bool UseCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return false;

            var normalizedCode = code.ToLower();

            lock (_lock)
            {
                var c = _codes.FirstOrDefault(x => x.Code == normalizedCode);
                if (c == null)
                    return false;

                if (c.IsValid)
                {
                    c.TimesUsed++;
                    return true;
                }
                else // code is not valid anymore -> remove it from list
                {
                    _codes.Remove(c);
                }
                return false;
            }
        }
    }
}
