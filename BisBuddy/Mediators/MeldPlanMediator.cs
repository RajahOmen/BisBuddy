using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using BisBuddy.Services.Gearsets;
using System;
using System.Collections.Generic;

namespace BisBuddy.Mediators
{
    public class MeldPlanMediator(
        IGearsetsService gearsetsService
        ) : IMeldPlanService
    {
        private readonly IGearsetsService gearsetsService = gearsetsService;

        private IReadOnlyList<(Gearset, MateriaGroup)> currentMeldPlans = [];
        private int currentMeldPlanIndex = 0;
        private uint? currentItemId;
        public IReadOnlyList<(Gearset, MateriaGroup)> CurrentMeldPlans
        {
            get => currentMeldPlans;
            set
            {
                currentMeldPlans = value;
                // "refresh" this to perform new bounds check
                CurrentMeldPlanIndex = CurrentMeldPlanIndex;
            }
        }
        public int CurrentMeldPlanIndex
        {
            get => currentMeldPlanIndex;
            set
            {
                // change, but stay within bounds of meld plan list
                currentMeldPlanIndex = Math.Max(
                    Math.Min(value, currentMeldPlans.Count - 1),
                    0
                    );
            }
        }
        public (Gearset, MateriaGroup)? CurrentMeldPlan =>
            currentMeldPlans.Count > 0
            ? currentMeldPlans[currentMeldPlanIndex]
            : null;

        public void SetCurrentMeldPlanItemId(uint? newItemId)
        {
            if (currentItemId == newItemId)
                return;

            currentItemId = newItemId;

            if (newItemId is uint id)
                CurrentMeldPlans = gearsetsService.GetNeededItemMeldPlans(id);
            else
                CurrentMeldPlans = [];
        }
    }

    public interface IMeldPlanService
    {
        public (Gearset Gearset, MateriaGroup MateriaGroup)? CurrentMeldPlan { get; }
        public IReadOnlyList<(Gearset Gearset, MateriaGroup MateriaGroup)> CurrentMeldPlans { get; }
        public int CurrentMeldPlanIndex { get; set; }
        public void SetCurrentMeldPlanItemId(uint? newItemId);
    }
}
