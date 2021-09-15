using System;
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
        public decimal Total { get; set; }
    }

    public abstract class BaseOrder
    {
        public ulong Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Market { get; set; }
        public decimal Size { get; set; }
        public decimal FilledSize { get; set; }
        public decimal RemainingSize { get; set; }
        public string Side { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public bool ReduceOnly { get; set; }
        public string ClientId { get; set; }

        public decimal Quantity => string.Equals(Side, "buy", StringComparison.OrdinalIgnoreCase) ? Size : -Size;
    }

    public class Order : BaseOrder
    {
        public decimal Price { get; set; }
        [JsonProperty("ioc")]
        public bool ImmediateOrCancel { get; set; }
        public bool PostOnly { get; set; }
    }

    public class TriggerOrder : BaseOrder
    {
        public string OrderType { get; set; }
        public decimal OrderPrice { get; set; }
        public decimal TriggerPrice { get; set; }
    }
#pragma warning restore 1591
}