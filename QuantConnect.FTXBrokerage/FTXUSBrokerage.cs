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
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.FTXBrokerage
{
    /// <summary>
    /// FTX.US Brokerage implementation
    /// </summary>
    [BrokerageFactory(typeof(FTXUSBrokerageFactory))]
    public class FTXUSBrokerage : FTXBrokerage
    {
        /// <summary>
        /// Parameterless constructor for brokerage
        /// </summary>
        /// <remarks>This parameterless constructor is required for brokerages implementing <see cref="IDataQueueHandler"/></remarks>
        public FTXUSBrokerage() : base(Market.FTXUS)
        {
        }

        /// <summary>
        /// Creates a new <see cref="FTXBrokerage"/> from the specified values retrieving data from configuration file
        /// </summary>
        /// <param name="orderProvider">The order provider</param>
        /// <param name="securityProvider">The security provider</param>
        /// <param name="aggregator">data aggregator</param>
        /// <param name="job">The job packet</param>
        public FTXUSBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider, IDataAggregator aggregator, LiveNodePacket job) : this(
            Config.Get("ftxus-api-key"),
            Config.Get("ftxus-api-secret"),
            Config.Get("ftxus-account-tier"),
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
        public FTXUSBrokerage(string apiKey, string apiSecret, string accountTier, IAlgorithm algorithm, IDataAggregator aggregator, LiveNodePacket job) : this(
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
        public FTXUSBrokerage(string apiKey, string apiSecret, string accountTier, IOrderProvider orderProvider, ISecurityProvider securityProvider, IDataAggregator aggregator, LiveNodePacket job) : base(
            apiKey,
            apiSecret,
            accountTier,
            FTXRestApiClient.FtxUsRestEndpoint,
            FTXRestApiClient.FtxUsWsEndpoint,
            orderProvider,
            securityProvider,
            aggregator,
            job,
            Market.FTXUS)
        {
        }

        protected override void SetJobInit(LiveNodePacket job, IDataAggregator aggregator)
        {
            Initialize(
                job.BrokerageData["ftxus-api-key"],
                job.BrokerageData["ftxus-api-secret"],
                job.BrokerageData["ftxus-account-tier"],
                FTXRestApiClient.FtxUsRestEndpoint,
                FTXRestApiClient.FtxUsWsEndpoint,
                null,
                null,
                aggregator,
                job,
                Market.FTXUS);
        }
    }
}
