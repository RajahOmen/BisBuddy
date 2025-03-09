using BisBuddy.Gear;
using BisBuddy.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BisBuddy.Import
{
    public class EtroSource : ImportSource
    {
        public ImportSourceType SourceType => ImportSourceType.Etro;

        private static readonly string UriHost = "etro.gg";
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

        private readonly ItemData itemData;
        private readonly HttpClient httpClient;

        public EtroSource(ItemData itemData, HttpClient httpClient)
        {
            this.itemData = itemData;
            this.httpClient = httpClient;
        }

        public async Task<List<Gearset>> ImportGearsets(string importString)
        {
            var apiUrl = safeUrl(importString)
                ?? throw new GearsetImportException(GearsetImportStatusType.InvalidInput, message: "Invalid URL");

            try
            {
                var gearsets = new List<Gearset>();
                var response = await httpClient.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                var jsonString = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(jsonString))
                    throw new GearsetImportException(GearsetImportStatusType.InvalidResponse);

                using var jsonDoc = JsonDocument.Parse(jsonString);
                var rootJsonElement = jsonDoc.RootElement;

                var gearset = await parseGearset(rootJsonElement, importString);
                if (gearset != null)
                    gearsets.Add(gearset);

                return gearsets;
            }
            catch (HttpRequestException ex)
            {
                throw new GearsetImportException(GearsetImportStatusType.InvalidInput, ex.Message);
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

            var etroSetUuid = uri.AbsolutePath.Split("/").LastOrDefault();
            if (etroSetUuid == string.Empty)
                return null;

            return EtroApiBase + etroSetUuid;
        }

        private async Task<Gearset?> parseGearset(JsonElement rootJsonElement, string importString)
        {
            // set gearset name
            var gearsetName = Configuration.DefaultGearsetName;
            if (
                rootJsonElement.TryGetProperty("name", out var nameProp)
                && nameProp.ValueKind == JsonValueKind.String
                )
            {
                gearsetName = nameProp.GetString() ?? Configuration.DefaultGearsetName;
            }

            var job = "???";
            if (
                rootJsonElement.TryGetProperty("jobAbbrev", out var jobProp)
                && jobProp.ValueKind == JsonValueKind.String
                )
            {
                job = jobProp.GetString() ?? "???";
            }

            JsonElement? materiaProp = null;
            if (rootJsonElement.TryGetProperty("materia", out var materia))
            {
                materiaProp = materia;
            }

            var gearpieces = new List<Gearpiece>();
            var gearset = new Gearset(gearsetName, gearpieces, job, importString, ImportSourceType.Etro);

            foreach (var typeStr in EtroGearpieceTypeFieldNames)
            {
                if (
                    rootJsonElement.TryGetProperty(typeStr, out var gearpieceIdJson)
                    && gearpieceIdJson.ValueKind == JsonValueKind.Number
                    )
                {
                    // parse gearpiece properties
                    var gearpieceType = GearpieceTypeMapper.Parse(typeStr);
                    var gearpieceId = gearpieceIdJson.GetUInt32();
                    var hqGearpieceId = itemData.ConvertItemIdToHq(gearpieceId);
                    // etro only provides NQ items, convert to HQ
                    var gearpiece = new Gearpiece(
                        hqGearpieceId,
                        itemData.GetItemNameById(hqGearpieceId),
                        gearpieceType,
                        itemData.BuildGearpiecePrerequisiteTree(hqGearpieceId),
                        parseMateria(materiaProp, gearpieceId.ToString(), typeStr)
                        );

                    // add gearpiece to gearpieces
                    gearpieces.Add(gearpiece);
                }
            }

            if (rootJsonElement.TryGetProperty("relics", out var relics)
                && relics.ValueKind == JsonValueKind.Object)
            {
                foreach (var relic in relics.EnumerateObject())
                {
                    if (relic.Value.ValueKind != JsonValueKind.String)
                        continue;

                    var etroRelicUuid = relic.Value.GetString();
                    if (etroRelicUuid == null)
                        continue;

                    // get the item id from etro
                    var relicItemId = await getItemIdFromRelicUuid(etroRelicUuid);
                    if (relicItemId == 0)
                        continue;

                    var relicName = itemData.GetItemNameById(relicItemId);
                    var relicType = GearpieceTypeMapper.Parse(relic.Name);
                    var relicMateria = new List<Materia>();
                    var gearpiece = new Gearpiece(
                        relicItemId,
                        relicName,
                        relicType,
                        itemData.BuildGearpiecePrerequisiteTree(relicItemId),
                        parseMateria(materiaProp, relicItemId.ToString(), relic.Name)
                        );

                    // add gearpiece to gearpieces
                    gearpieces.Add(gearpiece);
                }
            }

            if (gearset.Gearpieces.Count == 0)
                return null;

            gearset.Gearpieces.Sort((a, b) => a.GearpieceType.CompareTo(b.GearpieceType));

            return gearset;
        }

        private List<Materia> parseMateria(
            JsonElement? materiaElement,
            string gearpieceIdStr,
            string gearpieceTypeStr
            )
        {
            var materiaList = new List<Materia>();
            if (materiaElement == null)
                return materiaList;
            var materiaProp = materiaElement.Value;

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

        private async Task<uint> getItemIdFromRelicUuid(string relicUuid)
        {
            // fetch the relic data from etro
            var response = httpClient.GetAsync(EtroRelicApiBase + relicUuid).Result;
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
    }
}
