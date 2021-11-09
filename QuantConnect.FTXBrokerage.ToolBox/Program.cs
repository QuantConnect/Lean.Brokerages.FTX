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

using QuantConnect.Configuration;
using System;
using static QuantConnect.Configuration.ApplicationParser;

namespace QuantConnect.FTXBrokerage.ToolBox
{
    static class Program
    {
        static void Main(string[] args)
        {
            var optionsObject = ToolboxArgumentParser.ParseArguments(args);
            if (optionsObject.Count == 0)
            {
                PrintMessageAndExit();
            }

            var targetApp = GetParameterOrExit(optionsObject, "app").ToLowerInvariant();
            if (targetApp.Contains("download") || targetApp.EndsWith("dl"))
            {
                var fromDate = Parse.DateTimeExact(GetParameterOrExit(optionsObject, "from-date"), "yyyyMMdd-HH:mm:ss");
                var resolution = optionsObject.ContainsKey("resolution") ? optionsObject["resolution"].ToString() : "";
                var securityType = optionsObject.ContainsKey("security-type") ? optionsObject["security-type"].ToString() : "";
                var tickers = ToolboxArgumentParser.GetTickers(optionsObject);
                var toDate = optionsObject.ContainsKey("to-date")
                    ? Parse.DateTimeExact(optionsObject["to-date"].ToString(), "yyyyMMdd-HH:mm:ss")
                    : DateTime.UtcNow;
                switch (targetApp)
                {
                    case "ftxdl":
                    case "ftxdownloader":
                        FTXHistoryDownloader.DownloadHistory(tickers, resolution, securityType, fromDate, toDate);
                        break;

                    default:
                        PrintMessageAndExit(1, "ERROR: Unrecognized --app value");
                        break;
                }
            }
            else if (targetApp.Contains("updater") || targetApp.EndsWith("spu"))
            {
                switch (targetApp)
                {
                    case "ftxspu":
                    case "ftxmarketupdater":
                        FTXMarketDownloader.ExchangeInfoDownloader();
                        break;
                    default:
                        PrintMessageAndExit(1, "ERROR: Unrecognized --app value");
                        break;
                }
            }
        }
    }
}