using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BlueBirdDX.PublicApi;

public sealed class BlueBirdClient
{
    private static readonly HttpClient SharedClient = new HttpClient();
    
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    static BlueBirdClient()
    {
        Version version = typeof(BlueBirdClient).Assembly.GetName().Version!;

        SharedClient.DefaultRequestHeaders.Add("User-Agent",
            $"BlueBirdDX.PublicApi/{version.Major}.{version.Minor}.{version.Revision}");
    }

    public BlueBirdClient(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    public BlueBirdClient(string baseUrl) : this(SharedClient, baseUrl)
    {
        //
    }

    private async Task<HttpResponseMessage> SendRequestInternal(HttpMethod method, string endpoint,
        HttpContent? bodyContent = null)
    {
        StringBuilder urlBuilder = new StringBuilder();

        if (_baseUrl[_baseUrl.Length - 1] == '/')
        {
            urlBuilder.Append(_baseUrl, 0, _baseUrl.Length - 1);
        }
        else
        {
            urlBuilder.Append(_baseUrl);
        }

        if (endpoint.Length == 0)
        {
            throw new Exception("");
        }

        urlBuilder.Append(endpoint);

        string url = urlBuilder.ToString();

        HttpRequestMessage requestMessage = new HttpRequestMessage(method, url)
        {
            Content = bodyContent
        };
        
        HttpResponseMessage responseMessage = await _httpClient.SendAsync(requestMessage);
        
        if (!responseMessage.IsSuccessStatusCode)
        {
            string response;
            
            try
            {
                response = await responseMessage.Content.ReadAsStringAsync();
            }
            catch (Exception)
            {
                throw new BlueBirdException(
                    $"Received HTTP status code {responseMessage.StatusCode}, failed to read response");
            }

            throw new BlueBirdException($"Status code {responseMessage.StatusCode}, response: \"" + response + "\"");
        }

        return responseMessage;
    }
    
    
    public async Task EnqueuePostThread(PostThreadApi apiThread)
    {
        apiThread.State = 1;
        
        string json = JsonSerializer.Serialize(apiThread);

        await SendRequestInternal(HttpMethod.Post, "/api/v1/thread",
            new StringContent(json, Encoding.UTF8, "application/json"));
    }
}