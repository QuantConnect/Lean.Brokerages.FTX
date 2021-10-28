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
using QuantConnect.ToolBox;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using QuantConnect.Configuration;

namespace QuantConnect.FTXBrokerage.ToolBox
{
    /// <summary>
    /// FTX implementation of <see cref="IExchangeInfoDownloader"/>
    /// </summary>
    public class FTXMarketDownloader : IExchangeInfoDownloader
    {
        /// <summary>
        /// Market name
        /// </summary>
        public string Market { get; private set; }

        public FTXMarketDownloader(string market = QuantConnect.Market.FTX)
        {
            Market = market;
            if (market == QuantConnect.Market.FTXUS)
            {
                Config.Set("ftx-api-url", "https://ftx.us/api");
            }
        }

        /// <summary>
        /// Pulling data from a remote source
        /// </summary>
        /// <returns>Enumerable of exchange info</returns>
        public IEnumerable<string> Get()
        {
            var client = new FTXRestApiClient();

            var exchangeInfo = client.GetAllMarkets();

            foreach (var symbol in exchangeInfo.Where(s => s.Type.Equals("spot")).OrderBy(x => x.Name))
            {
                var leanSymbolName = symbol.Name.Replace("/", "");
                yield return $"{Market},{leanSymbolName},crypto,{symbol.Name},{symbol.QuoteCurrency},1,{symbol.PriceIncrement.ToStringInvariant()},{symbol.SizeIncrement.ToStringInvariant()},{symbol.Name},{symbol.MinProvideSize.ToStringInvariant()}";
            }
        }

        /// <summary>
        /// Endpoint for downloading exchange info
        /// </summary>
        public static void ExchangeInfoDownloader(string market = QuantConnect.Market.FTX)
        {
            new ExchangeInfoUpdater(new FTXMarketDownloader(market))
                .Run();
        }
    }
}
