using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevOps.Util.UnitTests
{
    public sealed class TestableHttpMessageHandler : HttpMessageHandler
    {
        public Dictionary<Uri, HttpResponseMessage> MessageMap { get; } = new Dictionary<Uri, HttpResponseMessage>();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (MessageMap.TryGetValue(request.RequestUri, out var response))
            {
                return Task.FromResult(response);
            }

            return Task.FromException<HttpResponseMessage>(new Exception("Unexpected request"));
        }

        internal void AddJson(string uri, string json)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(json, Encoding.UTF8);
            MessageMap[new Uri(uri)] = response;
        }
    }
}
