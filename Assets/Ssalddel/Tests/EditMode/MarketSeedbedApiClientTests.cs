using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Ssalddel.Unity.Exhibition;
using Ssalddel.Unity.Infrastructure.UrbanMarket;
using Ssalddel.Unity.Runtime.Transport;

namespace Ssalddel.Unity.Tests.EditMode
{
    public sealed class MarketSeedbedApiClientTests
    {
        [Test]
        public async Task 공개상품Client는_기존경로와한국어JSON을다품목으로변환한다()
        {
            var transport = new RecordingApiClient(ProductListResponse());
            var client = new OperationalMarketProductApiClient(transport);

            var result = await client.GetAsync();

            Assert.That(result.Items.Length, Is.EqualTo(3));
            Assert.That(result.Items[0].상품명, Is.EqualTo("감자"));
            Assert.That(result.Items[1].상품명, Is.EqualTo("쌀"));
            Assert.That(result.Items[2].상품명, Is.EqualTo("양파"));
            Assert.That(transport.Requests[0].Method, Is.EqualTo("GET"));
            Assert.That(transport.Requests[0].RelativePath,
                Is.EqualTo(MarketProductBusinessApiRoutes.PublicProducts));
            Assert.That(transport.Requests[0].RequiresAuthentication, Is.False);
        }

        [Test]
        public async Task 주문의향등록은_인증요청과서버한국어계약을보낸다()
        {
            var response = OrderIntentResponse();
            var transport = new RecordingApiClient(response);
            var client = new OperationalMarketOrderIntentApiClient(transport);
            var command = Command();

            var result = await client.등록Async(command);

            var request = transport.Requests[0];
            Assert.That(request.Method, Is.EqualTo("POST"));
            Assert.That(request.RelativePath, Is.EqualTo(MarketProductBusinessApiRoutes.OrderRequests));
            Assert.That(request.RequiresAuthentication, Is.True);
            Assert.That(request.JsonBody, Does.Contain("\"신청개인정보동의증적Id\""));
            Assert.That(request.JsonBody, Does.Contain("\"신청출처Code\":\"UnitySeedbed\""));
            Assert.That(request.JsonBody, Does.Contain("\"공개상품Id\":41"));
            Assert.That(request.JsonBody, Does.Contain("\"수량\":2"));
            Assert.That(request.JsonBody, Does.Contain("\"비구속주문요청확인\":true"));
            Assert.That(result.주문요청Id, Is.EqualTo(OrderRequestId));
            Assert.That(result.재고예약됨, Is.False);
            Assert.That(result.결제됨, Is.False);
        }

        [Test]
        public async Task 주문의향상세재조회는_서버반환ID를경로에사용한다()
        {
            var transport = new RecordingApiClient(OrderIntentResponse());
            var client = new OperationalMarketOrderIntentApiClient(transport);

            await client.상세조회Async(OrderRequestId);

            var request = transport.Requests[0];
            Assert.That(request.Method, Is.EqualTo("GET"));
            Assert.That(request.RelativePath,
                Is.EqualTo("api/v1/orderer/mart/order-requests/" + OrderRequestId.ToString("D")));
            Assert.That(request.RequiresAuthentication, Is.True);
        }

        [Test]
        public void HTTP실패는_Simulation상품으로대체하지않고그대로드러낸다()
        {
            var transport = new RecordingApiClient(new UnityApiResponse
            {
                StatusCode = 401,
                ErrorCode = "Unauthorized",
                Body = "{}",
            });
            var client = new OperationalMarketOrderIntentApiClient(transport);

            var error = Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await client.등록Async(Command()));

            Assert.That(error.Message, Does.StartWith("MarketOrderIntentCreateRequestFailed:401"));
        }

        private static readonly Guid OrderRequestId =
            Guid.Parse("20bfba93-158c-46e6-a226-87d2944294e8");

        private static MarketOrderIntentCommandApiModel Command()
            => new MarketOrderIntentCommandApiModel
            {
                신청개인정보동의증적Id =
                    Guid.Parse("f5f50cbe-1a55-4610-b9a6-3948cd4a573d"),
                신청출처Code = "UnitySeedbed",
                클라이언트요청Id =
                    Guid.Parse("f782c26f-04da-477c-9815-0731772f2c7d"),
                공개상품Id = 41,
                수량 = 2,
                비구속주문요청확인 = true,
                안내버전 = MarketOrderIntentCoordinator.CurrentNoticeVersion,
            };

        private static UnityApiResponse ProductListResponse()
            => Success(@"{
  ""items"": [
    { ""id"": 1, ""상품명"": ""감자"", ""판매단위"": ""20kg"", ""판매가"": 35000, ""판매가능수량"": 12, ""판매가능여부"": true, ""재고기준시각Utc"": ""2026-08-12T01:00:00Z"", ""수정일시Utc"": ""2026-08-12T01:01:00Z"" },
    { ""id"": 2, ""상품명"": ""쌀"", ""판매단위"": ""10kg"", ""판매가"": 42000, ""판매가능수량"": 8, ""판매가능여부"": true, ""재고기준시각Utc"": ""2026-08-12T01:00:00Z"", ""수정일시Utc"": ""2026-08-12T01:01:00Z"" },
    { ""id"": 3, ""상품명"": ""양파"", ""판매단위"": ""10kg"", ""판매가"": 18000, ""판매가능수량"": 4, ""판매가능여부"": true, ""재고기준시각Utc"": ""2026-08-12T01:00:00Z"", ""수정일시Utc"": ""2026-08-12T01:01:00Z"" }
  ],
  ""totalCount"": 3,
  ""재고기준안내"": ""판매 가능 수량이며 내부 재고가 아닙니다.""
}");

        private static UnityApiResponse OrderIntentResponse()
            => Success(@"{
  ""주문요청Id"": ""20bfba93-158c-46e6-a226-87d2944294e8"",
  ""공개상품Id"": 41,
  ""상품명"": ""양파"",
  ""판매단위"": ""10kg"",
  ""단가"": 18000,
  ""수량"": 2,
  ""합계"": 36000,
  ""통화"": ""KRW"",
  ""제출시판매가능수량"": 8,
  ""재고기준시각Utc"": ""2026-08-12T01:00:00Z"",
  ""상태코드"": ""Submitted"",
  ""안내버전"": ""2026-07-20"",
  ""제출일시Utc"": ""2026-08-12T01:01:00Z"",
  ""재고예약됨"": false,
  ""결제됨"": false
}");

        private static UnityApiResponse Success(string body)
            => new UnityApiResponse { StatusCode = 200, Body = body };

        private sealed class RecordingApiClient : IOperationalUnityApiClient
        {
            private readonly UnityApiResponse response;

            public RecordingApiClient(UnityApiResponse value)
            {
                response = value;
            }

            public List<UnityApiRequest> Requests { get; } = new List<UnityApiRequest>();

            public Task<UnityApiResponse> SendAsync(
                UnityApiRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Requests.Add(request);
                return Task.FromResult(response);
            }
        }
    }
}
