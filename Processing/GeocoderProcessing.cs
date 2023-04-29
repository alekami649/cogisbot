using Newtonsoft.Json;

namespace CoGISBot.Telegram.Processing;

public class GeocoderProcessing
{
    public static async Task<AddressCandidatesResponse> FindAddressCandidates(string query, string serviceUrl)
    {
        var httpUrl = new Uri(serviceUrl + $"/findAddressCandidates/?SingleLine={query}&f=json&outSR={{\"wkid\":4326,\"wkt\":null,\"latestWkid\":4326}}&outFields=*&maxLocations=5");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, httpUrl);
        using var httpClient = new HttpClient();
        using var httpResponse = await httpClient.SendAsync(httpRequest);
        return JsonConvert.DeserializeObject<AddressCandidatesResponse>(await httpResponse.Content.ReadAsStringAsync()) ?? new();
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class AddressCandidatesResponse
{
    [JsonProperty("candidates")]
    public List<AddressCandidate> Candidates { get; set; } = new();

    public class AddressCandidate
    {
        [JsonProperty("address"), JsonRequired]
        public string Address { get; set; } = "";

        [JsonProperty("location")]
        public AddressCandidateLocation Location { get; set; } = new();

        [JsonProperty("extent")]
        public AddressCandidateExtent Extent { get; set; } = new();
    }
    public class AddressCandidateLocation
    {
        [JsonProperty("x"), JsonRequired]
        public double X { get; set; }

        [JsonProperty("y"), JsonRequired]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double? Z { get; set; }
    }
    public class AddressCandidateExtent
    {
        [JsonProperty("xmin"), JsonRequired]
        public double XMin { get; set; }

        [JsonProperty("xmax"), JsonRequired]
        public double XMax { get; set; }

        [JsonProperty("ymin"), JsonRequired]
        public double YMin { get; set; }

        [JsonProperty("ymax"), JsonRequired]
        public double YMax { get; set; }
    }
}