using BisBuddy.Factories;
using BisBuddy.Gear;
using BisBuddy.Import;
using BisBuddy.Items;
using BisBuddy.Util;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BisBuddy.Services.ImportGearset
{
    public class TeamcraftPlaintextSource(
        IItemDataService itemDataService,
        IMateriaFactory materiaFactory,
        IGearpieceFactory gearpieceFactory,
        IGearsetFactory gearsetFactory
        ) : IImportGearsetSource
    {
        public ImportGearsetSourceType SourceType => ImportGearsetSourceType.Teamcraft;
        private static readonly string GearpieceStartingStr = "**";
        private static readonly string MateriaStartingStr = "- ";
        private static readonly string HqIndicatorStr = "HQ";

        private readonly IItemDataService itemDataService = itemDataService;
        private readonly IMateriaFactory materiaFactory = materiaFactory;
        private readonly IGearpieceFactory gearpieceFactory = gearpieceFactory;
        private readonly IGearsetFactory gearsetFactory = gearsetFactory;

        public async Task<List<Gearset>> ImportGearsets(string importString)
        {
            try
            {
                var gearset = await Task.Run(() => parseGearset(importString))
                    ?? throw new GearsetImportException(GearsetImportStatusType.InvalidInput, "No gearset could be created");

                return [gearset];
            }
            catch (Exception ex) when (ex is ArgumentException)
            {
                throw new GearsetImportException(GearsetImportStatusType.InvalidInput, ex.Message);
            }
        }

        private Gearset? parseGearset(string importString)
        {
            using var reader = new StringReader(importString)
                ?? throw new ArgumentException("Could not instantiate string reader");

            // data for gearpiece currently being built
            var gearpieceName = string.Empty;
            List<string> materiaNames = [];

            // data for past built gearpieces
            HashSet<string> possibleJobAbbrevs = [];
            List<Gearpiece> gearpieces = [];

            while (reader.ReadLine() is { } line)
            {
                if (line.IsNullOrEmpty() || line.IsNullOrWhitespace())
                    continue;

                // identifier for what kind of line this is
                var startingStr = line[..2];

                // the item this line has on it
                var lineItem = line[2..].Replace("*", "");

                if (startingStr == GearpieceStartingStr)
                {
                    // new gearpiece started, build old onex    
                    var buildResult = buildGearpiece(gearpieceName, materiaNames, possibleJobAbbrevs);
                    if (buildResult.Gearpiece != null)
                    {
                        gearpieces.Add(buildResult.Gearpiece);
                        possibleJobAbbrevs = buildResult.newJobAbbrevs;
                    }

                    // start gathering data for new gearpiece
                    gearpieceName = lineItem;
                    materiaNames.Clear();
                }
                else if (startingStr == MateriaStartingStr)
                {
                    // this item is a materia, add here
                    materiaNames.Add(lineItem);
                }
                else
                {
                    throw new ArgumentException($"Unknown line type: {line}");
                }
            }

            var finalBuildResult = buildGearpiece(gearpieceName, materiaNames, possibleJobAbbrevs);
            if (finalBuildResult.Gearpiece != null)
            {
                gearpieces.Add(finalBuildResult.Gearpiece);
                possibleJobAbbrevs = finalBuildResult.newJobAbbrevs;
            }

            if (gearpieces.Count == 0)
                return null;

            // did gearpieces narrow possibilities down to 1? Pick it, else unknown
            var actualJobAbbrev = possibleJobAbbrevs.Count == 1 ? possibleJobAbbrevs.First() : "???";
            var classJobId = itemDataService.GetClassJobInfoByEnAbbreviation(actualJobAbbrev).ClassJobId;

            return gearsetFactory.Create(
                gearpieces: gearpieces,
                classJobId: classJobId,
                sourceType: ImportGearsetSourceType.Teamcraft,
                sourceString: importString
                );
        }

        private (Gearpiece? Gearpiece, HashSet<string> newJobAbbrevs) buildGearpiece(
            string gearpieceName,
            List<string> materiaNames,
            HashSet<string> possibleJobAbbrevs
            )
        {
            // not a real gearpiece
            if (gearpieceName == string.Empty)
                return (null, possibleJobAbbrevs);

            // handle HQ parsing
            gearpieceName = gearpieceName.Replace(HqIndicatorStr, Constants.HqIcon.ToString());
            var itemId = itemDataService.GetItemIdByName(gearpieceName);

            // invalid item name
            if (itemId == 0)
                return (null, possibleJobAbbrevs);

            // update possible job abbrevs
            var currentItemJobAbbrevs = itemDataService.GetItemClassJobCategories(itemId);

            if (possibleJobAbbrevs.Count == 0)
                possibleJobAbbrevs = currentItemJobAbbrevs;
            else
                possibleJobAbbrevs = possibleJobAbbrevs
                    .Intersect(currentItemJobAbbrevs)
                    .ToHashSet();

            var itemMateria = materiaNames
                .Select(itemDataService.GetItemIdByName)
                .Where(id => id > 0)
                .Select(id => materiaFactory.Create(id))
                .ToList();


            var gearpiece = gearpieceFactory.Create(
                itemId: itemId,
                itemMateria: itemMateria
                );

            return (gearpiece, possibleJobAbbrevs);
        }
    }
}
