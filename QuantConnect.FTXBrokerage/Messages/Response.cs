namespace QuantConnect.FTXBrokerage.Messages
{
    public class Response<T>
    {
        public bool Success { get; set; }
        public T Result { get; set; }
    }
}