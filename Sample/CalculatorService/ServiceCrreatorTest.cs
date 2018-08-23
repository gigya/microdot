using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalculatorService.Interface;

namespace CalculatorService
{
    public class ServiceCrreatorTest
    {
        private readonly Func<ICalculatorService> _calculatorService;

        public ServiceCrreatorTest(Func<ICalculatorService> calculatorService)
        {
            _calculatorService = calculatorService;
        }

        public ICalculatorService GetCalculatorService()
        {
            return _calculatorService();
        }
    }
}
