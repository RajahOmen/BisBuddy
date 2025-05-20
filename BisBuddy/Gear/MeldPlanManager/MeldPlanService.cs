using BisBuddy.Gear.GearsetsManager;
using BisBuddy.Services.AddonEventListeners;
using BisBuddy.Windows;
using System;
using System.Collections.Generic;

namespace BisBuddy.Gear.MeldPlanManager
{
    public class MeldPlanService
    {
        private readonly IGearsetsService gearsetsService;
        private readonly MeldPlanSelectorWindow meldPlanSelectorWindow;
        private readonly MateriaAttachEventListener materiaAttachEventListener;

        private List<MeldPlan> currentMeldPlans = [];
        private int currentPlanIdx = 0;
        public IReadOnlyList<MeldPlan> CurrentMeldPlans => currentMeldPlans;
        public int CurrentPlanIdx => currentPlanIdx;

        public MeldPlanService(
            IGearsetsService gearsetsService,
            MeldPlanSelectorWindow meldPlanSelectorWindow,
            MateriaAttachEventListener materiaAttachEventListener
            )
        {
            this.gearsetsService = gearsetsService;
            this.meldPlanSelectorWindow = meldPlanSelectorWindow;
            this.materiaAttachEventListener = materiaAttachEventListener;
        }

        public void UpdateMeldPlans(uint? newItemId)
        {
            if (newItemId is not null)
                currentMeldPlans = gearsetsService.GetNeededItemMeldPlans(newItemId!.Value);
            else
                currentMeldPlans.Clear();
        }

        public void UpdateMeldPlanIndex(int newPlanIdx)
        {
            if (currentPlanIdx == newPlanIdx)
                return;

            if (newPlanIdx < 0 || newPlanIdx >= currentMeldPlans.Count)
                return;


            currentPlanIdx = newPlanIdx;
            // todo: setter in the listener to
            materiaAttachEventListener.selectedMeldPlanIndex = currentPlanIdx;
        }

        public delegate void MeldPlanIdxSelectedChangeHandler(int newIdx);
        public event MeldPlanIdxSelectedChangeHandler? OnMeldPlanIdxSelectedChange;
        public void TriggerMeldPlanIdxSelectedChange(int newIdx) =>
            OnMeldPlanIdxSelectedChange?.Invoke(newIdx);
    }
}
