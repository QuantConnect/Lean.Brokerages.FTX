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

using NodaTime;
using QuantConnect.Brokerages;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.FTXBrokerage.ToolBox
{
    /// <summary>
    /// Template Brokerage Data Downloader implementation
    /// </summary>
    public class FTXHistoryDownloader : IDataDownloader
    {
        private readonly FTXBrokerage _brokerage;
        private readonly SymbolPropertiesDatabaseSymbolMapper _symbolMapper = new SymbolPropertiesDatabaseSymbolMapper(Market.FTX);

        /// <summary>
        /// Initializes a new instance of the <see cref="FTXDataDownloader"/> class
        /// </summary>
        public FTXHistoryDownloader()
        {
            _brokerage = new FTXBrokerage(string.Empty, string.Empty, null, null, null);
        }

        /// <summary>
        /// Get historical data enumerable for a single symbol, type and resolution given this start and end time (in UTC).
        /// </summary>
        /// <param name="symbol">Symbol for the data we're looking for.</param>
        /// <param name="resolution">Resolution of the data request</param>
        /// <param name="startUtc">Start time of the data in UTC</param>
        /// <param name="endUtc">End time of the data in UTC</param>
        /// <returns>Enumerable of base data for this symbol</returns>
        public IEnumerable<BaseData> Get(Symbol symbol, Resolution resolution, DateTime startUtc, DateTime endUtc)
        {
            if (resolution == Resolution.Tick || resolution == Resolution.Second)
                throw new ArgumentException($"Resolution not available: {resolution}");

            if (!_symbolMapper.IsKnownLeanSymbol(symbol))
                throw new ArgumentException($"The ticker {symbol.Value} is not available.");

            if (endUtc < startUtc)
                throw new ArgumentException("The end date must be greater or equal than the start date.");

            var historyRequest = new HistoryRequest(
                startUtc,
                endUtc,
                typeof(TradeBar),
                symbol,
                resolution,
                SecurityExchangeHours.AlwaysOpen(TimeZones.EasternStandard),
                DateTimeZone.Utc,
                resolution,
                false,
                false,
                DataNormalizationMode.Adjusted,
                TickType.Trade);

            var data = _brokerage.GetHistory(historyRequest);

            return data;
        }

        /// <summary>
        /// Creates Lean Symbol
        /// </summary>
        /// <param name="ticker"></param>
        /// <returns></returns>
        private Symbol GetSymbol(string ticker)
        {
            return _symbolMapper.GetLeanSymbol(ticker, SecurityType.Crypto, Market.FTX);
        }

        /// <summary>
        /// Aggregates a list of minute bars at the requested resolution
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="bars"></param>
        /// <param name="resolution"></param>
        /// <returns></returns>
        private IEnumerable<TradeBar> AggregateBars(Symbol symbol, IEnumerable<TradeBar> bars, TimeSpan resolution)
        {
            return
                (from b in bars
                 group b by b.Time.RoundDown(resolution)
                    into g
                 select new TradeBar
                 {
                     Symbol = symbol,
                     Time = g.Key,
                     Open = g.First().Open,
                     High = g.Max(b => b.High),
                     Low = g.Min(b => b.Low),
                     Close = g.Last().Close,
                     Volume = g.Sum(b => b.Volume),
                     Value = g.Last().Close,
                     DataType = MarketDataType.TradeBar,
                     Period = resolution,
                     EndTime = g.Key.AddMilliseconds(resolution.TotalMilliseconds)
                 });
        }

        public static void DownloadHistory(List<string> tickers, string resolution, string securityType, DateTime fromDate, DateTime toDate)
        {
            if (resolution.IsNullOrEmpty() || tickers.IsNullOrEmpty())
            {
                Console.WriteLine("FTXHistoryDownloader ERROR: '--tickers=' or '--resolution=' parameter is missing");
                Console.WriteLine("--tickers=eg BTCUSD");
                Console.WriteLine("--resolution=Minute/Hour/Daily/All");
                Environment.Exit(1);
            }
            try
            {
                var allResolutions = resolution.Equals("all", StringComparison.OrdinalIgnoreCase);
                var castResolution = allResolutions ? Resolution.Minute : (Resolution)Enum.Parse(typeof(Resolution), resolution);

                // Load settings from config.json
                var dataDirectory = Config.Get("data-folder", "../../../Data");

                var downloader = new FTXHistoryDownloader();

                foreach (var ticker in tickers)
                {
                    // Download the data
                    var startDate = fromDate;
                    var symbol = downloader.GetSymbol(ticker);
                    var data = downloader.Get(symbol, castResolution, fromDate, toDate);
                    var bars = data.Cast<TradeBar>().ToList();

                    // Save the data (single resolution)
                    var writer = new LeanDataWriter(castResolution, symbol, dataDirectory);
                    writer.Write(bars);

                    if (allResolutions)
                    {
                        // Save the data (other resolutions)
                        foreach (var res in new[] { Resolution.Hour, Resolution.Daily })
                        {
                            var resData = downloader.AggregateBars(symbol, bars, res.ToTimeSpan());

                            writer = new LeanDataWriter(res, symbol, dataDirectory);
                            writer.Write(resData);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }
    }
}