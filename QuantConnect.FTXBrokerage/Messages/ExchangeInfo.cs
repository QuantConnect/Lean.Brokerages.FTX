namespace QuantConnect.FTXBrokerage.Messages
{
    public class ExchangeInfo
    {
        public string Name { get; set; }
        public string BaseCurrency { get; set; }
        public string QuoteCurrency { get; set; }
        public decimal QuoteVolume24h { get; set; }
        public decimal Change1h { get; set; }
        public decimal Change24h { get; set; }
        public decimal ChangeBod { get; set; }
        public bool HighLeverageFeeExempt { get; set; }
        public decimal MinProvideSize { get; set; }
        public string Type { get; set; }
        public string Underlying { get; set; }
        public bool Enabled { get; set; }
        public decimal Ask { get; set; }
        public decimal Bid { get; set; }
        public decimal Last { get; set; }
        public bool PostOnly { get; set; }
        public decimal Price { get; set; }
        public decimal PriceIncrement { get; set; }
        public decimal SizeIncrement { get; set; }
        public string Restricted { get; set; }
        public decimal VolumeUsd24h { get; set; }
    }
}