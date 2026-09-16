using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using BisBuddy.Import;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.IO;
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
        IMateriaFactory materiaFactory,
        IGearpieceFactory gearpieceFactory,
        IGearsetFactory gearsetFactory
        ) : IImportGearsetSource
    {
        public ImportGearsetSourceType SourceType => ImportGearsetSourceType.Xivgear;

        private const string UriHost = "xivgear.app";
        private const string XivgearApiPathBase = "https://api.xivgear.app/basedata/";
        private const string XivgearSetIndexBase = "&onlySetIndex=";

        private readonly ITypedLogger<XivgearSource> logger = logger;
        private readonly HttpClient httpClient = httpClient;
        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IMateriaFactory materiaFactory = materiaFactory;
        private readonly IGearpieceFactory gearpieceFactory = gearpieceFactory;
        private readonly IGearsetFactory gearsetFactory = gearsetFactory;

        public async Task<List<Gearset>> ImportGearsets(string importString)
        {
            Uri importUri;
            try
            {
                if (!importString.Contains(UriHost))
                    throw new ArgumentException("Not a XIVGear URL", nameof(importString));
                var query = HttpUtility.ParseQueryString(string.Empty);
                query["url"] = importString;

                importUri = new UriBuilder(XivgearApiPathBase)
                {
                    Query = query.ToString()
                }.Uri;
            }
            catch (Exception ex)
            {
                throw new GearsetImportException(GearsetImportStatusType.InvalidInput, message: $"Invalid URL: {ex.Message}");
            }

            try
            {
                logger.Verbose($"Requesting set info from '{importUri}'");
                var response = await httpClient.GetAsync(importUri);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(jsonString))
                    throw new GearsetImportException(GearsetImportStatusType.InternalError);

                using var jsonDoc = JsonDocument.Parse(jsonString);
                var jsonRootElement = jsonDoc.RootElement;
                var gearsets = new List<Gearset>();

                // page has multiple gearsets on it, handle appropriately
                if (jsonRootElement.TryGetProperty("sets", out var setsElement))
                {
                    gearsets = parseMultipleGearsets(setsElement, jsonRootElement, importString);
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
                throw new GearsetImportException(GearsetImportStatusType.InvalidInput, $"importString: {importUri}, importUri: {importUri}, {ex.Message}");
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is InvalidOperationException)
            {
                throw new GearsetImportException(GearsetImportStatusType.InvalidResponse, ex.Message);
            }
        }

        private List<Gearset> parseMultipleGearsets(
            JsonElement setsElement,
            JsonElement rootElement,
            string importString
            )
        {
            var gearsets = new List<Gearset>();
            var setIdx = -1;
            foreach (var setElement in setsElement.EnumerateArray())
            {
                try
                {
                    setIdx++;

                    uint? classJobId = null;
                    if (
                        rootElement.TryGetProperty("job", out var jobProp)
                        && jobProp.ValueKind == JsonValueKind.String
                        )
                    {
                        var classJobInfo = itemDataService.GetClassJobInfoByEnAbbreviation(jobProp.GetString() ?? "");
                        if (classJobInfo.ClassJobId != 0)
                            classJobId = classJobInfo.ClassJobId;
                    }
                    var setSourceUrl =
                        importString.Contains(XivgearSetIndexBase)
                        ? importString
                        : importString + XivgearSetIndexBase + setIdx;

                    var gearset = parseGearset(setSourceUrl, setElement, classJobId);
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

        private Gearset? parseGearset(string sourceUrl, JsonElement setElement, uint? gearsetJobOverride)
        {
            // set gearset name
            string? gearsetName = null;
            if (
                setElement.TryGetProperty("name", out var nameProp)
                && nameProp.ValueKind == JsonValueKind.String
                )
            {
                gearsetName = nameProp.GetString();
            }

            var classJobId = gearsetJobOverride ?? 0;
            if (
                (setElement.TryGetProperty("jobOverride", out var jobProp) ||
                setElement.TryGetProperty("job", out jobProp))
                && jobProp.ValueKind == JsonValueKind.String
                )
            {
                logger.Debug($"Using job {jobProp.GetString()} from gearset");
                classJobId = itemDataService.GetClassJobInfoByEnAbbreviation(jobProp.GetString() ?? "").ClassJobId;
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

                List<Materia> gearpieceMateria = [];

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
                            var newMateria = materiaFactory.Create(materiaId.GetUInt32());
                            gearpieceMateria.Add(newMateria);
                        }
                    }
                }

                var gearpiece = gearpieceFactory.Create(
                    itemId: gearpieceId,
                    itemMateria: gearpieceMateria
                    );

                // add gearpiece to gearset
                gearpieces.Add(gearpiece);
            }

            // don't return a gearset if it has no gearpieces
            if (gearpieces.Count == 0)
                return null;

            return gearsetFactory.Create(
                gearpieces: gearpieces,
                name: gearsetName,
                classJobId: classJobId,
                sourceType: ImportGearsetSourceType.Xivgear,
                sourceUrl: sourceUrl
                );
        }
    }
}
