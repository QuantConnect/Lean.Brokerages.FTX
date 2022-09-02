![header-cheetah](https://user-images.githubusercontent.com/79997186/184224088-de4f3003-0c22-4a17-8cc7-b341b8e5b55d.png)

&nbsp;
&nbsp;
&nbsp;

## Introduction

This repository hosts the FTX and FTXUS Brokerage Plugin Integration with the QuantConnect LEAN Algorithmic Trading Engine. LEAN is a brokerage agnostic operating system for quantitative finance. Thanks to open-source plugins such as this [LEAN](https://github.com/QuantConnect/Lean) can route strategies to almost any market.

[LEAN](https://github.com/QuantConnect/Lean) is maintained primarily by [QuantConnect](https://www.quantconnect.com), a US based technology company hosting a cloud algorithmic trading platform. QuantConnect has successfully hosted more than 200,000 live algorithms since 2015, and trades more than $1B volume per month.

### About FTX

<p align="center">
<picture >
 <source media="(prefers-color-scheme: dark)" srcset="https://user-images.githubusercontent.com/79997186/188238558-83ea0d18-0d81-43e1-9b2b-0ca80f7df6f0.png">
 <source media="(prefers-color-scheme: light)" srcset="https://user-images.githubusercontent.com/79997186/188238550-061abf06-c153-4a1c-af5f-b6dd03ab6315.png">
 <img alt="FTX" width="20%">
</picture>
<p>

[FTX](https://ftx.com/) was founded by Sam Bankman-Fried in 2017 with the goal to "develop a platform robust enough for professional trading firms and intuitive enough for first-time users". FTX provides access to trading Crypto, tokenized Equities, and Bitcoin Options for clients outside of the [restricted locations](https://help.ftx.com/hc/en-us/articles/360042412652-Location-Restrictions-) with no minimum deposit. FTX also provides a platform-native FTT token, an NFT marketplace, and margin borrowing services.

![deploy-ftx](https://user-images.githubusercontent.com/38889814/188245340-aeeb0e6c-f981-4679-878a-20d249dbf922.gif)

For more information about the FTX brokerage, see the [QuantConnect-FTX Integration Page](https://www.quantconnect.com/docs/v2/our-platform/live-trading/brokerages/ftx).

## Using the Brokerage Plugin
  
### Deploying FTX with VSCode User Interace

  You can deploy using a visual interface in the QuantConnect Cloud. For more information see the [QuantConnect-FTX Integration Page](https://www.quantconnect.com/brokerages/ftx).

  In the QuantConnect Cloud Platform you can harness the QuantConnect Live Data Feed. For most users this is substantially cheaper and easier than self-hosting.
  
### Deploying FTX with LEAN CLI

Follow these steps to start local live trading with the FTX brokerage:

1.  Open a terminal in your [CLI root directory](https://www.quantconnect.com/docs/v2/lean-cli/initialization/directory-structure#02-lean-init).
2.  Run lean live "`<projectName>`" to start a live deployment wizard for the project in ./`<projectName>` and then enter the brokerage number.

    ```
    $ lean live 'My Project'
     
    Select a brokerage:
    1. Paper Trading
    2. Interactive Brokers
    3. Tradier
    4. OANDA
    5. Bitfinex
    6. Coinbase Pro
    7. Binance
    8. Zerodha
    9. Samco
    10. Terminal Link
    11. Atreyu
    12. Trading Technologies
    13. Kraken
    14. FTX
    Enter an option: 
    ```
  
3.  Enter the number of the organization that has a subscription for the FTX module.

    ```
    $ lean live "My Project"

    Select the organization with the FTX module subscription:
    1. Organization 1
    2. Organization 2
    3. Organization 3
       Enter an option: 1
    ```

4.  Enter the exchange to use.

    ```
    $ lean live "My Project"

    FTX Exchange (FTX, FTXUS): FTXUS
    ```

5.  Enter your API key, API secret, and account tier.

    ```
    $ lean live "My Project"

    API key: 
    API secret: 
    Select the Account Tier (Tier1, Tier2, Tier3, Tier4, Tier5, Tier6, VIP1, VIP2, VIP3, MM1, MM2, MM3):
    ```

    To create new API credentials and to check your account tier, see your Profile page on the [FTX](https://ftx.com/profile) or [FTX US](https://ftx.us/profile) website. If your account tier changes after you deploy the algorithm, stop the algorithm and then redeploy it to correct the account tier.

6.  Enter the number of the data feed to use and then follow the steps required for the data connection.

    ``` 
    $ lean live 'My Project'

    Select a data feed:
    1. Interactive Brokers
    2. Tradier
    3. Oanda
    4. Bitfinex
    5. Coinbase Pro
    6. Binance
    7. Zerodha
    8. Samco
    9. Terminal Link
    10. Trading Technologies
    11. Kraken
    12. FTX
    13. IQFeed
    14. Polygon Data Feed
    15. Custom Data Only
  
        To enter multiple options, separate them with comma:
    ```

7. View the result in the `<projectName>/live/<timestamp>` directory. Results are stored in real-time in JSON format. You can save results to a different directory by providing the `--output <path>` option in step 2.

If you already have a live environment configured in your [Lean configuration file](https://www.quantconnect.com/docs/v2/lean-cli/initialization/configuration#03-Lean-Configuration), you can skip the interactive wizard by providing the `--environment <value>` option in step 2. The value of this option must be the name of an environment which has `live-mode` set to true.

## Account Types

FTX and FTX US support cash and margin accounts.

## Order Types and Asset Classes

Our FTX and FTX US integrations support trading Crypto and the following order types:

- Market Order
- Limit Order
- Limit-If-Touched Order
- Stop Market Order
- Stop-Limit Order
- Market-On-Open Order
- Market-On-Close Order

## Downloading Data

For local deployment, the algorithm needs to download the following dataset:

[FTX Crypto Price Data](https://www.quantconnect.com/datasets/ftx-crypto-price-data)  
[FTX US Crypto Price Data](https://www.quantconnect.com/datasets/ftx-us-crypto-price-data)  


## Brokerage Model

Lean models the brokerage behavior for backtesting purposes. The margin model is used in live trading to avoid placing orders that will be rejected due to insufficient buying power.

You can set the Brokerage Model with the following statements

    SetBrokerageModel(BrokerageName.FTX, AccountType.Cash);
    SetBrokerageModel(BrokerageName.FTX, AccountType.Margin);

    SetBrokerageModel(BrokerageName.FTXUS, AccountType.Cash);
    SetBrokerageModel(BrokerageName.FTXUS, AccountType.Margin);

[Read Documentation](https://www.quantconnect.com/docs/v2/our-platform/live-trading/brokerages/ftx)

### Fees

We model the order fees of FTX and FTX US at the lowest tier in their respective tiered fee structures. The following table shows the fees of each platform:

|Platform|Maker Fee (%)|Taker Fee (%)|
|:----|----:|----:|
|FTX|0.02|0.07|
|FTX US|0.1|0.4|

If you add liquidity to the order book by placing a limit order that doesn't cross the spread, you pay maker fees. If you remove liquidity from the order book by placing an order that crosses the spread, you pay taker fees. You pay the fees in the base currency for maker orders and the quote currency for taker orders. FTX and FTXUS adjust your fees based on your 30-day trading volume and FTT balance, but we don't currently model these metrics to adjust fees. To check the latest fees at all the fee tiers, see the Fees page on the [FTX.com](https://help.ftx.com/hc/en-us/articles/360024479432-Fees) or [FTX.us](https://help.ftx.us/hc/en-us/articles/360043579273-Fees) website.

### Margin

We model buying power and margin calls to ensure your algorithm stays within the margin requirements.

#### Buying Power

FTX and FTX US allow up to 3x leverage for margin accounts.

#### Margin Calls

Regulation T margin rules apply. When the amount of margin remaining in your portfolio drops below 5% of the total portfolio value, you receive a [warning](https://www.quantconnect.com/docs/v2/writing-algorithms/reality-modeling/margin-calls#08-Monitor-Margin-Call-Events). When the amount of margin remaining in your portfolio drops to zero or goes negative, the portfolio sorts the generated margin call orders by their unrealized profit and executes each order synchronously until your portfolio is within the margin requirements.

### Slippage

Orders through FTX and FTX US do not experience slippage in backtests. In live trading, your orders may experience slippage.

### Fills

We fill market orders immediately and completely in backtests. In live trading, if the quantity of your market orders exceeds the quantity available at the top of the order book, your orders are filled according to what is available in the order book.

### Settlements

Trades settle immediately after the transaction.

### Deposits and Withdraws

You can deposit and withdraw cash from your brokerage account while you run an algorithm that's connected to the account. We sync the algorithm's cash holdings with the cash holdings in your brokerage account every day at 7:45 AM Eastern Time (ET).

&nbsp;
&nbsp;
&nbsp;

![whats-lean](https://user-images.githubusercontent.com/79997186/184042682-2264a534-74f7-479e-9b88-72531661e35d.png)

&nbsp;
&nbsp;
&nbsp;

LEAN Engine is an open-source algorithmic trading engine built for easy strategy research, backtesting, and live trading. We integrate with common data providers and brokerages, so you can quickly deploy algorithmic trading strategies.

The core of the LEAN Engine is written in C#, but it operates seamlessly on Linux, Mac and Windows operating systems. To use it, you can write algorithms in Python 3.8 or C#. QuantConnect maintains the LEAN project and uses it to drive the web-based algorithmic trading platform on the website.

## Contributions

Contributions are warmly very welcomed but we ask you to read the existing code to see how it is formatted, commented and ensure contributions match the existing style. All code submissions must include accompanying tests. Please see the [contributor guide lines](https://github.com/QuantConnect/Lean/blob/master/CONTRIBUTING.md).

## Code of Conduct

We ask that our users adhere to the community [code of conduct](https://www.quantconnect.com/codeofconduct) to ensure QuantConnect remains a safe, healthy environment for
high quality quantitative trading discussions.

## License Model

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. You
may obtain a copy of the License at

<http://www.apache.org/licenses/LICENSE-2.0>

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language
governing permissions and limitations under the License.
