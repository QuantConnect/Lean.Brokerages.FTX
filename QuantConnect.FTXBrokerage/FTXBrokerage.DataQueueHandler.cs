﻿/*
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
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Logging;
using QuantConnect.Packets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.FTXBrokerage
{
    public partial class FTXBrokerage
    {
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

        private bool Subscribe(IWebSocket webSocket, Symbol symbol)
        {
            return SubscribeChannel(webSocket, "trades", symbol)
                   && SubscribeChannel(webSocket, "orderbook", symbol);
        }

        private bool Unsubscribe(IWebSocket webSocket, Symbol symbol)
        {
            return UnsubscribeChannel(webSocket, "trades", symbol)
                   && UnsubscribeChannel(webSocket, "orderbook", symbol);
        }

        private bool SubscribeChannel(IWebSocket webSocket, string channel, Symbol symbol = null)
        {
            _onSubscribeEvent.Reset();

            var payload = new Dictionary<string, object>()
            {
                {"op", "subscribe"},
                { "channel", channel }
            };

            if (symbol != null)
            {
                payload.Add("market", _symbolMapper.GetBrokerageSymbol(symbol));
            }

            webSocket.Send(JsonConvert.SerializeObject(payload, FTXRestApiClient.JsonSettings));

            if (!_onSubscribeEvent.WaitOne(TimeSpan.FromSeconds(30)))
            {
                Log.Error($"FTXBrokerage.Subscribe(): Could not subscribe to {symbol?.Value}/{channel}.");
                return false;
            }

            return true;
        }

        private bool UnsubscribeChannel(IWebSocket webSocket, string channel, Symbol symbol)
        {
            _onUnsubscribeEvent.Reset();

            webSocket.Send(JsonConvert.SerializeObject(new
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

        private bool CanSubscribe(Symbol symbol)
        {
            return symbol.Value.IndexOfInvariant("universe", true) == -1
                   && _symbolMapper.IsKnownLeanSymbol(symbol)
                   && symbol.SecurityType == SecurityType.Crypto;
        }
    }
}