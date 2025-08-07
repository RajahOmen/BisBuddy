using BisBuddy.Gear;
using BisBuddy.Import;
using BisBuddy.Items;
using BisBuddy.Resources;
using BisBuddy.Util;
using System;
using System.Collections.Generic;

namespace BisBuddy.Factories
{
    public class GearsetFactory(
        IItemDataService itemDataService
        ) : IGearsetFactory
    {
        private readonly IItemDataService itemDataService = itemDataService;

        public Gearset Create(
            IReadOnlyList<Gearpiece> gearpieces,
            string? id = null,
            string? name = null,
            ImportGearsetSourceType? sourceType = null,
            uint classJobId = 0,
            bool isActive = true,
            string? sourceUrl = null,
            string? sourceString = null,
            int? priority = null,
            DateTime? importDate = null,
            HighlightColor? highlightColor = null
            ) => new(
                id: id ?? Guid.NewGuid().ToString(),
                isActive: isActive,
                name: name ?? Resource.DefaultNewGearsetName,
                gearpieces: gearpieces,
                classJobInfo: itemDataService.GetClassJobInfoById(classJobId),
                sourceType: sourceType,
                sourceUrl: sourceUrl,
                sourceString: sourceString,
                priority: priority ?? Constants.DefaultGearsetPriority,
                importDate: importDate ?? DateTime.UtcNow,
                highlightColor: highlightColor
                );
    }

    public interface IGearsetFactory
    {
        /// <summary>
        /// Builds a gearset from the provided parameters
        /// </summary>
        /// <param name="gearpieces">Gearset gearpieces</param>
        /// <param name="id">Gearset id. Defaults to <see cref="Guid.NewGuid"/></param>
        /// <param name="name">Gearset name. Defaults to <see cref="Resource.DefaultNewGearsetName"/></param>
        /// <param name="sourceType">How was this gearset imported</param>
        /// <param name="classJobId">The ClassJob id for this gearset. Defaults to unknown</param>
        /// <param name="isActive">If this gearset currently enabled</param>
        /// <param name="sourceUrl">If imported from a url, populate the url here</param>
        /// <param name="sourceString">If imported from plaintext, populate string here</param>
        /// <param name="priority">The priority value of the gearset for the assignment system</param>
        /// <param name="importDate">When was this gearset imported. Defaults to <see cref="DateTime.UtcNow"/></param>
        /// <param name="highlightColor">If the gearset has a custom <see cref="HighlightColor"/> set. Defaults
        /// to, instead using <see cref="Configuration.DefaultHighlightColor"/></param>
        /// <returns>The initialized <see cref="Gearset"/></returns>
        public Gearset Create(
            IReadOnlyList<Gearpiece> gearpieces,
            string? id = null,
            string? name = null,
            ImportGearsetSourceType? sourceType = null,
            uint classJobId = 0,
            bool isActive = true,
            string? sourceUrl = null,
            string? sourceString = null,
            int? priority = null,
            DateTime? importDate = null,
            HighlightColor? highlightColor = null
            );
    }
}
