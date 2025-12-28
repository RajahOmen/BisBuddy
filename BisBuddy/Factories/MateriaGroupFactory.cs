using BisBuddy.Gear.Melds;
using BisBuddy.Items;
using BisBuddy.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BisBuddy.Factories;

public class MateriaGroupFactory(
    ITypedLogger<MateriaGroupFactory> logger,
    IItemDataService itemDataService,
    IMateriaFactory materiaFactory
    ) : IMateriaGroupFactory
{
    private readonly ITypedLogger<MateriaGroupFactory> logger = logger;
    private readonly IItemDataService itemDataService = itemDataService;
    private readonly IMateriaFactory materiaFactory = materiaFactory;

    public MateriaGroup Create(
        ICollection<uint> materiaIds,
        uint? attachedItemId = null,
        bool isCollected = false,
        bool collectLock = false
    )
    {
        var materias = materiaIds.Select(
            id => materiaFactory.Create(id, isCollected, collectLock)
        ).ToList();
        return Create(materias, attachedItemId);
    }

    public MateriaGroup Create(
        ICollection<Materia> materias,
        uint? attachedItemId = null
        )
    {
        var materiaCount = materias.Count();
        var maxSlots = materiaCount;
        var normalSlots = materiaCount;
        var itemIsHq = false;
        if (attachedItemId is uint itemId)
        {
            itemIsHq = itemDataService.ItemIdIsHq(itemId);
            var (normal, advanced) = itemDataService.GetItemMateriaSlotCount(itemId);
            normalSlots = normal;
            var itemSlots = normal + advanced;

            if (itemSlots >= materiaCount)
                maxSlots = itemSlots;
            else
                logger.Warning($"Max slots on attached item {itemDataService.GetItemNameById(itemId)}/{itemId} ({maxSlots}) lower than materia id count ({materiaCount})");
        }

        foreach (var (idx, materia) in materias.Index())
        {
            if (idx < normalSlots)
                materia.PercentChanceToAttach = 100;
            else
                materia.PercentChanceToAttach = itemDataService
                    .GetPercentChanceToAttach((uint)materia.MateriaLevel, idx - normalSlots, itemIsHq);
        }

        return new MateriaGroup(
            materias,
            maxSlots,
            normalSlots,
            attachToHq: itemIsHq
            );
    }
}

public interface IMateriaGroupFactory
{
    public MateriaGroup Create(
        ICollection<Materia> materias,
        uint? attachedItemId = null
        );

    public MateriaGroup Create(
        ICollection<uint> materiaIds,
        uint? attachedItemId = null,
        bool isCollected = false,
        bool collectLock = false
        );
}
