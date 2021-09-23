using System;
using QuantConnect.Orders;

namespace QuantConnect.FTXBrokerage.Messages
{
    public class Trade
    {
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public OrderDirection Side { get; set; }
        public DateTime Time { get; set; }
        public decimal Quantity => Side == OrderDirection.Buy ? Size : -Size;
    }
}