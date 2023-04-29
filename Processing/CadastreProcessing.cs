using Newtonsoft.Json;

namespace CoGISBot.Telegram.Processing;

public class CadastreProcessing
{
    public static async Task<CadastreFindResponse> Find(string query, string mapServerUrl)
    {
        var httpUrl = new Uri(mapServerUrl + $"/find?f=json&returnGeometry=false&returnZ=false&sr=4326&layers=20, 30, 51, 52&layerDefs={{\"51\":\"label_text <> '0000000'\"}}&searchText={query}:&searchFields=cadastral_number&contains=true");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, httpUrl);
        using var httpClient = new HttpClient();
        using var httpResponse = await httpClient.SendAsync(httpRequest);
        return JsonConvert.DeserializeObject<CadastreFindResponse>(await httpResponse.Content.ReadAsStringAsync()) ?? new();
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class CadastreFindResponse
{
    public class CadastreFindResult
    {

    }
}