using System;

namespace QuantConnect.FTXBrokerage.Messages
{
    public class Candle
    {
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public DateTime StartTime { get; set; }
        public decimal Volume { get; set; }
    }
}