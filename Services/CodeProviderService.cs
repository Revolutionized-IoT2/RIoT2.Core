using RIoT2.Core.Models;
using RIoT2.Core.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RIoT2.Common.Services
{
    public class CodeProviderService : ICodeProviderService
    {
        private List<DeviceCode> _codes;

        public CodeProviderService() 
        {
            _codes = new List<DeviceCode>();
        }

        public string CreateCode(int? timesValid = null, DateTime? from = null, DateTime? to = null)
        {
            var newCode = Guid.NewGuid().ToString().ToLower();
            _codes.Add(new DeviceCode()
            {
                From = from,
                TimesUsed = 0,
                TimesValid = timesValid,
                To = to,
                Code = newCode
            });
            return newCode;
        }

        public bool UseCode(string code)
        {
            var c = _codes.FirstOrDefault(x => x.Code == code.ToLower());
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
