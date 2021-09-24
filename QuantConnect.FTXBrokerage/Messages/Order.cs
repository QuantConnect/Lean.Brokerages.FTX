using System;
using Newtonsoft.Json;
using QuantConnect.Orders;

namespace QuantConnect.FTXBrokerage.Messages
{
    [JsonConverter(typeof(Converters.OrderConverter))]
    public abstract class BaseOrder
    {
        public ulong Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Market { get; set; }
        public decimal Size { get; set; }
        public decimal FilledSize { get; set; }
        public decimal RemainingSize { get; set; }
        public OrderDirection Side { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public bool ReduceOnly { get; set; }
        public string ClientId { get; set; }

        public decimal Quantity => Side == OrderDirection.Buy ? Size : -Size;
    }

    public class Order : BaseOrder
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public decimal Price { get; set; }
        [JsonProperty("ioc")]
        public bool ImmediateOrCancel { get; set; }
        public bool PostOnly { get; set; }
    }

    public class TriggerOrder : BaseOrder
    {
        public string OrderType { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public decimal OrderPrice { get; set; }
        public decimal TriggerPrice { get; set; }
    }
}