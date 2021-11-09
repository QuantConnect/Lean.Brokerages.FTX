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

using System;
using QuantConnect.Orders;

namespace QuantConnect.FTXBrokerage.Messages
{
    public class Fill
    {
        public string Market { get; set; }
        public string BaseCurrency { get; set; }
        public string QuoteCurrency { get; set; }
        public string Type { get; set; }
        public OrderDirection Side { get; set; }
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public long OrderId { get; set; }
        public DateTime Time { get; set; }
        public string FeeCurrency { get; set; }
        public decimal Fee { get; set; }
        public decimal FeeRate { get; set; }
        public decimal Quantity => Side == OrderDirection.Buy ? Size : -Size;
    }
}