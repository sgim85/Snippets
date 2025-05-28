// In an test project init function or constructore,  Mock IHttpClientFactory then "Setup" the CreateTime method that returns a mock HttpClient
 _httpClientFactory = new Mock<IHttpClientFactory>();
 _httpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

// Another detailed example of "Setup" if you want more control over the HttpClient's behavior
 _httpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(() =>
 {
     var handler = new Mock<HttpMessageHandler>();
     handler.Protected()
         .Setup<Task<HttpResponseMessage>>("SendAsync",
             It.IsAny<HttpRequestMessage>(),
             It.IsAny<CancellationToken>())
         .ReturnsAsync(new HttpResponseMessage
         {
             StatusCode = HttpStatusCode.OK,
             Content = new StringContent("{\"key\":\"value\"}", Encoding.UTF8, "application/json")
         });

     return new HttpClient(handler.Object);
 });


// Any instance that uses IHttpClientFactory can now be injected with the mock factory. See _utils in the example below.
 public class HttpUtilsTests
{
    private Utils _utils;

    public HttpUtilsTests()
    {
        // ... other initializations here

        _utils = new Utils(_httpContextAccessorMock.Object, _synchConverter, _logger.Object, _httpClientFactory.Object);
    }
}
 