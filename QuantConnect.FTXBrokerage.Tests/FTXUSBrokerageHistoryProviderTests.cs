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

using Moq;
using NUnit.Framework;
using QuantConnect.Data.Market;
using QuantConnect.Securities;
using System;

namespace QuantConnect.FTXBrokerage.Tests
{
    [TestFixture]
    public class FTXUSBrokerageHistoryProviderTests : FTXBrokerageHistoryProviderTests
    {
        protected override FTXBrokerage CreateBrokerage()
            => new FTXUSBrokerage(
                Mock.Of<IOrderProvider>(),
                Mock.Of<ISecurityProvider>(),
                null,
                null);

        private static TestCaseData[] ValidTestParameters
        {
            get
            {
                return new[]
                {
                    // valid parameters:
                    new TestCaseData(Symbol.Create("SUSHIUSDT", SecurityType.Crypto, Market.FTXUS), Resolution.Minute, TimeSpan.FromDays(30), TickType.Trade, typeof(TradeBar)),
                    new TestCaseData(Symbol.Create("SUSHIUSDT", SecurityType.Crypto, Market.FTXUS), Resolution.Minute, TimeSpan.FromMinutes(5), TickType.Trade, typeof(TradeBar)),
                    new TestCaseData(Symbol.Create("SUSHIUSDT", SecurityType.Crypto, Market.FTXUS), Resolution.Hour, TimeSpan.FromDays(10), TickType.Trade, typeof(TradeBar)),
                    new TestCaseData(Symbol.Create("SUSHIUSDT", SecurityType.Crypto, Market.FTXUS), Resolution.Daily, TimeSpan.FromDays(15), TickType.Trade, typeof(TradeBar))
                };
            }
        }

        private static TestCaseData[] InvalidTestParameters
        {
            get
            {
                return new[]
                {
                    new TestCaseData(Symbol.Create("SUSHIUSDT", SecurityType.Crypto, Market.FTXUS), Resolution.Tick, TimeSpan.FromMinutes(1), TickType.Trade, typeof(TradeBar)),
                    new TestCaseData(Symbol.Create("SUSHIUSDT", SecurityType.Crypto, Market.FTXUS), Resolution.Second, TimeSpan.FromMinutes(5), TickType.Trade, typeof(TradeBar))
                };
            }
        }

        [Test, TestCaseSource(nameof(ValidTestParameters))]
        public override void GetsHistoryForValid(Symbol symbol, Resolution resolution, TimeSpan period, TickType tickType, Type dataType)
        {
            base.GetsHistoryForValid(symbol, resolution, period, tickType, dataType);
        }

        [Test, TestCaseSource(nameof(InvalidTestParameters))]
        public override void GetsHistoryForInvalid(Symbol symbol, Resolution resolution, TimeSpan period, TickType tickType, Type dataType)
        {
            base.GetsHistoryForInvalid(symbol, resolution, period, tickType, dataType);
        }
    }
}