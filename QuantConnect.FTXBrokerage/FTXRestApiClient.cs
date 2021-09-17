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

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using QuantConnect.Brokerages;
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
    public class FTXRestApiClient : IDisposable
    {
        private readonly string _apiKey;
        private readonly string _apiSecret;
        private readonly HMACSHA256 _hashMaker;

        public static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateParseHandling = DateParseHandling.DateTime,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        // Rate gate limiter useful for REST API calls
        private readonly RateGate _restRateLimiter = new(6, TimeSpan.FromSeconds(1));

        /// <summary>
        /// The rest client instance
        /// </summary>
        private readonly IRestClient _restClient;

        public FTXRestApiClient(IRestClient restClient, string apiKey, string apiSecret)
        {
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _restClient = restClient;
            _hashMaker = new HMACSHA256(Encoding.UTF8.GetBytes(_apiSecret));
        }

        internal List<Balance> GetBalances()
        {
            var path = "wallet/balances";
            var method = Method.GET;

            var sign = GenerateSignature(method, $"/{path}", "", out var nonce);

            var request = CreateSignedRequest(method, path, sign, nonce);
            var response = ExecuteRestRequest(request);

            return EnsureSuccessAndParse<List<Balance>>(response);
        }

        internal List<BaseOrder> GetOpenOrders()
            => FetchOpenOrders<Order>("orders").ToList<BaseOrder>();

        internal List<BaseOrder> GetOpenTriggerOrders()
            => FetchOpenOrders<TriggerOrder>("conditional_orders").ToList<BaseOrder>();

        private List<T> FetchOpenOrders<T>(string path)
        {
            var method = Method.GET;

            var sign = GenerateSignature(method, $"/{path}", null, out var nonce);

            var request = CreateSignedRequest(method, path, sign, nonce);
            var response = ExecuteRestRequest(request);
            
            return EnsureSuccessAndParse<List<T>>(response);
        }

        internal BaseOrder PlaceOrder(Dictionary<string, object> body)
        {
            var path = "orders";
            var method = Method.POST;

            var json = JsonConvert.SerializeObject(body, JsonSettings);

            var sign = GenerateSignature(method, $"/{path}", json, out var nonce);
            var request = CreateSignedRequest(method, path, sign, nonce);
            var response = ExecuteWithRateLimit(request);

            var result = EnsureSuccessAndParse<Order>(response);

            if (result.Id == 0)
            {
                throw new Exception($"Error parsing response from place order: {response.Content}");
            }

            return result;
        }

        public void Dispose()
        {
            _restRateLimiter?.DisposeSafely();
            _hashMaker?.DisposeSafely();
        }

        #region util
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

        private IRestRequest CreateRequest(Method method, string endpoint, object body = null)
        {
            var request = new RestRequest(endpoint, method);

            if (body != null)
            {
                request.AddJsonBody(body);
            }

            return request;
        }

        private IRestRequest CreateSignedRequest(Method method, string endpoint, string sign, long nonce, string body = null)
        {
            var request = CreateRequest(method, endpoint, body);

            request.AddHeaders(new List<KeyValuePair<string, string>>
            {
                new ("FTX-KEY", _apiKey),
                new ("FTX-SIGN", sign),
                new ("FTX-TS", nonce.ToString())
            });

            return request;
        }

        private string GenerateSignature(Method method, string url, string requestBody, out long nonce)
        {
            nonce = GetNonce();
            var signature = $"{nonce}{method.ToString().ToUpper()}/api{url}{requestBody}";
            var hash = _hashMaker.ComputeHash(Encoding.UTF8.GetBytes(signature));
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