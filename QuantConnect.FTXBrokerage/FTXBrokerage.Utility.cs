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
using QuantConnect.Brokerages;
using QuantConnect.Data.Fundamental;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.FTXBrokerage
{
    public partial class FTXBrokerage
    {
        private CashAmount ConvertBalance(Balance wallet)
            => new(wallet.Total, wallet.Coin);

        private Orders.Order CreateOrder(Order ftxOrder)
        {
            switch (ftxOrder.Type.LazyToUpper())
            {
                case "LIMIT":
                    return new LimitOrder { LimitPrice = ftxOrder.Price };
                case "MARKET":
                    return new MarketOrder();
                default:
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1,
                        $"FTXBrokerage.GetOpenOrders: Unsupported order type returned from brokerage: {ftxOrder.Type}"));
                    return null;
            }
        }

        private Orders.Order CreateTriggerOrder(TriggerOrder ftxOrder)
        {
            switch (ftxOrder.Type.LazyToUpper())
            {
                case "STOP":
                    {
                        if (ftxOrder.OrderType.ToUpper() == "LIMIT")
                        {
                            return new StopLimitOrder { StopPrice = ftxOrder.TriggerPrice, LimitPrice = ftxOrder.OrderPrice };
                        }

                        return ftxOrder.OrderType.ToUpper() == "MARKET"
                            ? new StopMarketOrder { StopPrice = ftxOrder.TriggerPrice }
                            : null;
                    }
                case "TAKE_PROFIT":
                    {
                        if (ftxOrder.OrderType.ToUpper() == "LIMIT")
                        {
                            return new StopLimitOrder { StopPrice = ftxOrder.TriggerPrice, LimitPrice = ftxOrder.OrderPrice };
                        }

                        return ftxOrder.OrderType.ToUpper() == "MARKET"
                            ? new StopMarketOrder { StopPrice = ftxOrder.TriggerPrice }
                            : null;
                    }
                default:
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1,
                        $"FTXBrokerage.GetOpenOrders: Unsupported order type returned from brokerage: {ftxOrder.Type}"));
                    return null;
            }
        }

        private static OrderStatus ConvertOrderStatus(BaseOrder order)
        {
            switch (order.Status.LazyToUpper())
            {
                case "NEW":
                    return OrderStatus.New;

                case "OPEN":
                    return order.FilledSize == 0
                        ? OrderStatus.Submitted
                        : OrderStatus.PartiallyFilled;

                case "CLOSED":
                    return order.FilledSize == order.Size
                        ? OrderStatus.Filled
                        : OrderStatus.Canceled;

                default:
                    return OrderStatus.None;
            }
        }
    }
}
