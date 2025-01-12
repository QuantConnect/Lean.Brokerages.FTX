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

using System;

namespace QuantConnect.FTXBrokerage.Messages
{
    /// <summary>
    /// Described trigger event for conditional order
    /// https://docs.ftx.com/#get-trigger-order-triggers
    /// </summary>
    public class ConditionalOrderTrigger
    {
        /// <summary>
        /// Reason for order failing to be placed, null if successful
        /// </summary>
        public string Error { get; set; }
        public decimal? FilledSize { get; set; }
        /// <summary>
        /// null if order failed to place
        /// </summary>
        public decimal? OrderSize { get; set; }
        /// <summary>
        /// null if order failed to place
        /// </summary>
        public ulong? OrderId { get; set; }
        public DateTime Time { get; set; }
    }
}