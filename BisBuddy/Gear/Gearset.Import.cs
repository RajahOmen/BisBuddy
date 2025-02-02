using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace BisBuddy.Gear
{
    public partial class Gearset
    {
        private static readonly string XivgearApiBase = "https://api.xivgear.app/shortlink/";
        private static readonly string XivgearSetIndexBase = "&onlySetIndex=";
        private static readonly string EtroApiBase = "https://etro.gg/api/gearsets/";
        private static readonly string EtroRelicApiBase = "https://etro.gg/api/relic/";
        private static readonly List<string> EtroGearpieceTypeFieldNames = new([
            "weapon",
            "head",
            "body",
            "hands",
            "legs",
            "feet",
            "offHand",
            "ears",
            "neck",
            "wrists",
            "fingerL",
            "fingerR",
            ]);

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
        };

        public static async Task<List<Gearset>> ImportFromRemote(string url, ItemData itemData)
        {
            var apiUrl = string.Empty;
            GearsetSourceType? urlType = null;
            try
            {
                (apiUrl, urlType) = SafeUrl(url);
                if (apiUrl == null || urlType == null)
                {
                    throw new GearsetImportException(GearsetImportStatusType.InvalidUrl);
                }

                return urlType switch
                {
                    GearsetSourceType.Xivgear => await ImportFromXivgear(apiUrl, url, itemData),
                    GearsetSourceType.Etro => await ImportFromEtro(apiUrl, url, itemData),
                    _ => throw new GearsetImportException(GearsetImportStatusType.InternalError),
                };
            }
            catch (GearsetImportException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Gearset Import Internal Error for URL: {apiUrl} [{urlType}]");
                throw new GearsetImportException(GearsetImportStatusType.InternalError);
            }
        }

        public static Gearset ImportFromJson(string jsonStr)
        {
            try
            {
                var gearset = JsonSerializer.Deserialize<Gearset>(jsonStr, JsonOptions)
                    ?? throw new GearsetImportException(GearsetImportStatusType.InvalidJson);

                gearset.Id = Guid.NewGuid().ToString(); // set to a new random uuid\
                Services.Log.Debug($"Imported 1 gearset from {jsonStr}");
                return gearset;
            }
            catch (ArgumentNullException)
            {
                Services.Log.Error($"Gearset Import Null JSON: {jsonStr}");
                throw new GearsetImportException(GearsetImportStatusType.NoJson);
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
            {
                Services.Log.Error(ex, $"Gearset Import Invalid JSON: {jsonStr}");
                throw new GearsetImportException(GearsetImportStatusType.InvalidJson);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Gearset Import Internal Error for JSON: {jsonStr}");
                throw new GearsetImportException(GearsetImportStatusType.InternalError);
            }
        }

        private static (string? apiUrl, GearsetSourceType? type) SafeUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                switch (uri.Host)
                {
                    case "etro.gg":
                        var etroSetUuid = uri.AbsolutePath.Split("/").LastOrDefault();
                        if (etroSetUuid == string.Empty) return (null, null);
                        return (EtroApiBase + etroSetUuid, GearsetSourceType.Etro);
                    case "xivgear.app":
                        // get relevant part of query string
                        var page = HttpUtility.ParseQueryString(uri.Query).Get("page");
                        if (string.IsNullOrEmpty(page) || !page.Contains('|')) return (null, null);

                        // Split the string and return the part after '|' appended to base
                        var xivgearSetUuid = page.Split('|')[1].Split('&')[0];
                        return (XivgearApiBase + xivgearSetUuid, GearsetSourceType.Xivgear);
                    default: return (null, null);
                }
            }
            return (null, null);
        }

        protected static async Task<List<Gearset>> ImportFromXivgear(string apiUrl, string sourceUrl, ItemData itemData)
        {
            try
            {
                Services.Log.Verbose(sourceUrl);
                var onlyImportSetIdx = sourceUrl.Contains(XivgearSetIndexBase)
                    ? int.Parse(sourceUrl.Split(XivgearSetIndexBase)[1])
                    : -1;
                Services.Log.Verbose(onlyImportSetIdx.ToString());

                var gearsets = new List<Gearset>();
                using var client = new HttpClient();
                var response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(jsonString))
                    throw new GearsetImportException(GearsetImportStatusType.NoWebResponse);

                using var jsonDoc = JsonDocument.Parse(jsonString);
                var json = jsonDoc.RootElement;

                Services.Log.Verbose(jsonString);

                // Ignore links that are for pages with multiple sets
                if (json.TryGetProperty("sets", out var sets))
                {
                    var setIdx = -1;
                    foreach (var set in sets.EnumerateArray())
                    {
                        try
                        {
                            setIdx++;
                            // importing a specific set, ignore the rest
                            if (onlyImportSetIdx >= 0 && setIdx != onlyImportSetIdx) continue;
                            Services.Log.Verbose($"{setIdx} == {onlyImportSetIdx} ({setIdx == onlyImportSetIdx}) || onlyImportSetIdx <= 0 ({onlyImportSetIdx > 0})");

                            string? job = null;
                            if (
                                json.TryGetProperty("job", out var jobProp)
                                && jobProp.ValueKind == JsonValueKind.String
                                )
                            {
                                job = jobProp.GetString();
                            }
                            var setSourceUrl =
                                sourceUrl.Contains(XivgearSetIndexBase)
                                ? sourceUrl
                                : sourceUrl + XivgearSetIndexBase + setIdx;
                            gearsets.Add(GearsetFromXivgear(setSourceUrl, set, itemData, job));
                        }
                        catch (Exception e)
                        {
                            Services.Log.Warning($"Failed to import gearset of gearsets: " + e.Message);
                        }
                    }
                }
                else if (json.TryGetProperty("items", out var items))
                {
                    gearsets.Add(GearsetFromXivgear(sourceUrl, json, itemData, null));
                }
                else
                {
                    throw new GearsetImportException(GearsetImportStatusType.NoGearsets);
                }

                Services.Log.Debug($"Imported {gearsets.Count} gearset(s) from {apiUrl}");
                return gearsets;
            }
            catch (HttpRequestException ex)
            {
                Services.Log.Error(ex, $"Gearset Import Http Status Error [{ex.StatusCode}]: {apiUrl}");
                throw new GearsetImportException(GearsetImportStatusType.InvalidWebResponse);
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is InvalidOperationException)
            {
                Services.Log.Error(ex, $"Gearset Import Error for URL: {apiUrl}");
                throw new GearsetImportException(GearsetImportStatusType.InvalidWebResponse);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Gearset Import Internal Error for URL: {apiUrl}");
                throw new GearsetImportException(GearsetImportStatusType.InternalError);
            }
        }

        protected static Gearset GearsetFromXivgear(string sourceUrl, JsonElement setJson, ItemData itemData, string? gearsetNameOverride)
        {
            // set gearset name
            var gearsetName = DefaultName;
            if (
                setJson.TryGetProperty("name", out var nameProp)
                && nameProp.ValueKind == JsonValueKind.String
                )
            {
                gearsetName = nameProp.GetString() ?? DefaultName;
            }

            var gearsetJob = gearsetNameOverride ?? "???";
            if (
                gearsetNameOverride == null
                && setJson.TryGetProperty("job", out var jobProp)
                && jobProp.ValueKind == JsonValueKind.String
                )
            {
                gearsetJob = jobProp.GetString() ?? "???";
            }

            // get items from json
            if (!setJson.TryGetProperty("items", out var slots))
                throw new JsonException($"No items found in {gearsetName}");

            var gearpieces = new List<Gearpiece>();
            var gearset = new Gearset(gearsetName, gearpieces, gearsetJob, sourceUrl, GearsetSourceType.Xivgear);

            foreach (var slot in slots.EnumerateObject())
            {
                if (
                    !slot.Value.TryGetProperty("id", out var id)
                    || id.ValueKind != JsonValueKind.Number
                    )
                {
                    throw new JsonException("No item ID for slot " + slot.Name);
                }

                var gearpieceType = GearpieceTypeMapper.Parse(slot.Name);

                var gearpieceId = itemData.ConvertItemIdToHq(id.GetUInt32());
                // xivgear only provides NQ items, convert to HQ
                gearpieceId = itemData.ConvertItemIdToHq(gearpieceId);
                var gearpieceName = itemData.GetItemNameById(gearpieceId);
                List<Materia> materiaList = [];

                if (slot.Value.TryGetProperty("materia", out var materiaArray))
                {
                    foreach (var materiaSlot in materiaArray.EnumerateArray())
                    {
                        if (
                            materiaSlot.TryGetProperty("id", out var materiaId)
                            && materiaId.ValueKind == JsonValueKind.Number
                            && materiaId.GetInt32() > 0
                            )
                        {
                            var newMateria = itemData.BuildMateria(materiaId.GetUInt32());
                            materiaList.Add(newMateria);
                        }
                    }

                }

                var gearpiece = new Gearpiece(
                    gearpieceId,
                    itemData.GetItemNameById(gearpieceId),
                    gearpieceType,
                    itemData.BuildGearpiecePrerequesites(gearpieceId),
                    materiaList
                    );

                // add gearpiece to gearset
                gearpieces.Add(gearpiece);
            }

            gearset.Gearpieces.Sort((a, b) => a.GearpieceType.CompareTo(b.GearpieceType));

            return gearset;
        }

        protected static async Task<List<Gearset>> ImportFromEtro(string apiUrl, string sourceUrl, ItemData itemData)
        {
            try
            {
                var gearsets = new List<Gearset>();
                using var client = new HttpClient();
                var response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(jsonString))
                {
                    throw new Exception("Empty response");
                }

                using var jsonDoc = JsonDocument.Parse(jsonString);
                var json = jsonDoc.RootElement;

                // set gearset name
                var gearsetName = DefaultName;
                if (
                    json.TryGetProperty("name", out var nameProp)
                    && nameProp.ValueKind == JsonValueKind.String
                    )
                {
                    gearsetName = nameProp.GetString() ?? DefaultName;
                }

                var job = "???";
                if (
                    json.TryGetProperty("jobAbbrev", out var jobProp)
                    && jobProp.ValueKind == JsonValueKind.String
                    )
                {
                    job = jobProp.GetString() ?? "???";
                }

                JsonElement? materiaProp = null;
                if (json.TryGetProperty("materia", out var materia))
                {
                    materiaProp = materia;
                }

                var gearpieces = new List<Gearpiece>();
                var gearset = new Gearset(gearsetName, gearpieces, job, sourceUrl, GearsetSourceType.Etro);

                foreach (var typeStr in EtroGearpieceTypeFieldNames)
                {
                    if (
                        json.TryGetProperty(typeStr, out var gearpieceIdJson)
                        && gearpieceIdJson.ValueKind == JsonValueKind.Number
                        )
                    {
                        // parse gearpiece properties
                        var gearpieceType = GearpieceTypeMapper.Parse(typeStr);
                        var gearpieceId = itemData.ConvertItemIdToHq(gearpieceIdJson.GetUInt32());
                        // etro only provides NQ items, convert to HQ
                        var gearpiece = new Gearpiece(
                            gearpieceId,
                            itemData.GetItemNameById(gearpieceId),
                            gearpieceType,
                            itemData.BuildGearpiecePrerequesites(gearpieceId),
                            getEtroMateria(materiaProp, gearpieceId.ToString(), typeStr, itemData)
                            );

                        // add gearpiece to gearpieces
                        gearpieces.Add(gearpiece);
                    }
                }

                if (json.TryGetProperty("relics", out var relics)
                    && relics.ValueKind == JsonValueKind.Object)
                {
                    foreach (var relic in relics.EnumerateObject())
                    {
                        if (relic.Value.ValueKind != JsonValueKind.String) continue;
                        var etroRelicUuid = relic.Value.GetString();
                        if (etroRelicUuid == null) continue;

                        // get the item id from etro
                        var relicItemId = await GetItemIdFromEtroRelicUuid(etroRelicUuid, client);
                        if (relicItemId == 0) continue;

                        var relicName = itemData.GetItemNameById(relicItemId);
                        var relicType = GearpieceTypeMapper.Parse(relic.Name);
                        var relicMateria = new List<Materia>();
                        var gearpiece = new Gearpiece(
                            relicItemId,
                            relicName,
                            relicType,
                            itemData.BuildGearpiecePrerequesites(relicItemId),
                            getEtroMateria(materiaProp, relicItemId.ToString(), relic.Name, itemData)
                            );

                        // add gearpiece to gearpieces
                        gearpieces.Add(gearpiece);
                    }
                }

                gearset.Gearpieces.Sort((a, b) => a.GearpieceType.CompareTo(b.GearpieceType));

                gearsets.Add(gearset);

                Services.Log.Debug($"Imported {gearsets.Count} gearset(s) from {apiUrl}");
                return gearsets;
            }
            catch (HttpRequestException ex)
            {
                Services.Log.Error(ex, $"Gearset Import Http Status Error [{ex.StatusCode}]: {apiUrl}");
                throw new GearsetImportException(GearsetImportStatusType.InvalidWebResponse);
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is InvalidOperationException)
            {
                Services.Log.Error(ex, $"Gearset Import Error for URL: {apiUrl}");
                throw new GearsetImportException(GearsetImportStatusType.InvalidWebResponse);
            }
            catch (Exception ex)
            {
                Services.Log.Error(ex, $"Gearset Import Internal Error for URL: {apiUrl}");
                throw new GearsetImportException(GearsetImportStatusType.InternalError);
            }
        }

        protected static List<Materia> getEtroMateria(
            JsonElement? materia,
            string gearpieceIdStr,
            string gearpieceTypeStr,
            ItemData itemData
            )
        {
            var materiaList = new List<Materia>();
            if (materia == null) return materiaList;
            var materiaProp = materia.Value;

            foreach (var prop in materiaProp.EnumerateObject())
            {
                var propMatchString = prop.Name;
                // materia for a ring. only match with gearpieces of appropriate finger type
                if (prop.Name.EndsWith('R') || prop.Name.EndsWith('L'))
                {
                    if (prop.Name.EndsWith('R') && !gearpieceTypeStr.EndsWith('R')) continue;
                    if (prop.Name.EndsWith('L') && !gearpieceTypeStr.EndsWith('L')) continue;

                    // don't match the R/L at the end
                    propMatchString = prop.Name[..^1];
                }

                // not the materia for this gearpiece
                if (propMatchString != gearpieceIdStr) continue;

                // materia for this gearpiece, add to list
                foreach (var materiaSlot in prop.Value.EnumerateObject())
                {
                    var materiaSlotId = materiaSlot.Value.GetUInt32();
                    var newMateria = itemData.BuildMateria(materiaSlotId);
                    materiaList.Add(newMateria);
                }
            }

            return materiaList;
        }

        protected static async Task<uint> GetItemIdFromEtroRelicUuid(string relicUuid, HttpClient client)
        {
            // fetch the relic data from etro
            var response = client.GetAsync(EtroRelicApiBase + relicUuid).Result;
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var json = jsonDoc.RootElement;

            // get response["baseItem"]
            if (json.TryGetProperty("baseItem", out var baseItem))
            {
                // get baseItem["id"]
                if (baseItem.TryGetProperty("id", out var id))
                {
                    return id.GetUInt32();
                }
            }

            return 0;
        }

        public string ExportToJsonStr()
        {
            return JsonSerializer.Serialize(this, JsonOptions);
        }
    }
}
