using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect.FTXBrokerage
{
#pragma warning disable 1591

    public class Response<T>
    {
        public bool Success { get; set; }
        public T Result { get; set; }
    }

    public class Balance
    {
        public string Coin { get; set; }
        public decimal Free { get; set; }
        public decimal Total { get; set; }
    }


#pragma warning restore 1591
}