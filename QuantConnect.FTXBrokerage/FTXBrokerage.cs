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
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.FTXBrokerage.Messages;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DateTime = System.DateTime;
using HistoryRequest = QuantConnect.Data.HistoryRequest;
using LimitOrder = QuantConnect.Orders.LimitOrder;
using MarketOrder = QuantConnect.Orders.MarketOrder;
using Order = QuantConnect.FTXBrokerage.Messages.Order;
using Timer = System.Timers.Timer;

namespace QuantConnect.FTXBrokerage
{
    /// <summary>
    /// FTX Brokerage implementation
    /// </summary>
    [BrokerageFactory(typeof(FTXBrokerageFactory))]
    public partial class FTXBrokerage : BaseWebsocketsBrokerage, IDataQueueHandler
    {
        private bool _isAuthenticated;

        private string _market;
        private IDataAggregator _aggregator;
        private IOrderProvider _orderProvider;
        private ISecurityProvider _securityProvider;
        private BrokerageConcurrentMessageHandler<WebSocketMessage> _messageHandler;
        private readonly SymbolPropertiesDatabaseSymbolMapper _symbolMapper;
        private Timer _keepAliveTimer;
        private FTXRestApiClient _restApiClient;

        private const int MaximumSymbolsPerConnection = 12;
        private const int HistoricalDataPerRequestLimit = 1000;

        /// <summary>
        /// A list of currently active stop orders and their appropriate orders
        /// </summary>
        private readonly ConcurrentDictionary<ulong, Orders.Order> _stopCachedOrderIDs = new();

        /// <summary>
        /// Returns true if we're currently connected to the broker
        /// </summary>
        public override bool IsConnected => WebSocket.IsOpen && _isAuthenticated;

        /// <summary>
        /// Parameterless constructor for brokerage
        /// </summary>
        /// <remarks>This parameterless constructor is required for brokerages implementing <see cref="IDataQueueHandler"/></remarks>
        public FTXBrokerage(): base("FTX")
        {
        }

        /// <summary>
        /// Creates a new <see cref="FTXBrokerage"/> from the specified values retrieving data from configuration file
        /// </summary>
        /// <param name="orderProvider">The order provider</param>
        /// <param name="securityProvider">The security provider</param>
        /// <param name="aggregator">data aggregator</param>
        /// <param name="job">The job packet</param>
        public FTXBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider, IDataAggregator aggregator, LiveNodePacket job) : this(
            Config.Get("ftx-api-key"),
            Config.Get("ftx-api-secret"),
            Config.Get("ftx-account-tier"),
            orderProvider,
            securityProvider,
            aggregator,
            job)
        { }

        /// <summary>
        /// Creates a new <see cref="FTXBrokerage"/>
        /// </summary>
        /// <param name="apiKey">api key</param>
        /// <param name="apiSecret">api secret</param>
        /// <param name="accountTier">account tier</param>
        /// <param name="algorithm">the algorithm instance is required to retrieve account type</param>
        /// <param name="aggregator">consolidate ticks</param>
        /// <param name="job">The live job packet</param>
        public FTXBrokerage(string apiKey, string apiSecret, string accountTier, IAlgorithm algorithm, IDataAggregator aggregator, LiveNodePacket job) : this(
            apiKey,
            apiSecret,
            accountTier,
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
        /// <param name="accountTier">account tier</param>
        /// <param name="orderProvider">An instance of IOrderProvider used to fetch Order objects by brokerage ID</param>
        /// <param name="securityProvider">The security provider used to give access to algorithm securities</param>
        /// <param name="aggregator">consolidate ticks</param>
        /// <param name="job">The live job packet</param>
        public FTXBrokerage(string apiKey, string apiSecret, string accountTier, IOrderProvider orderProvider, ISecurityProvider securityProvider, IDataAggregator aggregator, LiveNodePacket job) : this(
            apiKey,
            apiSecret,
            accountTier,
            "https://ftx.com/api",
            "wss://ftx.com/ws/",
            orderProvider,
            securityProvider,
            aggregator,
            job,
            Market.FTX)
        {
        }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="apiKey">api key</param>
        /// <param name="apiSecret">api secret</param>
        /// <param name="accountTier">account tier</param>
        /// <param name="orderProvider">An instance of IOrderProvider used to fetch Order objects by brokerage ID</param>
        /// <param name="securityProvider">The security provider used to give access to algorithm securities</param>
        /// <param name="aggregator">consolidate ticks</param>
        /// <param name="job">The live job packet</param>
        protected FTXBrokerage(string apiKey, string apiSecret, string accountTier, string restApiUrl, string wssUrl, IOrderProvider orderProvider, ISecurityProvider securityProvider, IDataAggregator aggregator, LiveNodePacket job, string exchangeName) : base(
            wssUrl,
            exchangeName.ToUpperInvariant())
        {
            Initialize(apiKey, apiSecret, accountTier, orderProvider, securityProvider, aggregator, job);
        }

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
                var symbol = _symbolMapper.GetLeanSymbol(ftxOrder.Market, SecurityType.Crypto, _market);
                Orders.Order leanOrder;
                switch (ftxOrder)
                {
                    case Order simpleOrder:
                        {
                            leanOrder = CreateOrder(symbol, simpleOrder);
                            break;
                        }
                    case TriggerOrder triggerOrder:
                        {
                            leanOrder = CreateTriggerOrder(symbol, triggerOrder);
                            // track original lean order instances for trigger orders, see GH-6041
                            // it allows us to attach new orders to original lean order
                            _stopCachedOrderIDs.AddOrUpdate(ftxOrder.Id, leanOrder);
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

                leanOrder.BrokerId.Add(ftxOrder.Id.ToStringInvariant());
                leanOrder.Status = ConvertOrderStatus(ftxOrder);

                resultList.Add(leanOrder);
            }

            return resultList;
        }


        /// <summary>
        /// Gets all open positions, not applicable for spot assets
        /// https://docs.ftx.com/#get-positions works for futures only
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
            return _restApiClient.GetBalances()
                .Where(balance => balance.Total != 0)
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
                        case StopLimitOrder stopLimitOrder:
                            {
                                payload.Add("type", "stop");
                                payload.Add("triggerPrice", stopLimitOrder.StopPrice);
                                payload.Add("orderPrice", stopLimitOrder.LimitPrice);
                                break;
                            }
                        default:
                            {
                                throw new NotSupportedException(
                                    $"FTXBrokerage.PlaceOrder: Unsupported order type: {order.Type}");
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

                    if (order.Type is OrderType.StopLimit or OrderType.StopMarket)
                    {
                        _stopCachedOrderIDs.TryAdd(resultOrder.Id, order);
                    }
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
            throw new NotSupportedException("FTXBrokerage.UpdateOrder: Order update not supported. Please cancel and re-create.");
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
                    string orderType;
                    string brokerId = order.BrokerId.First();
                    OrderStatus newStatus;
                    switch (order.Type)
                    {
                        case OrderType.Market:
                            {
                                orderType = "market";
                                newStatus = OrderStatus.CancelPending;
                                break;
                            }
                        case OrderType.Limit:
                            {
                                orderType = "limit";
                                newStatus = OrderStatus.CancelPending;
                                break;
                            }
                        case OrderType.StopMarket:
                            {
                                orderType = "stop";
                                newStatus = OrderStatus.Canceled;
                                break;
                            }
                        case OrderType.StopLimit:
                            {
                                orderType = "stop";
                                newStatus = OrderStatus.Canceled;
                                // check if stop limit order was triggered
                                if (_stopCachedOrderIDs.TryGetValue(ulong.Parse(brokerId), out var originalOrder) &&
                                    originalOrder.BrokerId.Count > 1)
                                {
                                    // close appropriate limit order if Lean tries to close triggered stop limit order
                                    orderType = "limit";
                                    brokerId = originalOrder.BrokerId.Last();
                                    newStatus = OrderStatus.CancelPending;
                                }
                                break;
                            }
                        default:
                            {
                                throw new NotSupportedException($"FTXBrokerage.PlaceOrder: Unsupported order type: {order.Type}");
                            }

                    }
                    submitted = _restApiClient.CancelOrder(orderType, brokerId.ConvertInvariant<ulong>());


                    OnOrderEvent(new OrderEvent(
                            order,
                            DateTime.UtcNow,
                            OrderFee.Zero,
                            "Order queued for cancelation")
                    { Status = newStatus }
                    );
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Information, 0, $"Order queued for cancelation - OrderId: {order.Id}"));
                    if (newStatus == OrderStatus.Canceled && !_stopCachedOrderIDs.TryRemove(ulong.Parse(order.BrokerId.First()), out _))
                    {
                        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, $"Could not remove order from local cache - OrderId: {order.Id}"));
                    }
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

            // ftx doesn't send any response if "login" is succeded
            // here we try again and expect response {"type": "error", "code": 400, "msg": "Already logged in"}
            // to be sure that authenticated successfully
            Authenticate();

            if (!_authResetEvent.WaitOne(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("Websockets connection timeout.");
            }

            _isAuthenticated = true;
            _isAuthenticated &= SubscribeChannel(WebSocket, "fills");
            _isAuthenticated &= SubscribeChannel(WebSocket, "orders");
        }

        /// <summary>
        /// Disconnects the client from the broker's remote servers
        /// </summary>
        public override void Disconnect()
        {
            WebSocket.Close();
        }

        /// <summary>
        /// Gets the history for the requested security
        /// </summary>
        /// <param name="request">The historical data request</param>
        /// <returns>An enumerable of bars covering the span specified in the request</returns>
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

            var brokerageSymbol = _symbolMapper.GetBrokerageSymbol(request.Symbol);
            var period = request.Resolution.ToTimeSpan();
            var resolutionInSeconds = (int)period.TotalSeconds;
            //ftx returns last candle with startTime equals to end_time of request
            var lastRequestedBarEndTime = request.EndTimeUtc.RoundDown(period).Add(-period);

            // Define current api request's start and end dates
            var currentStartTime = request.StartTimeUtc.RoundDown(period);
            var currentEndTime = lastRequestedBarEndTime;

            // Check if need to use pagination 
            var requestBarsCount = (currentEndTime - currentStartTime).Ticks / period.Ticks + 1;
            if (requestBarsCount > HistoricalDataPerRequestLimit)
            {
                currentEndTime = currentStartTime + TimeSpan.FromTicks(period.Ticks * (HistoricalDataPerRequestLimit - 1));
            }

            // Fetch the data
            while (currentStartTime < lastRequestedBarEndTime)
            {
                Log.Debug($"FTXBrokerage.GetHistory(): Fetching data from {currentStartTime:g} to {currentEndTime:g} for {request.Symbol.Value}");

                foreach (var candle in _restApiClient.GetHistoricalPrices(brokerageSymbol, resolutionInSeconds, currentStartTime, currentEndTime))
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
                        DataType = MarketDataType.TradeBar,
                        Period = period
                    };
                }

                currentStartTime = currentEndTime + TimeSpan.FromTicks(period.Ticks);
                currentEndTime = currentStartTime + TimeSpan.FromTicks(period.Ticks * (HistoricalDataPerRequestLimit - 1));
                currentEndTime = currentEndTime > lastRequestedBarEndTime
                    ? lastRequestedBarEndTime
                    : currentEndTime;
            }
        }

        #endregion

        /// <summary>
        /// Initializes the instance of the class
        /// </summary>
        /// <param name="apiKey">api key</param>
        /// <param name="apiSecret">api secret</param>
        /// <param name="accountTier">account tier</param>
        /// <param name="orderProvider">An instance of IOrderProvider used to fetch Order objects by brokerage ID</param>
        /// <param name="securityProvider">The security provider used to give access to algorithm securities</param>
        /// <param name="aggregator">consolidate ticks</param>
        /// <param name="job">The live job packet</param>
        protected void Initialize(string apiKey, string apiSecret, string accountTier, IOrderProvider orderProvider, ISecurityProvider securityProvider, IDataAggregator aggregator, LiveNodePacket job)
        {
            if (IsInitialized)
            {
                return;
            }
            base.Initialize(FTXRestApiClient.WsApiUrl, new WebSocketClientWrapper(), new RestClient(FTXRestApiClient.RestApiUrl),apiKey, apiSecret);
            _orderProvider = orderProvider;
            _securityProvider = securityProvider;
            _job = job;
            _aggregator = aggregator;
            SubscriptionManager = new BrokerageMultiWebSocketSubscriptionManager(
                FTXRestApiClient.WsApiUrl,
                MaximumSymbolsPerConnection,
                maximumWebSocketConnections: 0,
                null,
                () =>
                {
                    var webSocket = new WebSocketClientWrapper();
                    _webSocketResetEvents.AddOrUpdate(webSocket, new ManualResetEvent(false));
                    return webSocket;
                },
                Subscribe,
                Unsubscribe,
                OnStreamDataImpl,
                webSocketConnectionDuration: TimeSpan.Zero);

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
            _messageHandler = new BrokerageConcurrentMessageHandler<WebSocketMessage>(OnUserDataImpl);

            _restApiClient = new FTXRestApiClient(RestClient, apiKey, apiSecret, accountTier);
            _webSocketResetEvents.AddOrUpdate(WebSocket, new ManualResetEvent(false));
        }

        /// <summary>
        /// Adds the specified symbols to the subscription
        /// </summary>
        /// <param name="symbols">The symbols to be added keyed by SecurityType</param>
        protected override bool Subscribe(IEnumerable<Symbol> symbols)
        {
            return true;
        }

        /// <summary>
        /// Handles websocket received messages
        /// </summary>
        /// <param name="sender">web socket wrapper</param>
        /// <param name="e">message</param>
        protected override void OnMessage(object sender, WebSocketMessage e)
        {
            _messageHandler.HandleNewMessage(e);
        }

        /// <summary>
        /// Dispose of the brokerage instance
        /// </summary>
        public override void Dispose()
        {
            foreach (var onSubscribeEvent in _webSocketResetEvents.Values)
            {
                onSubscribeEvent.DisposeSafely();
            }

            _authResetEvent?.DisposeSafely();
            _keepAliveTimer?.DisposeSafely();
            _restApiClient?.DisposeSafely();
        }
    }
}
