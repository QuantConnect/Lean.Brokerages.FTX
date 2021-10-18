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
using QuantConnect.FTXBrokerage.Messages;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using QuantConnect.Data.Fundamental;
using Order = QuantConnect.FTXBrokerage.Messages.Order;

namespace QuantConnect.FTXBrokerage
{
    public partial class FTXBrokerage
    {
        private readonly ManualResetEvent _onSubscribeEvent = new(false);
        private readonly ManualResetEvent _onUnsubscribeEvent = new(false);
        private ManualResetEvent _authResetEvent;
        private readonly ConcurrentDictionary<Symbol, DefaultOrderBook> _orderBooks = new();
        private readonly ConcurrentDictionary<int, decimal> _fills = new();

        /// <summary>
        /// Locking object for the Ticks list in the data queue handler
        /// </summary>
        private readonly object _tickLocker = new();

        /// <summary>
        /// Subscribes to the authenticated channels (using an single streaming channel)
        /// </summary>
        private void Authenticate()
        {
            if (string.IsNullOrEmpty(ApiKey) || string.IsNullOrEmpty(ApiSecret))
                return;

            WebSocket.Send(JsonConvert.SerializeObject(new
            {
                op = "login",
                args = _restApiClient.GenerateAuthPayloadForWebSocketApi()
            }));

            Log.Trace("FTXBrokerage.Auth(): Sent authentication request.");
        }

        private void OnUserDataImpl(WebSocketMessage webSocketMessage)
        {
            OnMessageImpl(webSocketMessage, (eventType, payload) =>
            {
                switch (eventType)
                {
                    case "error":
                        {
                            // expect this error message as confirmation that subscribed to private channel
                            if (payload["msg"]?.ToObject<string>() == "Already logged in")
                            {
                                _authResetEvent?.Set();
                            }

                            return;
                        }
                    case "update":
                        {
                            switch (payload["channel"]?.ToObject<string>()?.ToLowerInvariant())
                            {
                                case "fills":
                                    {
                                        OnOrderFill(payload.SelectToken("data")?.ToObject<Fill>());
                                        return;
                                    }
                                case "orders":
                                    {
                                        OnOrderExecution(payload.SelectToken("data")?.ToObject<BaseOrder>());
                                        return;
                                    }
                                default:
                                    return;
                            }
                        }
                }
            });
        }

        private void OnStreamDataImpl(WebSocketMessage webSocketMessage)
        {
            OnMessageImpl(webSocketMessage, (eventType, payload) =>
            {
                switch (eventType)
                {
                    case "partial":
                        {
                            OnSnapshot(
                                payload["market"]?.ToObject<string>(),
                                payload["data"]?.ToObject<Snapshot>());
                            return;
                        }
                    case "update":
                        {
                            switch (payload["channel"]?.ToObject<string>()?.ToLowerInvariant())
                            {
                                case "trades":
                                    {
                                        OnTrade(
                                            payload.SelectToken("market")?.ToObject<string>(),
                                            payload.SelectToken("data")?.ToObject<Trade[]>());
                                        return;
                                    }
                                case "orderbook":
                                    {
                                        OnOrderbookUpdate(
                                            payload.SelectToken("market")?.ToObject<string>(),
                                            payload.SelectToken("data")?.ToObject<OrderbookUpdate>());
                                        return;
                                    }
                                default:
                                    return;
                            }
                        }
                }
            });
        }

        private void OnMessageImpl(WebSocketMessage webSocketMessage, Action<string, JObject> action)
        {
            var e = (WebSocketClientWrapper.TextMessage)webSocketMessage.Data;
            try
            {
                if (Log.DebuggingEnabled)
                {
                    Log.Debug($"FTXBrokerage.OnMessageImpl(): {e.Message}");
                }

                var obj = JsonConvert.DeserializeObject<JObject>(e.Message, FTXRestApiClient.JsonSettings);

                var objEventType = obj?["type"]?.ToObject<string>()?.ToLowerInvariant();
                if (string.IsNullOrEmpty(objEventType))
                {
                    throw new Exception("Cannot find websocket message type");
                }
                switch (objEventType)
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
                            break;
                        }
                }

                action(objEventType, obj);
            }
            catch (Exception exception)
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1, $"Parsing wss message failed. Data: {e.Message} Exception: {exception}"));
                throw;
            }
        }

        private void OnTrade(string market, Trade[] trades)
        {
            try
            {
                var symbol = _symbolMapper.GetLeanSymbol(market, SecurityType.Crypto, Market.FTX);
                for (int i = 0; i < trades.Length; i++)
                {
                    var trade = trades[i];
                    EmitTradeTick(
                        symbol,
                        trade.Time,
                        trade.Price,
                        trade.Size);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void OnSnapshot(string market, Snapshot snapshot)
        {
            try
            {
                var symbol = _symbolMapper.GetLeanSymbol(market, SecurityType.Crypto, Market.FTX);

                if (!_orderBooks.TryGetValue(symbol, out var orderBook))
                {
                    orderBook = new DefaultOrderBook(symbol);
                    _orderBooks[symbol] = orderBook;
                }
                else
                {
                    orderBook.BestBidAskUpdated -= OnBestBidAskUpdated;
                    orderBook.Clear();
                }

                for (var i = 0; i < snapshot.Bids.Length; i++)
                {
                    var row = snapshot.Bids[i];
                    orderBook.UpdateBidRow(row[0], row[1]);
                }
                for (var i = 0; i < snapshot.Asks.Length; i++)
                {
                    var row = snapshot.Asks[i];
                    orderBook.UpdateAskRow(row[0], row[1]);
                }

                orderBook.BestBidAskUpdated += OnBestBidAskUpdated;

                EmitQuoteTick(symbol, orderBook.BestBidPrice, orderBook.BestBidSize, orderBook.BestAskPrice, orderBook.BestAskSize);
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void OnOrderbookUpdate(string market, OrderbookUpdate update)
        {
            try
            {
                var symbol = _symbolMapper.GetLeanSymbol(market, SecurityType.Crypto, Market.FTX);

                if (!_orderBooks.TryGetValue(symbol, out var orderBook))
                {
                    throw new Exception($"FTXBRokerage.OnOrderbookUpdate: orderbook is not initialized for {market}.");
                }

                for (var i = 0; i < update.Bids.Length; i++)
                {
                    var row = update.Bids[i];
                    if (row[1] == 0)
                    {
                        orderBook.RemoveBidRow(row[0]);
                        continue;
                    }

                    orderBook.UpdateBidRow(row[0], row[1]);
                }
                for (var i = 0; i < update.Asks.Length; i++)
                {
                    var row = update.Asks[i];
                    if (row[1] == 0)
                    {
                        orderBook.RemoveAskRow(row[0]);
                        continue;
                    }

                    orderBook.UpdateAskRow(row[0], row[1]);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void OnOrderExecution(BaseOrder order)
        {
            try
            {
                var brokerId = order.Id.ToStringInvariant();
                var foundOrder = _orderProvider.GetOrderByBrokerageId(brokerId);
                if (foundOrder == null)
                {
                    foundOrder = FindRelatedTriggerOrder(order.Id.ConvertInvariant<ulong>(), order.Market);

                    if (foundOrder == null)
                    {
                        Log.Error(
                            $"OnOrderExecution(): order not found. BrokerId: {brokerId}, New Status: {order.Status}");
                        return;
                    }
                }

                OrderStatus newStatus = ConvertOrderStatus(order);
                OnOrderEvent(new OrderEvent(foundOrder, DateTime.UtcNow, OrderFee.Zero, "FTX Cancel Order Event")
                {
                    Status = newStatus
                });
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void OnOrderFill(Fill fill)
        {
            try
            {
                var brokerId = fill.OrderId.ToStringInvariant();

                var order = _orderProvider.GetOrderByBrokerageId(brokerId);
                if (order == null)
                {
                    order = FindRelatedTriggerOrder(fill.OrderId.ConvertInvariant<ulong>(), fill.Market);

                    if (order == null)
                    {
                        Log.Error($"EmitFillOrder(): order not found: BrokerId: {brokerId}");
                        return;
                    }
                }

                var symbol = _symbolMapper.GetLeanSymbol(fill.Market, SecurityType.Crypto, Market.FTX);
                var fillPrice = fill.Price;
                var fillQuantity = fill.Quantity;
                var orderFee = new OrderFee(new CashAmount(Math.Abs(fill.Fee), fill.FeeCurrency));

                var status = OrderStatus.Filled;
                if (fillQuantity != order.Quantity)
                {
                    _fills.TryGetValue(order.Id, out var totalFillQuantity);
                    totalFillQuantity += fillQuantity;
                    _fills[order.Id] = totalFillQuantity;

                    status = totalFillQuantity == order.Quantity
                        ? OrderStatus.Filled
                        : OrderStatus.PartiallyFilled;
                }

                if (status == OrderStatus.Filled)
                {
                    _fills.TryRemove(order.Id, out _);
                }

                var orderEvent = new OrderEvent
                (
                    order.Id, symbol, fill.Time, status,
                    fill.Side, fillPrice, fillQuantity,
                    orderFee, $"FTX Order Event {fill.Side}"
                );

                OnOrderEvent(orderEvent);
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void EmitTradeTick(Symbol symbol, DateTime time, decimal price, decimal quantity)
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

        private void OnBestBidAskUpdated(object sender, BestBidAskUpdatedEventArgs e)
        {
            EmitQuoteTick(e.Symbol, e.BestBidPrice, e.BestBidSize, e.BestAskPrice, e.BestAskSize);
        }

        private void EmitQuoteTick(Symbol symbol, decimal bidPrice, decimal bidSize, decimal askPrice, decimal askSize)
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

        /// <summary>
        /// Emit stream tick
        /// </summary>
        /// <param name="tick"></param>
        private void EmitTick(Tick tick)
        {
            try
            {
                lock (_tickLocker)
                {
                    _aggregator.Update(tick);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        /// <summary>
        /// Trigger order execution creates new simple (market or limit)
        /// with different order id (not related to original trigger order id)
        /// New order execution also doesn't have any information to original trigger order id,
        /// so the only way to bind them is to use REST API
        /// https://docs.ftx.com/#get-trigger-order-triggers
        /// </summary>
        /// <param name="unknownOrderId"></param>
        /// <param name="market"></param>
        /// <returns></returns>
        private Orders.Order FindRelatedTriggerOrder(ulong unknownOrderId, string market)
        {
            var orderSymbol = _symbolMapper.GetLeanSymbol(market, SecurityType.Crypto, Market.FTX);
            var potentialOwnerConditionalOrders = _orderProvider
                .GetOpenOrders(o => (o.Type == OrderType.StopLimit || o.Type == OrderType.StopMarket) && o.Symbol.Equals(orderSymbol));

            for (var i = 0; i < potentialOwnerConditionalOrders.Count; i++)
            {
                var triggerOrder = potentialOwnerConditionalOrders[i];
                if (triggerOrder.BrokerId.Count > 1)
                {
                    continue;
                }

                var conditionalOrderId = triggerOrder.BrokerId.First();
                var triggers = _restApiClient.GetTriggers(conditionalOrderId.ConvertInvariant<ulong>());
                for (var j = 0; j < triggers.Count; j++)
                {
                    if (!triggers[j].OrderId.HasValue)
                    {
                        continue;
                    }

                    var relatedOrderId = triggers[j].OrderId?.ToStringInvariant();
                    if (!triggerOrder.BrokerId.Contains(relatedOrderId))
                    {
                        triggerOrder.BrokerId.Add(relatedOrderId);
                    }

                    if (triggers[j].OrderId.Value == unknownOrderId)
                    {
                        return triggerOrder;
                    }
                }
            }

            return null;
        }
    }
}
