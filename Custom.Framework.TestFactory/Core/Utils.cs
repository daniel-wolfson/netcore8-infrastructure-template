using Custom.Framework.Models;
using Newtonsoft.Json;
using Serilog;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Custom.Framework.TestFactory.Core
{
    public class Utils
    {
        public static void ThrowRandomException(string? exceptionMessage = null)
        {
            var diceRoll = new Random().Next(0, 10);

            if (diceRoll > 5)
            {
                var errMsg = exceptionMessage ?? "Test exception error";
                Console.WriteLine(errMsg);
                throw new Exception(errMsg);
            }
        }
    }
}