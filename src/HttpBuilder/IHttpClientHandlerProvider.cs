using System.Net.Http;

namespace HttpBuilder
{
    public interface IHttpClientHandlerProvider
    {
        HttpClientHandler CreateHandler();
    }
}
