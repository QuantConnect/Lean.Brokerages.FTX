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
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Tests.Brokerages;

namespace QuantConnect.FTXBrokerage.Tests
{
    [TestFixture]
    [Explicit("This test requires a configured and testable FTX.US practice account")]
    public partial class FTXUSBrokerageTests : FTXBrokerageTests
    {
        private static readonly Symbol SUSHI_USDT = Symbol.Create("SUCHIUSDT", SecurityType.Crypto, Market.FTXUS);

        protected override Symbol Symbol => SUSHI_USDT;

        protected override IBrokerage CreateBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider)
            => CreateBrokerage(orderProvider, securityProvider, new LiveNodePacket());

        protected override IBrokerage CreateBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider, LiveNodePacket liveNodePacket)
        {
            ((SecurityProvider)securityProvider)[Symbol] = CreateSecurity(Symbol);

            var apiKey = Config.Get("ftxus-api-key");
            var apiSecret = Config.Get("ftxus-api-secret");
            var accountTier = Config.Get("ftxus-account-tier");

            return new FTXUSBrokerage(
                apiKey,
                apiSecret,
                accountTier,
                orderProvider,
                securityProvider,
                new AggregationManager(),
                liveNodePacket
            );
        }
    }
}