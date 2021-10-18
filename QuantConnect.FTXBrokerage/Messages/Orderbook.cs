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
    /// <summary>
    /// Provides data about the orderbook's best 100 orders on either side
    /// Can be used for both snapshot and updates
    /// </summary>
    public class Orderbook
    {
        /// <summary>
        /// Timestamp
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// Formatted like that [[best price, size at price], [next next best price, size at price], ...]
        /// If the bid size at price 5220.5 changed to 20.2, the bids field would be: [[5220.5, 20.2]]
        /// If all bid at price 5223.5 got canceled, the bids field would contain: [[5233.5, 0]]
        /// </summary>
        public decimal[][] Bids { get; set; }

        /// <summary>
        /// Formatted like that [[best price, size at price], [next next best price, size at price], ...]
        /// If the asks size at price 5220.5 changed to 20.2, the asks field would be: [[5220.5, 20.2]]
        /// If all asks at price 5223.5 got canceled, the asks field would contain: [[5233.5, 0]]
        /// </summary>
        public decimal[][] Asks { get; set; }
    }
}