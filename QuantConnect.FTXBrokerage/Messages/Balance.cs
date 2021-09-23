namespace QuantConnect.FTXBrokerage.Messages
{
    public class Balance
    {
        public string Coin { get; set; }
        public decimal Total { get; set; }
    }
}