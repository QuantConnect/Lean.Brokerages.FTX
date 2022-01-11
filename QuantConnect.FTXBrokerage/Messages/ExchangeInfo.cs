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

namespace QuantConnect.FTXBrokerage.Messages
{
    public class ExchangeInfo
    {
        public string Name { get; set; }
        public string BaseCurrency { get; set; }
        public string QuoteCurrency { get; set; }
        public decimal? QuoteVolume24h { get; set; }
        public decimal? Change1h { get; set; }
        public decimal? Change24h { get; set; }
        public decimal? ChangeBod { get; set; }
        public bool HighLeverageFeeExempt { get; set; }
        public decimal MinProvideSize { get; set; }
        public string Type { get; set; }
        public string Underlying { get; set; }
        public bool Enabled { get; set; }
        public decimal? Ask { get; set; }
        public decimal? Bid { get; set; }
        public decimal? Last { get; set; }
        public bool PostOnly { get; set; }
        public decimal? Price { get; set; }
        public decimal PriceIncrement { get; set; }
        public decimal SizeIncrement { get; set; }
        public string Restricted { get; set; }
        public decimal? VolumeUsd24h { get; set; }
    }
}