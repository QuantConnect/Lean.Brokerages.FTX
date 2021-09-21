using System;
using Newtonsoft.Json;
using QuantConnect.Orders;

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
        public OrderDirection Side { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public bool ReduceOnly { get; set; }
        public string ClientId { get; set; }

        public decimal Quantity => Side == OrderDirection.Buy ? Size : -Size;
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

    public class Snapshot
    {
        public double Time { get; set; }
        public decimal[][] Bids { get; set; }
        public decimal[][] Asks { get; set; }
    }

    public class OrderbookUpdate
    {
        public double Time { get; set; }
        public decimal[][] Bids { get; set; }
        public decimal[][] Asks { get; set; }
    }

    public class Trade
    {
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public OrderDirection Side { get; set; }
        public DateTime Time { get; set; }
        public decimal Quantity => Side == OrderDirection.Buy ? Size : -Size;
    }

    public class Fill
    {

    }

#pragma warning restore 1591
}