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

using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.FTXBrokerage.Messages;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using QuantConnect.Configuration;
using DateTime = System.DateTime;
using HistoryRequest = QuantConnect.Data.HistoryRequest;
using LimitOrder = QuantConnect.Orders.LimitOrder;
using MarketOrder = QuantConnect.Orders.MarketOrder;
using Order = QuantConnect.FTXBrokerage.Messages.Order;
using Timer = System.Timers.Timer;

namespace QuantConnect.FTXBrokerage
{
    [BrokerageFactory(typeof(FTXBrokerageFactory))]
    public partial class FTXBrokerage : BaseWebsocketsBrokerage, IDataQueueHandler, IDataQueueUniverseProvider
    {
        private const string RestApiUrl = "https://ftx.com/api";
        private const string WsApiUrl = "wss://ftx.com/ws/";

        private bool _isAuthenticated;

        private readonly LiveNodePacket _job;
        private readonly IDataAggregator _aggregator;
        private readonly IOrderProvider _orderProvider;
        private readonly ISecurityProvider _securityProvider;
        private readonly BrokerageConcurrentMessageHandler<WebSocketMessage> _messageHandler;
        private readonly SymbolPropertiesDatabaseSymbolMapper _symbolMapper = new(Market.FTX);
        private readonly Timer _keepAliveTimer;
        private readonly FTXRestApiClient _restApiClient;

        /// <summary>
        /// Returns true if we're currently connected to the broker
        /// </summary>
        public override bool IsConnected => WebSocket.IsOpen && _isAuthenticated;

        /// <summary>
        /// Creates a new <see cref="FTXBrokerage"/> from the specified values retrieving data from configuration file
        /// </summary>
        /// <param name="orderProvider">The order provider</param>
        /// <param name="securityProvider">The security provider</param>
        /// <param name="job">The job packet</param>
        public FTXBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider, IDataAggregator aggregator, LiveNodePacket job) : this(
            Config.Get("ftx-api-key"),
            Config.Get("ftx-api-secret"),
            orderProvider,
            securityProvider,
            aggregator,
            job)
        { }

        public FTXBrokerage(string apiKey, string apiSecret, IAlgorithm algorithm, IDataAggregator aggregator, LiveNodePacket job) : this(
            apiKey,
            apiSecret,
            algorithm?.Transactions,
            algorithm?.Portfolio,
            aggregator,
            job)
        { }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="apiKey">api key</param>
        /// <param name="apiSecret">api secret</param>
        /// <param name="algorithm">the algorithm instance is required to retrieve account type</param>
        /// <param name="aggregator">consolidate ticks</param>
        /// <param name="job">The live job packet</param>
        public FTXBrokerage(string apiKey, string apiSecret, IOrderProvider orderProvider, ISecurityProvider securityProvider, IDataAggregator aggregator, LiveNodePacket job) : base(
            WsApiUrl,
            new WebSocketClientWrapper(),
            new RestClient(RestApiUrl),
            apiKey,
            apiSecret,
            "FTX")
        {
            _orderProvider = orderProvider;
            _securityProvider = securityProvider;
            _job = job;
            _aggregator = aggregator;
            var subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();
            subscriptionManager.SubscribeImpl += (s, t) =>
            {
                return SubscribeImpl(s);
            };
            subscriptionManager.UnsubscribeImpl += (s, t) => Unsubscribe(s);

            SubscriptionManager = subscriptionManager;

            // Send pings at regular intervals (every 15 seconds)
            _keepAliveTimer = new Timer
            {
                Interval = 15 * 1000
            };
            _keepAliveTimer.Elapsed += (s, e) =>
            {
                WebSocket.Send("{\"op\": \"ping\"}");
            };

            WebSocket.Open += (s, e) =>
            {
                Authenticate();
                _keepAliveTimer.Start();
            };
            WebSocket.Closed += (s, e) => { _keepAliveTimer.Stop(); };

            // Useful for some brokerages:

            // Brokerage helper class to lock websocket message stream while executing an action, for example placing an order
            // avoid race condition with placing an order and getting filled events before finished placing
            _messageHandler = new BrokerageConcurrentMessageHandler<WebSocketMessage>(OnMessageImpl);

            _restApiClient = new FTXRestApiClient(RestClient, apiKey, apiSecret);
        }

        #region IDataQueueHandler

        /// <summary>
        /// Subscribe to the specified configuration
        /// </summary>
        /// <param name="dataConfig">defines the parameters to subscribe to a data feed</param>
        /// <param name="newDataAvailableHandler">handler to be fired on new data available</param>
        /// <returns>The new enumerator for this subscription request</returns>
        public IEnumerator<BaseData> Subscribe(SubscriptionDataConfig dataConfig, EventHandler newDataAvailableHandler)
        {
            if (!CanSubscribe(dataConfig.Symbol))
            {
                return Enumerable.Empty<BaseData>().GetEnumerator();
            }

            var enumerator = _aggregator.Add(dataConfig, newDataAvailableHandler);
            SubscriptionManager.Subscribe(dataConfig);

            return enumerator;
        }

        /// <summary>
        /// Removes the specified configuration
        /// </summary>
        /// <param name="dataConfig">Subscription config to be removed</param>
        public void Unsubscribe(SubscriptionDataConfig dataConfig)
        {
            SubscriptionManager.Unsubscribe(dataConfig);
            _aggregator.Remove(dataConfig);
        }

        /// <summary>
        /// Sets the job we're subscribing for
        /// </summary>
        /// <param name="job">Job we're subscribing for</param>
        public void SetJob(LiveNodePacket job)
        {
        }

        #endregion

        #region Brokerage

        /// <summary>
        /// Gets all open orders on the account.
        /// NOTE: The order objects returned do not have QC order IDs.
        /// </summary>
        /// <returns>The open orders returned from IB</returns>
        public override List<Orders.Order> GetOpenOrders()
        {
            var simpleOrders = _restApiClient.GetOpenOrders();
            var triggerOrders = _restApiClient.GetOpenTriggerOrders();
            var openOrders = new List<BaseOrder>(simpleOrders.Count + triggerOrders.Count);
            openOrders.AddRange(simpleOrders);
            openOrders.AddRange(triggerOrders);

            var resultList = new List<Orders.Order>(openOrders.Count);

            foreach (var ftxOrder in openOrders)
            {
                Orders.Order leanOrder;
                switch (ftxOrder)
                {
                    case Order simpleOrder:
                        {
                            leanOrder = CreateOrder(simpleOrder);
                            break;
                        }
                    case TriggerOrder triggerOrder:
                        {
                            leanOrder = CreateTriggerOrder(triggerOrder);
                            break;
                        }
                    default:
                        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1,
                            "FTXBrokerage.GetOpenOrders: Unsupported order type returned from brokerage: " + ftxOrder.Type));
                        continue;
                }

                if (leanOrder == null)
                {
                    continue;
                }

                leanOrder.Quantity = ftxOrder.Quantity;
                leanOrder.BrokerId = new List<string> { ftxOrder.Id.ToStringInvariant() };
                leanOrder.Symbol = _symbolMapper.GetLeanSymbol(ftxOrder.Market, _symbolMapper.GetBrokerageSecurityType(ftxOrder.Market), Market.FTX);
                leanOrder.Time = ftxOrder.CreatedAt;
                leanOrder.Status = ConvertOrderStatus(ftxOrder);

                resultList.Add(leanOrder);
            }

            return resultList;
        }

        /// <summary>
        /// Gets all open positions
        /// </summary>
        /// <returns></returns>
        public override List<Holding> GetAccountHoldings()
        {
            return base.GetAccountHoldings(_job?.BrokerageData, (_securityProvider as SecurityPortfolioManager)?.Securities.Values);
        }


        /// <summary>
        /// Gets the current cash balance for each currency held in the brokerage account
        /// </summary>
        /// <returns>The current cash balance for each currency available for trading</returns>
        public override List<CashAmount> GetCashBalance()
        {
            var balances = _restApiClient.GetBalances()
                .ToList();

            //TODO: discuss negative balances (borrowed)
            balances = balances.Where(balance => balance.Total != 0).ToList();

            if (balances.Any() != true)
                return new List<CashAmount>();

            return balances
                .Select(ConvertBalance)
                .ToList();
        }

        /// <summary>
        /// Places a new order and assigns a new broker ID to the order
        /// </summary>
        /// <param name="order">The order to be placed</param>
        /// <returns>True if the request for a new order has been placed, false otherwise</returns>
        public override bool PlaceOrder(Orders.Order order)
        {
            var submitted = false;

            _messageHandler.WithLockedStream(() =>
            {
                try
                {
                    var payload = new Dictionary<string, object>()
                    {
                        { "market", _symbolMapper.GetBrokerageSymbol(order.Symbol) },
                        { "side", order.Direction.ToLower() },
                        { "size", order.AbsoluteQuantity },
                        { "reduceOnly", (order.Properties as FTXOrderProperties)?.ReduceOnly ?? false },
                        { "clientId", null }
                    };

                    switch (order)
                    {
                        case MarketOrder:
                            {
                                payload.Add("ioc", false);
                                payload.Add("price", null);
                                payload.Add("type", "market");
                                break;
                            }
                        case LimitOrder limitOder:
                            {
                                payload.Add("ioc", false);
                                payload.Add("postOnly", (limitOder.Properties as FTXOrderProperties)?.PostOnly ?? false);
                                payload.Add("price", limitOder.LimitPrice);
                                payload.Add("type", "limit");
                                break;
                            }
                        case StopMarketOrder stopMarketOrder:
                            {
                                payload.Add("type", "stop");
                                payload.Add("triggerPrice", stopMarketOrder.StopPrice);
                                break;
                            }
                        case StopLimitOrder takeProfitOrder:
                            {
                                payload.Add("type", "takeProfit");
                                payload.Add("triggerPrice", takeProfitOrder.StopPrice);
                                payload.Add("orderPrice", takeProfitOrder.LimitPrice);
                                break;
                            }
                    }

                    var resultOrder = _restApiClient.PlaceOrder(payload);

                    order.BrokerId.Add(resultOrder.Id.ToString());

                    OnOrderEvent(new OrderEvent(
                            order,
                            resultOrder.CreatedAt,
                            OrderFee.Zero,
                            "FTX Order Event")
                    { Status = OrderStatus.Submitted }
                    );
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Information, 0, $"Order submitted successfully - OrderId: {order.Id}"));
                    submitted = true;
                }
                catch (Exception e)
                {
                    OnOrderEvent(new OrderEvent(
                            order,
                            DateTime.UtcNow,
                            OrderFee.Zero,
                            "FTX Order Event")
                    { Status = OrderStatus.Invalid, Message = e.Message });
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, e.Message));
                }
            });

            return submitted;
        }

        /// <summary>
        /// Update operation is risky and better to use Cancel & Place combination
        /// to update the order
        /// </summary>
        /// <param name="order">The new order information</param>
        /// <returns>True if the request was made for the order to be updated, false otherwise</returns>
        public override bool UpdateOrder(Orders.Order order)
        {
            // Order's queue priority will be reset, and the order ID of the modified order will be different from that of the original order.
            // Also note: this is implemented as cancelling and replacing your order.
            // There's a chance that the order meant to be cancelled gets filled and its replacement still gets placed.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Cancels the order with the specified ID
        /// </summary>
        /// <param name="order">The order to cancel</param>
        /// <returns>True if the request was made for the order to be canceled, false otherwise</returns>
        public override bool CancelOrder(Orders.Order order)
        {
            var submitted = false;

            _messageHandler.WithLockedStream(() =>
            {
                try
                {
                    submitted = _restApiClient.CancelOrder(Convert.ToUInt64(order.BrokerId.First()));


                    OnOrderEvent(new OrderEvent(
                            order,
                            DateTime.UtcNow,
                            OrderFee.Zero,
                            "Order queued for cancelation")
                    { Status = OrderStatus.CancelPending }
                    );
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Information, 0, $"Order queued for cancelation - OrderId: {order.Id}"));
                }
                catch (Exception e)
                {
                    OnOrderEvent(new OrderEvent(
                            order,
                            DateTime.UtcNow,
                            OrderFee.Zero,
                            "FTX Order Event")
                    { Status = OrderStatus.Invalid, Message = e.Message });
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, e.Message));
                }
            });

            return submitted;
        }

        /// <summary>
        /// Connects the client to the broker's remote servers
        /// </summary>
        public override void Connect()
        {
            if (IsConnected)
                return;

            base.Connect();
            _authResetEvent = new ManualResetEvent(false);
            // we should've authenticated on socket open.
            // here we try again and expect response {"type": "error", "code": 400, "msg": "Already logged in"}
            // to be sure that authenticated successfully
            Authenticate();

            if (!_authResetEvent.WaitOne(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("Websockets connection timeout.");
            }

            _isAuthenticated = true;
            _isAuthenticated &= SubscribeChannel("fills");
            _isAuthenticated &= SubscribeChannel("orders");
        }

        /// <summary>
        /// Disconnects the client from the broker's remote servers
        /// </summary>
        public override void Disconnect()
        {
            WebSocket.Close();
        }

        public override IEnumerable<BaseData> GetHistory(HistoryRequest request)
        {
            if (!_symbolMapper.IsKnownLeanSymbol(request.Symbol))
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidSymbol",
                    $"Unknown symbol: {request.Symbol.Value}, no history returned"));
                yield break;
            }

            if (request.Resolution == Resolution.Tick || request.Resolution == Resolution.Second)
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidResolution",
                    $"{request.Resolution} resolution is not supported, no history returned"));
                yield break;
            }

            if (request.TickType != TickType.Trade)
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidTickType",
                    $"{request.TickType} tick type not supported, no history returned"));
                yield break;
            }

            var period = request.Resolution.ToTimeSpan();

            int[] resolutions = new[] { 15, 60, 300, 900, 3600, 14400 }
                .Union(Enumerable.Repeat(86400, 30).Select((s, i) => s * i))
                .ToArray();
            int resolutionInSeconds = (int)period.TotalSeconds;

            if (Array.IndexOf(resolutions, resolutionInSeconds) == -1)
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidResolution",
                    $"Resolution is not supported, no history returned. Options: 15, 60, 300, 900, 3600, 14400, 86400, or any multiple of 86400 up to 30*86400"));
                yield break;
            }

            foreach (var candle in _restApiClient.GetHistoricalPrices(_symbolMapper.GetBrokerageSymbol(request.Symbol), resolutionInSeconds, request.StartTimeUtc, request.EndTimeUtc))
            {
                yield return new TradeBar()
                {
                    Time = candle.StartTime,
                    Symbol = request.Symbol,
                    Low = candle.Low,
                    High = candle.High,
                    Open = candle.Open,
                    Close = candle.Close,
                    Volume = candle.Volume,
                    Value = candle.Close,
                    DataType = MarketDataType.TradeBar,
                    Period = period
                };
            }
        }

        #endregion

        #region IDataQueueUniverseProvider

        /// <summary>
        /// Method returns a collection of Symbols that are available at the data source.
        /// </summary>
        /// <param name="symbol">Symbol to lookup</param>
        /// <param name="includeExpired">Include expired contracts</param>
        /// <param name="securityCurrency">Expected security currency(if any)</param>
        /// <returns>Enumerable of Symbols, that are associated with the provided Symbol</returns>
        public IEnumerable<Symbol> LookupSymbols(Symbol symbol, bool includeExpired, string securityCurrency = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns whether selection can take place or not.
        /// </summary>
        /// <remarks>This is useful to avoid a selection taking place during invalid times, for example IB reset times or when not connected,
        /// because if allowed selection would fail since IB isn't running and would kill the algorithm</remarks>
        /// <returns>True if selection can take place</returns>
        public bool CanPerformSelection() => IsConnected;

        #endregion

        private bool CanSubscribe(Symbol symbol)
        {
            return symbol.Value.IndexOfInvariant("universe", true) == -1
                   && _symbolMapper.IsKnownLeanSymbol(symbol);
        }

        /// <summary>
        /// Adds the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be added keyed by SecurityType</param>
        public override void Subscribe(IEnumerable<Symbol> symbols)
        {
            SubscribeImpl(symbols);
        }

        /// <summary>
        /// Adds the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be added keyed by SecurityType</param>
        private bool SubscribeImpl(IEnumerable<Symbol> symbols)
        {
            bool success = true;

            foreach (var symbol in symbols)
            {
                success &= SubscribeChannel("trades", symbol);
                success &= SubscribeChannel("orderbook", symbol);
            }

            return success;
        }

        /// <summary>
        /// Removes the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be removed keyed by SecurityType</param>
        private bool Unsubscribe(IEnumerable<Symbol> symbols)
        {
            throw new NotImplementedException();
        }

        public override void OnMessage(object sender, WebSocketMessage e)
        {
            _messageHandler.HandleNewMessage(e);
        }

        public override void Dispose()
        {
            _onSubscribeEvent.DisposeSafely();
            _onUnsubscribeEvent.DisposeSafely();
            _keepAliveTimer.DisposeSafely();
            _restApiClient?.DisposeSafely();
        }
    }
}
