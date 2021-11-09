/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.FTXBrokerage.Messages;
using System;
using Order = QuantConnect.FTXBrokerage.Messages.Order;

namespace QuantConnect.FTXBrokerage.Converters
{
    /// <summary>
    /// A custom JSON converter for the FTX <see cref="Order"/> class
    /// </summary>
    public class OrderConverter : JsonConverter
    {
        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        /// 	<c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type objectType) => typeof(BaseOrder).IsAssignableFrom(objectType);

        /// <summary>Reads the JSON representation of the object.</summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader" /> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        /// <returns>The object value.</returns>
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
                case "take_profit":
                    order = new TriggerOrder
                    {
                        OrderType = jObject["orderType"]?.ToObject<string>(),
                        OrderPrice = TryParse<decimal>(jObject["orderPrice"]),
                        TriggerPrice = TryParse<decimal>(jObject["triggerPrice"])
                    };
                    break;
                case "trailing_stop":
                default:
                    throw new InvalidCastException($"Order type cannot be converted. Order type: {jObject.ToObject<string>()}");
            }

            serializer.Populate(jObject.CreateReader(), order);

            return order;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="T:Newtonsoft.Json.JsonConverter" /> can write JSON.
        /// </summary>
        /// <value><c>true</c> if this <see cref="T:Newtonsoft.Json.JsonConverter" /> can write JSON; otherwise, <c>false</c>.</value>
        public override bool CanWrite => false;

        /// <summary>Writes the JSON representation of the object.</summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter" /> to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
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