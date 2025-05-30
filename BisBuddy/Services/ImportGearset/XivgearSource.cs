using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Import;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace BisBuddy.Services.ImportGearset
{
    public class XivgearSource(
        ITypedLogger<XivgearSource> logger,
        HttpClient httpClient,
        IItemDataService itemDataService,
        IGearpieceFactory gearpieceFactory
        ) : IImportGearsetSource
    {
        public ImportGearsetSourceType SourceType => ImportGearsetSourceType.Xivgear;

        private const string UriHost = "xivgear.app";
        private const string XivgearStandardApiBase = "https://api.xivgear.app/shortlink/{0}";
        private const string XivgearStaticBisApiBase = "https://staticbis.xivgear.app/{0}/{1}.json";
        private const string XivgearSetIndexBase = "&onlySetIndex=";
        private const string StaticBisIdentifier = "bis|";

        private readonly ITypedLogger<XivgearSource> logger = logger;
        private readonly HttpClient httpClient = httpClient;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IGearpieceFactory gearpieceFactory = gearpieceFactory;

        public async Task<List<Gearset>> ImportGearsets(string importString)
        {
            var apiUrl = safeUrl(importString) ??
                throw new GearsetImportException(GearsetImportStatusType.InvalidInput, message: "Invalid URL");

            try
            {
                // query can limit what gearset is displayed, so only import that one if the query term is provided
                var onlyImportSetIdx = importString.Contains(XivgearSetIndexBase)
                    ? int.Parse(importString.Split(XivgearSetIndexBase)[1])
                    : -1;

                var gearsets = new List<Gearset>();
                using var client = new HttpClient();
                var response = await httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(jsonString))
                    throw new GearsetImportException(GearsetImportStatusType.InternalError);

                using var jsonDoc = JsonDocument.Parse(jsonString);
                var jsonRootElement = jsonDoc.RootElement;

                // page has multiple gearsets on it, handle appropriately
                if (jsonRootElement.TryGetProperty("sets", out var setsElement))
                {
                    gearsets = parseMultipleGearsets(setsElement, jsonRootElement, importString, onlyImportSetIdx);
                }
                // one gearset page
                else if (jsonRootElement.TryGetProperty("items", out var items))
                {
                    var gearset = parseGearset(importString, jsonRootElement, null);
                    if (gearset != null)
                        gearsets.Add(gearset);
                }
                else
                {
                    throw new GearsetImportException(GearsetImportStatusType.NoGearsets);
                }

                return gearsets;
            }
            catch (HttpRequestException ex)
            {
                throw new GearsetImportException(GearsetImportStatusType.InvalidInput, $"apiUrl: {apiUrl}, {ex.Message}");
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is InvalidOperationException)
            {
                throw new GearsetImportException(GearsetImportStatusType.InvalidResponse, ex.Message);
            }
        }

        private static string? safeUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            if (uri.Host != UriHost)
                return null;

            // get relevant part of query string
            var page = HttpUtility.ParseQueryString(uri.Query).Get("page");
            if (string.IsNullOrEmpty(page) || !page.Contains('|'))
                return null;

            // follows pattern ...path=bis|{job abbrev}|current
            if (page.StartsWith(StaticBisIdentifier))
            {
                var pageParts = page.Split("|");
                var jobAbbrev = pageParts[1];
                var bisRelevance = pageParts[2];
                return string.Format(XivgearStaticBisApiBase, jobAbbrev, bisRelevance);
            }
            // follows pattern ...path=sl|{set uuid}
            else
            {
                // Split the string and return the part after '|' appended to base
                var xivgearSetUuid = page.Split('|')[1].Split('&')[0];
                return string.Format(XivgearStandardApiBase, xivgearSetUuid);
            }
        }

        private List<Gearset> parseMultipleGearsets(
            JsonElement setsElement,
            JsonElement rootElement,
            string importString,
            int onlyImportSetIdx
            )
        {
            var gearsets = new List<Gearset>();
            var setIdx = -1;
            foreach (var setElement in setsElement.EnumerateArray())
            {
                try
                {
                    setIdx++;
                    // importing a specific set, ignore the rest
                    if (onlyImportSetIdx >= 0 && setIdx != onlyImportSetIdx)
                        continue;

                    string? job = null;
                    if (
                        rootElement.TryGetProperty("job", out var jobProp)
                        && jobProp.ValueKind == JsonValueKind.String
                        )
                    {
                        job = jobProp.GetString();
                    }
                    var setSourceUrl =
                        importString.Contains(XivgearSetIndexBase)
                        ? importString
                        : importString + XivgearSetIndexBase + setIdx;

                    var gearset = parseGearset(setSourceUrl, setElement, job);
                    if (gearset != null)
                        gearsets.Add(gearset);
                }
                catch (Exception ex)
                {
                    logger.Warning($"Failed to import gearset of gearsets: " + ex.Message);
                }
            }

            return gearsets;
        }

        private Gearset? parseGearset(string sourceUrl, JsonElement setElement, string? gearsetJobOverride)
        {
            // set gearset name
            var gearsetName = BisBuddy.Configuration.DefaultGearsetName;
            if (
                setElement.TryGetProperty("name", out var nameProp)
                && nameProp.ValueKind == JsonValueKind.String
                )
            {
                gearsetName = nameProp.GetString() ?? BisBuddy.Configuration.DefaultGearsetName;
            }

            var gearsetJob = gearsetJobOverride ?? "???";
            if (
                gearsetJobOverride == null
                && setElement.TryGetProperty("job", out var jobProp)
                && jobProp.ValueKind == JsonValueKind.String
                )
            {
                gearsetJob = jobProp.GetString() ?? "???";
            }

            // get items from json
            if (!setElement.TryGetProperty("items", out var slots))
                throw new JsonException($"No items found in {gearsetName}");

            var gearpieces = new List<Gearpiece>();

            foreach (var slot in slots.EnumerateObject())
            {
                if (
                    !slot.Value.TryGetProperty("id", out var id)
                    || id.ValueKind != JsonValueKind.Number
                    )
                {
                    throw new JsonException("No item ID for slot " + slot.Name);
                }

                // xivgear only provides NQ items, convert to HQ
                var gearpieceId = itemDataService.ConvertItemIdToHq(id.GetUInt32());

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
                            var newMateria = itemDataService.BuildMateria(materiaId.GetUInt32());
                            materiaList.Add(newMateria);
                        }
                    }
                }

                var gearpiece = gearpieceFactory.Create(
                    gearpieceId,
                    materiaList
                    );

                // add gearpiece to gearset
                gearpieces.Add(gearpiece);
            }

            // don't return a gearset if it has no gearpieces
            if (gearpieces.Count == 0)
                return null;

            var gearset = new Gearset(
                gearsetName,
                gearpieces,
                gearsetJob,
                ImportGearsetSourceType.Xivgear,
                sourceUrl: sourceUrl
                );

            return gearset;
        }
    }
}
