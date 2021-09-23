using Newtonsoft.Json;
using System;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using QuantConnect.FTXBrokerage.Messages;
using QuantConnect.Orders;
using Order = QuantConnect.FTXBrokerage.Messages.Order;

namespace QuantConnect.FTXBrokerage.Converters
{
    public class OrderConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(BaseOrder).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);
            
            if (!jObject.TryGetValue("type", StringComparison.OrdinalIgnoreCase, out var token))
            {
                throw new InvalidCastException($"Json object cannot be converted to FTX Order. Data: {jObject}");
            }
            BaseOrder order;

            switch (token.ToObject<string>()?.ToLowerInvariant())
            {
                case "limit":
                case "market":
                    order = new Order
                    {
                        Price = TryParse<decimal>(jObject["price"]),
                        PostOnly = TryParse<bool>(jObject["postOnly"]),
                        ImmediateOrCancel = TryParse<bool>(jObject["ioc"])
                    };
                    break;
                case "stop":
                case "trailing_stop":
                case "take_profit":
                    order = new TriggerOrder
                    {
                        OrderType = jObject["price"]?.ToObject<string>(),
                        OrderPrice = TryParse<decimal>(jObject["orderPrice"]),
                        TriggerPrice = TryParse<decimal>(jObject["triggerPrice"])
                    };
                    break;

                default:
                    throw new InvalidCastException($"Order type cannot be converted. Order type: {jObject.ToObject<string>()}");
            }

            serializer.Populate(jObject.CreateReader(), order);

            return order;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        private T TryParse<T>(JToken jToken)
        {
            if (jToken == null || jToken.Type == JTokenType.Null)
            {
                return default;
            }

            return jToken.ToObject<T>();
        }
    }
}