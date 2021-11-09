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

namespace QuantConnect.FTXBrokerage.Messages
{
    /// <summary>
    /// Describes historical prices.
    /// </summary>
    public class Candle
    {
        /// <summary>
        /// Mark price at startTime
        /// </summary>
        public decimal Open { get; set; }
        /// <summary>
        /// Mark price at the end of the window
        /// </summary>
        public decimal Close { get; set; }
        /// <summary>
        /// Highest mark price over the window
        /// </summary>
        public decimal High { get; set; }
        /// <summary>
        /// Lowest mark price over the window
        /// </summary>
        public decimal Low { get; set; }
        /// <summary>
        /// Mark price at startTime
        /// </summary>
        public DateTime StartTime { get; set; }
        /// <summary>
        /// Volume traded in the window
        /// </summary>
        public decimal Volume { get; set; }
    }
}