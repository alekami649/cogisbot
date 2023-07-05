using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text;

namespace CoGISBot.Telegram;

[JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
public class GlobalSettings
{
    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = "CoGIS";

    [JsonProperty("url"), JsonRequired]
    public string Url { get; set; } = "https://cogisdemo.dataeast.com/portal";

    [JsonProperty("catalogUrl"), JsonRequired]
    public string CatalogUrl { get; set; } = "https://cogisdemo.dataeast.com/portal/Catalog/GetCatalogNodes";

    [JsonProperty("admins")]
    public List<long> Admins { get; set; } = new List<long>();

    [JsonProperty("cadastreUrl")]
    public string CadastreUrl { get; set; } = "https://cogisdemo.dataeast.com/elitegis/rest/services/solutions_cadastre/cadastre/MapServer";

    [JsonProperty("geocoderUrl")]
    public string GeocoderUrl { get; set; } = "https://cogisdemo.dataeast.com/elitegis/rest/services/common_osmde/ru_geocoder/GeocodeServer";

    [JsonProperty("addressSearch")]
    public bool EnableAddressSearch { get; set; } = true;

    [JsonProperty("cadastreSearch")]
    public bool EnableCadastreSearch { get; set; } = true;

    [JsonProperty("mapsSearch")]
    public bool EnableMapsSearch { get; set; } = true;

    public static GlobalSettings Instance { get; set; } = LoadOrCreate();

    public static GlobalSettings LoadOrCreate(string filePath = "globalSettings.json")
    {
        if (!File.Exists(filePath))
        {
            var instance = new GlobalSettings();
            File.WriteAllText(filePath, JsonConvert.SerializeObject(instance), Encoding.UTF8);
        }
        var raw = File.ReadAllText(filePath, Encoding.UTF8);
        return JsonConvert.DeserializeObject<GlobalSettings>(raw) ?? new();
    }

    public void Save(string filePath = "globalSettings.json")
    {
        var raw = JsonConvert.SerializeObject(this);
        File.WriteAllText(filePath, raw, Encoding.UTF8);
    }
}

public enum UserState
{
    Default = 0,
    EditName = 1,
    EditUrl = 2,
    EditGeocoder = 3,
    EditCadastre = 4
}