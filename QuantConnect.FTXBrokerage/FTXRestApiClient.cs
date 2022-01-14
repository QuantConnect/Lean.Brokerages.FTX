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

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using QuantConnect.Brokerages;
using QuantConnect.FTXBrokerage.Messages;
using QuantConnect.Logging;
using QuantConnect.Util;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace QuantConnect.FTXBrokerage
{
    /// <summary>
    /// FTX brokerage REST client and helpers
    /// </summary>
    public class FTXRestApiClient : IDisposable
    {
        public const string FtxRestEndpoint = "https://ftx.com/api";
        public const string FtxWsEndpoint = "wss://ftx.com/ws/";
        public const string FtxUsRestEndpoint = "https://ftx.us/api";
        public const string FtxUsWsEndpoint = "wss://ftx.us/ws/";

        private static readonly Dictionary<string, int> Tier2RateLimit = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Tier1", 6 },
            { "Tier2", 6 },
            { "Tier3", 6 },
            { "Tier4", 6 },
            { "Tier5", 6 },
            { "Tier6", 6 },
            { "Tier7", 6 },
            { "Tier8", 6 },
            { "Tier9", 6 },
            { "VIP1", 12 },
            { "VIP2", 30 },
            { "VIP3", 50 },
            { "MM1", 12 },
            { "MM2", 30 },
            { "MM3", 50 }
        };


        private readonly string _apiKey;
        private readonly string _apiSecret;
        private readonly HMACSHA256 _hashMaker;
        private readonly string _tier;
        private readonly string _marketName;
        private readonly string _headerPrefix;

        public static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.DateTimeOffset,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        // Rate gate limiter useful for REST API calls
        private readonly RateGate _restRateLimiter;

        /// <summary>
        /// The rest client instance
        /// </summary>
        private readonly IRestClient _restClient;

        private readonly Dictionary<string, string> _orderEndpoint = new()
        {
            { "market", "orders" },
            { "limit", "orders" },
            { "takeProfit", "conditional_orders" },
            { "stop", "conditional_orders" },
        };

        /// <summary>
        /// Can be used to access public endpoints
        /// </summary>
        /// <param name="restUrl">ftx exhcnage rest api endpoint (ftx or ftxus)</param>
        /// <param name="marketName">Market name matching ove of <see cref="Market"/> values</param>
        public FTXRestApiClient(string restUrl, string marketName) : this(null, null, restUrl, marketName)
        {
        }

        /// <summary>
        /// Creates FTX Rest API client
        /// </summary>
        /// <param name="apiKey">api access key</param>
        /// <param name="apiSecret">api access token</param>
        /// <param name="restUrl">ftx exhcnage rest api endpoint (ftx or ftxus)</param>
        /// <param name="marketName">Market name matching ove of <see cref="Market"/> values</param>
        public FTXRestApiClient(string apiKey, string apiSecret, string restUrl, string marketName)
            : this(new RestClient(restUrl), apiKey, apiSecret, marketName)
        {
        }

        /// <summary>
        /// Creates FTX Rest API client
        /// </summary>
        /// <param name="restClient">REST sharp client instance instance</param>
        /// <param name="apiKey">api access key</param>
        /// <param name="apiSecret">api access token</param>
        /// <param name="marketName">Market name matching ove of <see cref="Market"/> values</param>
        /// <param name="tier">account tier</param>
        public FTXRestApiClient(IRestClient restClient, string apiKey, string apiSecret, string marketName, string tier = "Tier1")
        {
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _restClient = restClient;

            _marketName = marketName;
            _headerPrefix = marketName.ToUpperInvariant();

            if (string.IsNullOrEmpty(tier))
            {
                throw new ArgumentNullException(nameof(tier), $"{_marketName} Tier cannot be null or empty");
            }

            if (!Tier2RateLimit.ContainsKey(tier))
            {
                throw new ArgumentException(nameof(tier), $"{_marketName} Tier passed cannot be recognized. Please use one of the following values: {string.Join(",", Tier2RateLimit.Keys)}");
            }

            _tier = tier;
            _restRateLimiter = new(Tier2RateLimit[_tier], TimeSpan.FromSeconds(1));
            if (!string.IsNullOrEmpty(_apiSecret))
            {
                _hashMaker = new HMACSHA256(Encoding.UTF8.GetBytes(_apiSecret));
            }
        }

        /// <summary>
        /// Gets the current cash balance for each currency held in the brokerage account. Requires authentication.
        /// </summary>
        /// <returns>The current cash balance for each currency available for trading</returns>
        internal List<Balance> GetBalances()
        {
            var path = "wallet/balances";
            var method = Method.GET;

            var request = CreateSignedRequest(method, path);
            var response = ExecuteRestRequest(request);

            return EnsureSuccessAndParse<List<Balance>>(response);
        }

        /// <summary>
        /// Gets the open orders (MARKET, LIMIT) in the brokerage account. Requires authentication.
        /// </summary>
        /// <returns>The open orders</returns>
        internal List<BaseOrder> GetOpenOrders()
            => FetchOpenOrders<Order>("orders").ToList<BaseOrder>();

        /// <summary>
        /// Gets the open trigger orders (STOP LOSS, TAKE PROFIT) in the brokerage account. Requires authentication.
        /// </summary>
        /// <returns>The open trigger orders</returns>
        internal List<BaseOrder> GetOpenTriggerOrders()
            => FetchOpenOrders<TriggerOrder>("conditional_orders").ToList<BaseOrder>();

        /// <summary>
        /// Covers all types of markets on FTX.
        /// </summary>
        /// <returns>Returns exchange information</returns>
        public ExchangeInfo[] GetAllMarkets()
        {
            var path = "/markets";

            var request = CreateRequest(Method.GET, path);
            var response = ExecuteRestRequest(request);

            var result = EnsureSuccessAndParse<ExchangeInfo[]>(response);

            return result;
        }

        /// <summary>
        /// Gets the history for the requested security
        /// </summary>
        /// <param name="market">symbol market ticker</param>
        /// <param name="resolutionInSeconds">resolution</param>
        /// <param name="startTimeUtc">start time (in UTC)</param>
        /// <param name="endTimeUtc">end time (in UTC)</param>
        /// <returns>An enumerable of bars covering the span specified in the request</returns>
        internal Candle[] GetHistoricalPrices(string market, int resolutionInSeconds, DateTime startTimeUtc, DateTime endTimeUtc)
        {
            var path = $"/markets/{market}/candles?resolution={resolutionInSeconds}"
                       + $"&start_time={(ulong)(Time.DateTimeToUnixTimeStamp(startTimeUtc))}"
                       + $"&end_time={(ulong)(Time.DateTimeToUnixTimeStamp(endTimeUtc))}";

            var request = CreateRequest(Method.GET, path);
            var response = ExecuteRestRequest(request);

            var result = EnsureSuccessAndParse<Candle[]>(response);

            return result;
        }

        /// <summary>
        /// Submit order to Brokerage
        /// </summary>
        /// <param name="body">order payload</param>
        /// <returns></returns>
        internal BaseOrder PlaceOrder(Dictionary<string, object> body)
        {
            var path = _orderEndpoint[(string)body["type"]];
            var method = Method.POST;

            var request = CreateSignedRequest(method, path, body);
            var response = ExecuteWithRateLimit(request);

            var result = EnsureSuccessAndParse<BaseOrder>(response);

            if (result.Id == 0)
            {
                throw new Exception($"Error parsing response from place order: {response.Content}");
            }

            return result;
        }

        /// <summary>
        /// Cancel the order. Sync for STOP orders, but async for Market and Limit orders
        /// </summary>
        /// <param name="orderType">order type</param>
        /// <param name="orderId">order id to be cancelled</param>
        /// <returns>True if the request was made for the order to be canceled, false otherwise</returns>
        internal bool CancelOrder(string orderType, ulong orderId)
        {
            var path = $"{_orderEndpoint[orderType]}/{orderId}";
            var method = Method.DELETE;

            var request = CreateSignedRequest(method, path);
            var response = ExecuteWithRateLimit(request);

            EnsureSuccessAndParse<string>(response);

            return true;
        }

        /// <summary>
        /// Get trigger order triggers 
        /// </summary>
        /// <param name="conditionalOrderId">conditional order id</param>
        /// <returns>Returns triggers for specific trigger orders</returns>
        internal List<ConditionalOrderTrigger> GetTriggers(ulong conditionalOrderId)
        {
            var path = $"conditional_orders/{conditionalOrderId}/triggers";
            var method = Method.GET;

            var request = CreateSignedRequest(method, path);
            var response = ExecuteWithRateLimit(request);

            return EnsureSuccessAndParse<List<ConditionalOrderTrigger>>(response);
        }

        /// <summary>
        /// Generates Authentication payload for Websocket API
        /// </summary>
        /// <returns>Returns object with required information</returns>
        internal object GenerateAuthPayloadForWebSocketApi()
        {
            var signature = GenerateSignature("websocket_login", out var nonce);
            return new
            {
                key = _apiKey,
                sign = signature,
                time = nonce
            };
        }

        /// <summary>
        /// Dispose of current FTX Rest API client
        /// </summary>
        public void Dispose()
        {
            _restRateLimiter?.DisposeSafely();
            _hashMaker?.DisposeSafely();
        }

        #region util

        private List<T> FetchOpenOrders<T>(string path)
        {
            var method = Method.GET;

            var request = CreateSignedRequest(method, path);
            var response = ExecuteRestRequest(request);

            return EnsureSuccessAndParse<List<T>>(response);
        }

        /// <summary>
        /// Hitting rate limits will result in HTTP 429 errors.
        /// Non-order placement requests do not count towards rate limits.
        /// Rate limits are tiered by account trading volumes.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private IRestResponse ExecuteWithRateLimit(IRestRequest request)
        {
            const int maxAttempts = 10;
            var attempts = 0;
            IRestResponse response;

            do
            {
                if (!_restRateLimiter.WaitToProceed(TimeSpan.Zero))
                {
                    Log.Trace("Brokerage.OnMessage(): " + new BrokerageMessageEvent(BrokerageMessageType.Warning, "RateLimit",
                        "The API request has been rate limited. To avoid this message, please reduce the frequency of API calls."));

                    _restRateLimiter.WaitToProceed();
                }

                response = ExecuteRestRequest(request);
                // 429 status code: Too Many Requests
            } while (++attempts < maxAttempts && (int)response.StatusCode == 429);

            return response;
        }

        private IRestResponse ExecuteRestRequest(IRestRequest request) => _restClient.Execute(request);

        private IRestRequest CreateRequest(Method method, string endpoint, string body = null)
        {
            var request = new RestRequest(endpoint, method);

            if (!string.IsNullOrEmpty(body))
            {
                request.AddParameter("", body, "application/json", ParameterType.RequestBody);
            }

            return request;
        }

        private IRestRequest CreateSignedRequest(Method method, string endpoint, object body = null)
        {
            var payload = body != null ? JsonConvert.SerializeObject(body, JsonSettings) : "";

            var request = CreateRequest(method, endpoint, payload);
            var sign = GenerateSignatureForPath(
                method,
                $"/{endpoint}",
                payload,
                out var nonce);

            request.AddHeaders(new List<KeyValuePair<string, string>>
            {
                new ($"{_headerPrefix}-KEY", _apiKey),
                new ($"{_headerPrefix}-SIGN", sign),
                new ($"{_headerPrefix}-TS", nonce.ToString())
            });

            return request;
        }

        private string GenerateSignatureForPath(Method method, string url, string requestBody, out long nonce)
        {
            var payload = $"{method.ToString().ToUpper()}/api{url}{requestBody}";
            return GenerateSignature(payload, out nonce);
        }

        private string GenerateSignature(string payload, out long nonce)
        {
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_apiSecret))
            {
                throw new InvalidOperationException("Private endpoints require incoming request signed using API Key and Secret");
            }

            nonce = GetNonce();
            var hash = _hashMaker.ComputeHash(Encoding.UTF8.GetBytes($"{nonce}{payload}"));
            var hashStringBase64 = BitConverter.ToString(hash).Replace("-", string.Empty);
            return hashStringBase64.ToLower();
        }

        private T EnsureSuccessAndParse<T>(IRestResponse response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception("FtxRestApiClient request failed: " +
                                    $"[{(int)response.StatusCode}] {response.StatusDescription}, " +
                                    $"Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
            }

            var ftxResponse = JsonConvert.DeserializeObject<Response<T>>(response.Content, JsonSettings);
            if (ftxResponse?.Success != true)
            {
                throw new Exception("FtxRestApiClient request failed: " +
                                    $"[{(int)response.StatusCode}] {response.StatusDescription}, " +
                                    $"Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
            }

            return ftxResponse.Result;
        }

        private long GetNonce() => Convert.ToInt64(Time.DateTimeToUnixTimeStampMilliseconds(DateTime.UtcNow));

        #endregion
    }
}