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

using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.HistoricalData;
using QuantConnect.Logging;
using QuantConnect.Securities;
using System;
using Moq;
using QuantConnect.Brokerages;
using QuantConnect.Interfaces;

namespace QuantConnect.FTXBrokerage.Tests
{
    [TestFixture, Ignore("Not implemented")]
    public class FTXBrokerageHistoryProviderTests
    {
        private FTXBrokerage _brokerage;

        public FTXBrokerageHistoryProviderTests()
        {
            _brokerage = new FTXBrokerage(
                Mock.Of<IOrderProvider>(),
                Mock.Of<ISecurityProvider>(),
                null,
                null);
        }

        private static TestCaseData[] ValidTestParameters
        {
            get
            {
                return new[]
                {
                    // valid parameters:
                    new TestCaseData(Symbol.Create("XRPUSDT", SecurityType.Crypto, Market.FTX), Resolution.Minute, TimeSpan.FromMinutes(5), TickType.Trade, typeof(TradeBar)),
                    new TestCaseData(Symbol.Create("XRPUSDT", SecurityType.Crypto, Market.FTX), Resolution.Hour, TimeSpan.FromDays(10), TickType.Trade, typeof(TradeBar)),
                    new TestCaseData(Symbol.Create("XRPUSDT", SecurityType.Crypto, Market.FTX), Resolution.Daily, TimeSpan.FromDays(15), TickType.Trade, typeof(TradeBar))
                };
            }
        }

        private static TestCaseData[] InvalidTestParameters
        {
            get
            {
                return new[]
                {
                    new TestCaseData(Symbol.Create("XRPUSDT", SecurityType.Crypto, Market.GDAX), Resolution.Minute, TimeSpan.FromMinutes(5), TickType.Trade, typeof(TradeBar)),
                    new TestCaseData(Symbol.Create("XRPUSDT", SecurityType.Crypto, Market.FTX), Resolution.Tick, TimeSpan.FromMinutes(1), TickType.Trade, typeof(TradeBar)),
                    new TestCaseData(Symbol.Create("XRPUSDT", SecurityType.Crypto, Market.FTX), Resolution.Second, TimeSpan.FromMinutes(5), TickType.Trade, typeof(TradeBar)),
                    new TestCaseData(Symbol.Create("XRPUSDT", SecurityType.Crypto, Market.FTX), Resolution.Hour, TimeSpan.FromMinutes(10), TickType.Quote, typeof(QuoteBar)),
                    new TestCaseData(Symbol.Create("XRPUSDT", SecurityType.Crypto, Market.FTX), Resolution.Daily, TimeSpan.FromDays(10), TickType.Quote, typeof(QuoteBar))
                };
            }
        }

        [Test, TestCaseSource(nameof(ValidTestParameters))]
        public void GetsHistoryForValid(Symbol symbol, Resolution resolution, TimeSpan period, TickType tickType, Type dataType)
        {
            int numberOfDataPoints = 0;

            Assert.DoesNotThrow(() =>
            {
                numberOfDataPoints = GetsHistory(symbol, resolution, period, tickType, dataType);
            });

            Assert.Greater(numberOfDataPoints, 0);
        }

        [Test, TestCaseSource(nameof(InvalidTestParameters))]
        public void GetsHistoryForInvalid(Symbol symbol, Resolution resolution, TimeSpan period, TickType tickType, Type dataType)
        {
            bool receievedWarning = false;
            EventHandler<BrokerageMessageEvent> messagEventHandler = (s, e) =>
            {
                receievedWarning = true;
            };
            int numberOfDataPoints = 0;
            _brokerage.Message += messagEventHandler;
            Assert.DoesNotThrow(() =>
            {
                numberOfDataPoints = GetsHistory(symbol, resolution, period, tickType, dataType);
            });

            Assert.Zero(numberOfDataPoints);
            Assert.True(receievedWarning);
            _brokerage.Message += messagEventHandler;
        }

        public int GetsHistory(Symbol symbol, Resolution resolution, TimeSpan period, TickType tickType, Type dataType)
        {
            var historyProvider = new BrokerageHistoryProvider();
            historyProvider.SetBrokerage(_brokerage);
            historyProvider.Initialize(new HistoryProviderInitializeParameters(null, null, null,
                null, null, null, null,
                false, Mock.Of<IDataPermissionManager>()));

            var marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
            var now = DateTime.UtcNow;
            var requests = new[]
            {
                new HistoryRequest(now.Add(-period),
                    now,
                    dataType,
                    symbol,
                    resolution,
                    marketHoursDatabase.GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType),
                    marketHoursDatabase.GetDataTimeZone(symbol.ID.Market, symbol, symbol.SecurityType),
                    resolution,
                    false,
                    false,
                    DataNormalizationMode.Adjusted,
                    tickType)
            };

            foreach (var slice in historyProvider.GetHistory(requests, TimeZones.Utc))
            {
                if (resolution == Resolution.Tick)
                {
                    foreach (var tick in slice.Ticks[symbol])
                    {
                        Log.Trace($"{tick}");
                    }
                }
                else if (slice.QuoteBars.TryGetValue(symbol, out var quoteBar))
                {
                    Log.Trace($"{quoteBar}");
                }
                else if (slice.Bars.TryGetValue(symbol, out var tradeBar))
                {
                    Log.Trace($"{tradeBar}");
                }
            }

            Log.Trace("Data points retrieved: " + historyProvider.DataPointCount);
            return historyProvider.DataPointCount;
        }
    }
}