using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlueBirdDX.Util.TextWrapper;

public class TextWrapperClient
{
    class UrlEntry
    {
        [JsonPropertyName("url")]
        public string Url
        {
            get;
            set;
        }

        [JsonPropertyName("indices")]
        public List<int> Indices
        {
            get;
            set;
        }
    }
    
    class HashtagEntry
    {
        [JsonPropertyName("hashtag")]
        public string Hashtag
        {
            get;
            set;
        }

        [JsonPropertyName("indices")]
        public List<int> Indices
        {
            get;
            set;
        }
    }

    class CharacterCountResponse
    {
        [JsonPropertyName("length")]
        public int Length
        {
            get;
            set;
        }
    }
    
    private static readonly HttpClient SharedClient = new HttpClient();
    
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    static TextWrapperClient()
    {
        Version version = typeof(TextWrapperClient).Assembly.GetName().Version!;

        SharedClient.DefaultRequestHeaders.Add("User-Agent",
            $"TextWrapperClient/{version.Major}.{version.Minor}.{version.Revision}");
    }

    public TextWrapperClient(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    public TextWrapperClient(string baseUrl) : this(SharedClient, baseUrl)
    {
        //
    }

    private async Task<HttpResponseMessage> SendRequestInternal(string endpoint, string text)
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

        string requestJson = JsonSerializer.Serialize(new Dictionary<string, string>()
        {
            { "text", text }
        });

        HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
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
                throw new Exception(
                    $"Received HTTP status code {responseMessage.StatusCode}, failed to read response");
            }

            throw new Exception($"Status code {responseMessage.StatusCode}, response: \"" + response + "\"");
        }

        return responseMessage;
    }

    public async Task<int> CountCharacters(string text)
    {
        HttpResponseMessage responseMessage = await SendRequestInternal("/api/count-characters", text);

        CharacterCountResponse countResponse = (await responseMessage.Content.ReadFromJsonAsync<CharacterCountResponse>())!;

        return countResponse.Length;
    }

    public async Task<List<ExtractedChunk>> ExtractUrls(string text)
    {
        HttpResponseMessage responseMessage = await SendRequestInternal("/api/extract-urls", text);

        List<UrlEntry> entries = (await responseMessage.Content.ReadFromJsonAsync<List<UrlEntry>>())!;

        return entries.Select(e => new ExtractedChunk()
        {
            Data = e.Url,
            Start = e.Indices[0],
            End = e.Indices[1]
        }).ToList();
    }
    
    public async Task<List<ExtractedChunk>> ExtractHashtags(string text)
    {
        HttpResponseMessage responseMessage = await SendRequestInternal("/api/extract-hashtags", text);

        List<HashtagEntry> entries = (await responseMessage.Content.ReadFromJsonAsync<List<HashtagEntry>>())!;

        return entries.Select(e => new ExtractedChunk()
        {
            Data = e.Hashtag,
            Start = e.Indices[0],
            End = e.Indices[1]
        }).ToList();
    }
}