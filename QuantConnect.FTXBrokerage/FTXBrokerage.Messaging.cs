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
using QuantConnect.Brokerages;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using System;
using System.Threading;

namespace QuantConnect.FTXBrokerage
{
    public partial class FTXBrokerage
    {
        private ManualResetEvent _onSubscribeEvent = new(false);
        private ManualResetEvent _onUnsubscribeEvent = new(false);

        /// <summary>
        /// Locking object for the Ticks list in the data queue handler
        /// </summary>
        protected readonly object TickLocker = new object();

        private bool SubscribeChannel(string channel, Symbol symbol)
        {
            _onSubscribeEvent.Reset();

            WebSocket.Send(JsonConvert.SerializeObject(new
            {
                op = "subscribe",
                channel,
                market = _symbolMapper.GetBrokerageSymbol(symbol)
            }, FTXRestApiClient.JsonSettings));

            if (!_onSubscribeEvent.WaitOne(TimeSpan.FromSeconds(30)))
            {
                Log.Error($"FTXBrokerage.Subscribe(): Could not subscribe to {symbol.Value}/{channel}.");
                return false;
            }

            return true;
        }

        private bool UnsubscribeChannel(string channel, Symbol symbol)
        {
            _onUnsubscribeEvent.Reset();

            WebSocket.Send(JsonConvert.SerializeObject(new
            {
                op = "unsubscribe",
                channel,
                market = _symbolMapper.GetBrokerageSymbol(symbol)
            }, FTXRestApiClient.JsonSettings));

            if (!_onUnsubscribeEvent.WaitOne(TimeSpan.FromSeconds(30)))
            {
                Log.Error($"FTXBrokerage.Unsubscribe(): Could not unsubscribe from {symbol.Value}/{channel}.");
                return false;
            }

            return true;
        }

        private void OnMessageImpl(WebSocketMessage webSocketMessage)
        {
            var e = (WebSocketClientWrapper.TextMessage)webSocketMessage.Data;
            try
            {
                var obj = JObject.Parse(e.Message);

                var objEventType = obj["type"];
                switch (objEventType?.ToObject<string>()?.ToLowerInvariant())
                {
                    case "pong":
                        {
                            return;
                        }

                    case "subscribed":
                        {
                            _onSubscribeEvent.Set();
                            return;
                        }

                    case "unsubscribed":
                        {
                            _onUnsubscribeEvent.Set();
                            return;
                        }

                    case "error":
                        {
                            // status code 400 - already subscribed
                            if (obj["msg"]?.ToObject<string>() == "Already subscribed")
                            {
                                _onSubscribeEvent.Set();
                            }
                            return;
                        }

                    case "update":
                        {
                            OnDataUpdate(obj);
                            return;
                        }

                    case "partial":
                        {
                            OnSnapshot(obj);
                            return;
                        }

                    default:
                        {
                            return;
                        }
                }
            }
            catch (Exception exception)
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1, $"Parsing wss message failed. Data: {e.Message} Exception: {exception}"));
                throw;
            }
        }

        private void OnDataUpdate(JObject obj)
        {
            switch (obj["channel"]?.ToObject<string>()?.ToLowerInvariant())
            {
                case "trades":
                    {
                        var trade = obj.ToObject<dynamic>();
                        EmitTradeTick(
                            _symbolMapper.GetLeanSymbol(trade.Symbol, SecurityType.Crypto, Market.Binance),
                            Time.UnixMillisecondTimeStampToDateTime(trade.Time),
                            trade.Price,
                            trade.Quantity);
                        return;
                    }
                case "orderbook":
                    {
                        var quote = obj.ToObject<dynamic>();
                        EmitQuoteTick(
                            _symbolMapper.GetLeanSymbol(quote.Symbol, SecurityType.Crypto, Market.Binance),
                            quote.BestBidPrice,
                            quote.BestBidSize,
                            quote.BestAskPrice,
                            quote.BestAskSize);
                        return;
                    }
            }
        }

        private void OnSnapshot(JObject obj)
        {

            var quote = obj.ToObject<dynamic>();
            EmitQuoteTick(
                _symbolMapper.GetLeanSymbol(quote.Symbol, SecurityType.Crypto, Market.Binance),
                quote.BestBidPrice,
                quote.BestBidSize,
                quote.BestAskPrice,
                quote.BestAskSize);
        }

        private void EmitTradeTick(Symbol symbol, DateTime time, decimal price, decimal quantity)
        {
            try
            {
                lock (TickLocker)
                {
                    EmitTick(new Tick
                    {
                        Value = price,
                        Time = time,
                        Symbol = symbol,
                        TickType = TickType.Trade,
                        Quantity = Math.Abs(quantity)
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void EmitQuoteTick(Symbol symbol, decimal bidPrice, decimal bidSize, decimal askPrice, decimal askSize)
        {
            try
            {
                lock (TickLocker)
                {
                    EmitTick(new Tick
                    {
                        AskPrice = askPrice,
                        BidPrice = bidPrice,
                        Time = DateTime.UtcNow,
                        Symbol = symbol,
                        TickType = TickType.Quote,
                        AskSize = askSize,
                        BidSize = bidSize
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        /// <summary>
        /// Emit stream tick
        /// </summary>
        /// <param name="tick"></param>
        private void EmitTick(Tick tick)
        {
            _aggregator.Update(tick);
        }
    }
}
