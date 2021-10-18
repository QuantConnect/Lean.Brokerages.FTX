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
    /// <summary>
    /// Provides data on all new trades in the market
    /// </summary>
    public class Trade
    {
        /// <summary>
        /// Price of the trade
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Size of the trade
        /// </summary>
        public decimal Size { get; set; }

        /// <summary>
        /// Side of the taker in the trade
        /// </summary>
        public OrderDirection Side { get; set; }

        /// <summary>
        /// Timestamp
        /// </summary>
        public DateTime Time { get; set; }
    }
}