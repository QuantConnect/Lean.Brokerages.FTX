using System;
using QuantConnect.Orders;

namespace QuantConnect.FTXBrokerage.Messages
{
    public class Fill
    {
        public string Market { get; set; }
        public string BaseCurrency { get; set; }
        public string QuoteCurrency { get; set; }
        public string Type { get; set; }
        public OrderDirection Side { get; set; }
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public long OrderId { get; set; }
        public DateTime Time { get; set; }
        public string FeeCurrency { get; set; }
        public decimal Fee { get; set; }
        public decimal FeeRate { get; set; }
        public decimal Quantity => Side == OrderDirection.Buy ? Size : -Size;
    }
}