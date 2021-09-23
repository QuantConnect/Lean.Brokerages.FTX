namespace QuantConnect.FTXBrokerage.Messages
{
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
}