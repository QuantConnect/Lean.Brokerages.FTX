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
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Tests.Brokerages;
using System.Collections.Generic;

namespace QuantConnect.FTXBrokerage.Tests
{
    [TestFixture]
    [Explicit("This test requires a configured and testable FTX practice account")]
    public partial class FTXBrokerageTests : BrokerageTests
    {
        private static readonly Symbol XRP_USDT = Symbol.Create("XRPUSDT", SecurityType.Crypto, Market.FTX);

        protected override Symbol Symbol => XRP_USDT;
        protected override SecurityType SecurityType => SecurityType.Crypto;

        protected override IBrokerage CreateBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider)
            => CreateBrokerage(orderProvider, securityProvider, new LiveNodePacket());

        private IBrokerage CreateBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider, LiveNodePacket liveNodePacket)
        {
            ((SecurityProvider)securityProvider)[Symbol] = CreateSecurity(Symbol);

            var apiKey = Config.Get("ftx-api-key");
            var apiSecret = Config.Get("ftx-api-secret");
            var accountTier = Config.Get("ftx-account-tier");

            return new FTXBrokerage(
                apiKey,
                apiSecret,
                accountTier,
                orderProvider,
                securityProvider,
                new AggregationManager(),
                liveNodePacket
            );
        }

        protected override bool IsAsync() => true;

        protected override bool IsCancelAsync() => false;

        // not user, we don't allow update orders
        protected override decimal GetAskPrice(Symbol symbol) => decimal.Zero;

        /// <summary>
        /// Provides the data required to test each order type in various cases
        /// </summary>
        private static TestCaseData[] OrderParameters()
        {
            return new[]
            {
                new TestCaseData(new MarketOrderTestParameters(XRP_USDT)).SetName("MarketOrder"),
                new TestCaseData(new NonUpdateableLimitOrderTestParameters(XRP_USDT, 1.5m, 0.5m)).SetName("LimitOrder"),
                new TestCaseData(new NonUpdateableStopMarketOrderTestParameters(XRP_USDT, 1.5m, 0.5m)).SetName("StopMarketOrder"),
                new TestCaseData(new NonUpdateableStopLimitOrderTestParameters(XRP_USDT, 1.5m, 0.5m)).SetName("StopLimitOrder")
            };
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public override void CancelOrders(OrderTestParameters parameters)
        {
            base.CancelOrders(parameters);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public override void LongFromZero(OrderTestParameters parameters)
        {
            base.LongFromZero(parameters);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public override void CloseFromLong(OrderTestParameters parameters)
        {
            base.CloseFromLong(parameters);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public override void ShortFromZero(OrderTestParameters parameters)
        {
            base.ShortFromZero(parameters);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public override void CloseFromShort(OrderTestParameters parameters)
        {
            base.CloseFromShort(parameters);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public override void ShortFromLong(OrderTestParameters parameters)
        {
            base.ShortFromLong(parameters);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public override void LongFromShort(OrderTestParameters parameters)
        {
            base.LongFromShort(parameters);
        }

        protected override void ModifyOrderUntilFilled(Order order, OrderTestParameters parameters, double secondsTimeout = 90)
        {
            Assert.Pass("Order update not supported. Please cancel and re-create.");
        }

        [Test]
        public override void GetAccountHoldings()
        {
            Assert.IsEmpty(Brokerage.GetAccountHoldings());
        }

        [Test]
        public virtual void GetAccountHoldingsClearCache()
        {
            var brokerage = CreateBrokerage(
                Mock.Of<IOrderProvider>(),
                new SecurityProvider(),
                new LiveNodePacket
                {
                    BrokerageData = new Dictionary<string, string>
                    {
                        { "live-holdings", "[{\"AveragePrice\": 5,\"Quantity\": 33,\"Symbol\": {\"Value\": \"GME\",\"ID\": \"GME 2T\",\"Permtick\": \"GME\"},\"MarketPrice\": 10, \"Type\":1 }]" }
                    }
                }
            );

            Assert.IsNotEmpty(brokerage.GetAccountHoldings());
            Assert.IsEmpty(brokerage.GetAccountHoldings());
        }
    }
}