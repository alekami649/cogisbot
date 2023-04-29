using Newtonsoft.Json;

namespace CoGISBot.Telegram.Processing;

public class CadastreProcessing
{
    public static async Task<CadastreFindResponse> Find(string query, string mapServerUrl)
    {
        var httpUrl = new Uri(mapServerUrl + $"/find?f=json&resultRecordCount=5&layers=20, 30, 51, 52&searchText={query}&searchFields=cadastral_number&contains=true");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, httpUrl);
        using var httpClient = new HttpClient();
        using var httpResponse = await httpClient.SendAsync(httpRequest);
        return JsonConvert.DeserializeObject<CadastreFindResponse>(await httpResponse.Content.ReadAsStringAsync()) ?? new();
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class CadastreFindResponse
{
    [JsonProperty("results")]
    public List<CadastreFindResult> Results { get; set; } = new();

    public class CadastreFindResult
    {
        [JsonProperty("attributes")]
        public CadastreFindAttributes Attributes { get; set; } = new();
    }
    public class CadastreFindAttributes
    {
        [JsonProperty("Полный адрес")] //wtf
        public string Address { get; set; } = "";

        [JsonProperty("Кадастровый номер")] //wtf no.2
        public string Number { get; set; } = "00:00:0000";
    }
}