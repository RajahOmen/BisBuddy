using BisBuddy.Gear;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace BisBuddy.Import
{
    public class XivgearSource : ImportSource
    {
        public ImportSourceType SourceType => throw new NotImplementedException();

        private static readonly string XivgearApiBase = "https://api.xivgear.app/shortlink/";
        private static readonly string XivgearSetIndexBase = "&onlySetIndex=";

        public Task<List<Gearset>> ImportGearsets(string importString)
        {
            throw new NotImplementedException();
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


        public static async Task<List<Gearset>> ImportFromRemote(string url, ItemData itemData)
        {
            var apiUrl = string.Empty;
            GearsetSourceType? urlType = null;
            try
            {
                (apiUrl, urlType) = SafeUrl(url);
                if (apiUrl == null || urlType == null)
                {
                    throw new GearsetImportException(GearsetImportStatusType.InternalError);
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


        protected static async Task<List<Gearset>> ImportFromXivgear(string apiUrl, string sourceUrl, ItemData itemData)
        {
            try
            {
                var onlyImportSetIdx = sourceUrl.Contains(XivgearSetIndexBase)
                    ? int.Parse(sourceUrl.Split(XivgearSetIndexBase)[1])
                    : -1;

                var gearsets = new List<Gearset>();
                using var client = new HttpClient();
                var response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(jsonString))
                    throw new GearsetImportException(GearsetImportStatusType.InternalError);

                using var jsonDoc = JsonDocument.Parse(jsonString);
                var json = jsonDoc.RootElement;

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
                throw new GearsetImportException(GearsetImportStatusType.InternalError);
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is InvalidOperationException)
            {
                Services.Log.Error(ex, $"Gearset Import Error for URL: {apiUrl}");
                throw new GearsetImportException(GearsetImportStatusType.InternalError);
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

                // xivgear only provides NQ items, convert to HQ
                var gearpieceId = itemData.ConvertItemIdToHq(id.GetUInt32());
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
                    gearpieceName,
                    gearpieceType,
                    itemData.BuildGearpiecePrerequisiteTree(gearpieceId),
                    materiaList
                    );

                // add gearpiece to gearset
                gearpieces.Add(gearpiece);
            }

            gearset.Gearpieces.Sort((a, b) => a.GearpieceType.CompareTo(b.GearpieceType));

            return gearset;
        }
    }
}
