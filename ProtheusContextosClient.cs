using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class ProtheusContextosClient
{
    private readonly HttpClient _http;
    private readonly string _urlPost;
    private readonly string _urlPut;

    public ProtheusContextosClient(string baseUrlPost, string baseUrlPut, string user, string pass)
    {
        _urlPost = baseUrlPost;
        _urlPut = baseUrlPut;

        _http = new HttpClient();

        var authBytes = Encoding.ASCII.GetBytes($"{user}:{pass}");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task SyncContextoAsync(ContextoDto contexto)
    {
        var json = JsonSerializer.Serialize(contexto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // respeta JsonPropertyName
            WriteIndented = true
        });

        // 1) Probar POST
        var postResp = await SendAsync(_urlPost, HttpMethod.Post, json);

        if (postResp.StatusCode == HttpStatusCode.Conflict)
        {
            var body = await postResp.Content.ReadAsStringAsync();

            // Si ya existe (errorCode 003), hago PUT
            if (body.Contains("errorCode", StringComparison.OrdinalIgnoreCase) &&
                body.Contains("003", StringComparison.OrdinalIgnoreCase))
            {
                var putResp = await SendAsync(_urlPut, HttpMethod.Put, json);
                if (!putResp.IsSuccessStatusCode)
                {
                    var putBody = await putResp.Content.ReadAsStringAsync();
                    throw new Exception($"PUT falló ({(int)putResp.StatusCode}): {putBody}");
                }
                return;
            }

            // Conflicto por otra razón
            throw new Exception($"POST devolvió 409 (conflict). Respuesta: {body}");
        }

        if (!postResp.IsSuccessStatusCode)
        {
            var postBody = await postResp.Content.ReadAsStringAsync();
            throw new Exception($"POST falló ({(int)postResp.StatusCode}): {postBody}");
        }
    }

    private Task<HttpResponseMessage> SendAsync(string url, HttpMethod method, string json)
    {
        var req = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return _http.SendAsync(req);
    }
}
