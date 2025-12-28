using BisBuddy.Extensions;
using BisBuddy.Gear;
using BisBuddy.Gear.Melds;
using BisBuddy.Items;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces.Companding;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Main;

public class MateriaCountsTab : TabRenderer<MainWindowTab>, IDisposable
{
    private static readonly WindowSizeConstraints? SizeConstraints = new()
    {
        MinimumSize = new(250, 150),
    };
    private readonly ITypedLogger<MateriaCountsTab> logger;
    private readonly IGearsetsService gearsetsService;
    private readonly IAttributeService attributeService;
    private readonly ITextureProvider textureProvider;
    private readonly IItemDataService itemDataService;
    private readonly IInventoryItemsService inventoryItemsService;
    private readonly IConfigurationService configurationService;
    private readonly IItemFinderService itemFinderService;
    private const double DefaultMeldConfidenceRate = 0.7;
    private double meldConfidenceRate = DefaultMeldConfidenceRate;
    private double probabilityNumerator = calcProbabilityLog(DefaultMeldConfidenceRate);

    private HashSet<Gearset> gearsetsToCount;
    private List<HashSet<Gearset>> gearsetsWithOverlap = [];
    private bool listDirty = true;


    public MateriaCountsTab(
        ITypedLogger<MateriaCountsTab> logger,
        IGearsetsService gearsetsService,
        IAttributeService attributeService,
        ITextureProvider textureProvider,
        IItemDataService itemDataService,
        IInventoryItemsService inventoryItemsService,
        IConfigurationService configurationService,
        IItemFinderService itemFinderService
    )
    {
        this.logger = logger;
        this.gearsetsService = gearsetsService;
        this.attributeService = attributeService;
        this.textureProvider = textureProvider;
        this.itemDataService = itemDataService;
        this.inventoryItemsService = inventoryItemsService;
        this.configurationService = configurationService;
        this.itemFinderService = itemFinderService;
        gearsetsToCount = gearsetsService
            .CurrentGearsets
            .Where(g => g.IsActive)
            .ToHashSet();
        calculateMateriaRequiredCounts();
        calculateGearsetsWithOverlap();
        this.gearsetsService.OnGearsetsChange += handleGearsetsChange;
    }

    private List<(
        Materia Materia,
        int SlotCount,
        int NeededCount,
        List<double> ProbabilityDenominator
    )> materiaRequiredCounts = [];

    public WindowSizeConstraints? TabSizeConstraints => SizeConstraints;

    public bool ShouldDraw => true;

    public void Dispose()
    {
        this.gearsetsService.OnGearsetsChange -= handleGearsetsChange;
    }

    private void handleGearsetsChange()
    {
        if (gearsetsToCount.Count == 0)
            gearsetsToCount = gearsetsService
                .CurrentGearsets
                .Where(g => g.IsActive)
                .ToHashSet();
        else
            gearsetsToCount = gearsetsToCount
                .Where(g => gearsetsService.CurrentGearsets.Contains(g))
                .ToHashSet();
        calculateGearsetsWithOverlap();
        calculateMateriaRequiredCounts();
    }

    private void calculateGearsetsWithOverlap()
    {
        var inCommonGroups = gearsetsService
            .CurrentGearsets
            .SelectMany(gs => gs.Gearpieces.Select(gp => (Gearpiece: gp, Gearset: gs)).DistinctBy(g => g.Gearpiece.ItemId))
            .GroupBy(g => g.Gearpiece.ItemId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Select(i => i.Gearset).ToHashSet());

        gearsetsWithOverlap.Clear();
        foreach (var group in inCommonGroups)
        {
            if (inCommonGroups.Any(g => g.IsProperSupersetOf(group)))
                continue;
            if (gearsetsWithOverlap.Any(g => g.SetEquals(group)))
                continue;

            gearsetsWithOverlap.Add(group);
        }
    }

    private void calculateMateriaRequiredCounts()
    {
        materiaRequiredCounts.Clear();
        materiaRequiredCounts = gearsetsService
            .CurrentGearsets
            .Where(gearsetsToCount.Contains)
            .SelectMany(gs => gs.Gearpieces)
            .SelectMany(gp => gp.ItemMateria)
            .Where(m => !m.IsCollected)
            .GroupBy(m => m.ItemId)
            .Select(g =>
            {
                var denominators = g
                    .Select(m => 1 / calcProbabilityLog(m.PercentChanceToAttach / 100d))
                    .ToList();

                return (
                    g.First(),
                    g.Count(),
                    calculateAmountNeeded(denominators),
                    denominators
                );
            }).ToList();
        listDirty = true;
    }

    private int calculateAmountNeeded(List<double> denominators) =>
        denominators.Sum(denom => denom == 0
            ? 1
            : (int)Math.Ceiling(probabilityNumerator * denom)
        );

    private int calculateAmountInventory(uint materiaId) =>
        inventoryItemsService.ItemInventoryQuantities.GetValueOrDefault(materiaId).Count;

    private int calculateAmountRemaining(int amountInv, int amountNeeded) =>
        Math.Max(0, amountNeeded - amountInv);

    private void recalculateAmountsNeeded()
    {
        for (var i = 0; i < materiaRequiredCounts.Count; i++)
        {
            var materiaInfo = materiaRequiredCounts[i];
            var amountNeeded = calculateAmountNeeded(materiaInfo.ProbabilityDenominator);
            materiaRequiredCounts[i] = materiaInfo with
            {
                NeededCount = amountNeeded,
            };
        }
        listDirty = true;
    }

    public void SetTabState(TabState state)
    {
        throw new NotImplementedException();
    }

    public void PreDraw()
    {

    }

    private static double calcProbabilityLog(double probability) =>
        Math.Log(1 - probability);

    public void Draw()
    {
        using var _ = ImRaii.Child("##materia_quantity_info", Vector2.Zero, border: true);

        drawHeader();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (gearsetsToCount.Count == 0)
        {
            ImGui.NewLine();
            ImGuiHelpers.CenteredText(Resource.MateriaCountsNoSelectedGearsets);
            return;
        }

        if (materiaRequiredCounts.Count == 0)
        {
            ImGui.NewLine();
            ImGuiHelpers.CenteredText(Resource.MateriaCountsNoUnmeldedMateria);
            return;
        }

        drawMateriaTable();
    }

    private void drawHeader()
    {
        var uiTheme = configurationService.UiTheme;

        var unobtainedColor = uiTheme.UnobtainedTextColor;
        var obtainedColor = uiTheme.ObtainedCompleteTextColor;
        SRgbCompanding.Expand(ref unobtainedColor);
        SRgbCompanding.Expand(ref obtainedColor);
        var textColor = Vector4.Lerp(unobtainedColor, obtainedColor, (float)meldConfidenceRate);
        SRgbCompanding.Compress(ref textColor);

        using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(10f, 5f) * ImGuiHelpers.GlobalScale);

        var meldPercent = meldConfidenceRate * 100;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

        //using (ImRaii.PushColor(ImGuiCol.Text, textColor))
        using (ImRaii.PushColor(ImGuiCol.FrameBg, textColor with { W = 0.25f }))
        using (ImRaii.PushColor(ImGuiCol.FrameBgActive, textColor with { W = 0.35f }))
        using (ImRaii.PushColor(ImGuiCol.SliderGrab, textColor with { W = 0.85f }))
        using (ImRaii.PushColor(ImGuiCol.SliderGrabActive, textColor with { W = 1f }))
            if (ImGui.SliderDouble(
                "",
                ref meldPercent,
                vMin: 1,
                vMax: 99,
                format: $"%.1f%% {Resource.MateriaCountsMeldConfidenceSliderLabel}",
                ImGuiSliderFlags.AlwaysClamp)
            )
            {
                meldConfidenceRate = meldPercent / 100;
                probabilityNumerator = calcProbabilityLog(meldConfidenceRate);
                recalculateAmountsNeeded();
            }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Format(
                Resource.MateriaCountsMeldConfidenceTooltip,
                $"{meldConfidenceRate:P1}"
            ));

        ImGui.Spacing();

        var comboPreview = (gearsetsToCount.Count, gearsetsService.CurrentGearsets.Count) switch
        {
            (_, 0) => Resource.MateriaCountsGearsetsComboNoneLoadedPreview,
            (0, _) => Resource.MateriaCountsGearsetsComboNoneSelectedPreview,
            (1, _) => gearsetsToCount.First().Name,
            (var count, var total) when count == total => Resource.MateriaCountsGearsetsComboAllSelectedPreview,
            _ => string.Format(
                Resource.MateriaCountsGearsetsComboCountSelectedPreview,
                $"{gearsetsToCount.Count}"
                )
        };
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

        using (ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 1f))
        using (ImRaii.PushStyle(ImGuiStyleVar.PopupRounding, 5f))
        using (ImRaii.Disabled(gearsetsService.CurrentGearsets.Count == 0))
        using (var combo = ImRaii.Combo("##gearset_materia_select_combo", comboPreview))
        {
            if (combo)
            {
                using var vAlign = ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0.5f));
                var selectableSize = new Vector2(0, ImGui.GetTextLineHeight() + 5f * ImGuiHelpers.GlobalScale);
                var allSelected = gearsetsToCount.Count == gearsetsService.CurrentGearsets.Count;
                if (allSelected && ImGui.Selectable(Resource.MateriaCountsGearsetsComboNoneOption, selected: true, ImGuiSelectableFlags.DontClosePopups, size: selectableSize))
                {
                    gearsetsToCount.Clear();
                    calculateMateriaRequiredCounts();
                }
                else if (!allSelected && ImGui.Selectable(Resource.MateriaCountsGearsetsComboAllOption, selected: true, ImGuiSelectableFlags.DontClosePopups, size: selectableSize))
                {
                    gearsetsToCount = gearsetsService.CurrentGearsets.ToHashSet();
                    calculateMateriaRequiredCounts();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                using var headerCol = ImRaii.PushColor(ImGuiCol.Header, configurationService.UiTheme.ObtainedCompleteTextColor with { W = 0.25f });

                foreach (var gearset in gearsetsService.CurrentGearsets)
                {
                    var selected = gearsetsToCount.Contains(gearset);
                    using var alpha = ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.6f, !gearset.IsActive);
                    if (ImGui.Selectable(gearset.Name, selected: selected, ImGuiSelectableFlags.DontClosePopups, size: selectableSize))
                    {
                        if (selected)
                            gearsetsToCount.Remove(gearset);
                        else
                            gearsetsToCount.Add(gearset);
                        calculateMateriaRequiredCounts();
                    }
                }
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Resource.MateriaCountsGearsetsComboTooltip);

        if (gearsetsWithOverlap.Count > 0)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, KnownColor.Yellow.Vector()))
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 3f * ImGuiHelpers.GlobalScale);
                using (ImRaii.PushFont(UiBuilder.IconFont))
                    ImGui.Text(FontAwesomeIcon.ExclamationTriangle.ToIconString());
                var iconHovered = ImGui.IsItemHovered();
                ImGui.SameLine();
                ImGui.Text(Resource.MateriaCountsOvercountWarningText);

                var gearsetGroups = string.Join("\n", gearsetsWithOverlap.Select(g => string.Join(", ", g.Select(g => g.Name))));
                if (iconHovered || ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.Format(
                        Resource.MateriaCountsOvercountWarningTooltip,
                        gearsetGroups
                        ));
            }
        }
    }

    private void drawMateriaTable()
    {
        using var table = ImRaii.Table("##materia_quantities_tableasdas", 6, ImGuiTableFlags.BordersInner | ImGuiTableFlags.PadOuterX | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable);
        if (!table)
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn(Resource.MateriaCountsMateriaNameHeader, initWidthOrWeight: 2.5f);
        ImGui.TableSetupColumn(Resource.MateriaCountsStatHeader, ImGuiTableColumnFlags.DefaultSort);
        ImGui.TableSetupColumn(Resource.MateriaCountsSlotsHeader);
        ImGui.TableSetupColumn(Resource.MateriaCountsNeededHeader);
        ImGui.TableSetupColumn(Resource.MateriaCountsInventoryHeader);
        ImGui.TableSetupColumn(Resource.MateriaCountsRemainingHeader);

        ImGui.TableHeadersRow();

        var sortSpecs = ImGui.TableGetSortSpecs();
        if (sortSpecs.SpecsDirty || listDirty)
        {
            sortSpecs.SpecsDirty = false;
            listDirty = false;
            var desc = sortSpecs.Specs.SortDirection == ImGuiSortDirection.Descending;
            materiaRequiredCounts = [.. sortSpecs.Specs.ColumnIndex switch
            {
                0 => materiaRequiredCounts.OrderByDirection(m => m.Materia.ItemName, desc),
                1 => materiaRequiredCounts
                    .OrderByDirection(m => m.Materia.StatType, desc)
                    .ThenByDirection(m => m.Materia.MateriaLevel, !desc),
                2 => materiaRequiredCounts.OrderByDirection(m => m.SlotCount, desc),
                3 => materiaRequiredCounts.OrderByDirection(m => m.NeededCount, desc),
                4 => materiaRequiredCounts.OrderByDirection(m => calculateAmountInventory(m.Materia.ItemId), desc),
                5 => materiaRequiredCounts.OrderByDirection(m =>
                    calculateAmountRemaining(calculateAmountInventory(m.Materia.ItemId), m.NeededCount),
                    desc
                ),
                _ => materiaRequiredCounts
                    .OrderByDirection(m => m.Materia.StatType, desc)
                    .ThenByDirection(m => m.Materia.MateriaLevel, !desc)
            }];
        }

        var padding = 7f * ImGuiHelpers.GlobalScale;
        var lineHeight = ImGui.GetTextLineHeight();
        var selectableSize = new Vector2(0, lineHeight * 2);
        var uiTheme = configurationService.UiTheme;
        foreach (var materiaInfo in materiaRequiredCounts)
        {
            var (materia, slotCount, amountNeeded, _) = materiaInfo;
            using var id = ImRaii.PushId($"{materia.ItemId}");
            ImGui.TableNextRow();

            var amountInv = calculateAmountInventory(materia.ItemId);
            var amountRemaining = calculateAmountRemaining(amountInv, amountNeeded);

            ImGui.TableNextColumn();
            using (ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0, 0.5f)))
            {
                var curPos = ImGui.GetCursorPos();
                curPos.Y += 1;
                ImGui.SetCursorPos(curPos);
                ImGui.Selectable("", flags: ImGuiSelectableFlags.SpanAllColumns, size: selectableSize);
                var selectableHovered = ImGui.IsItemHovered();
                var selectableClicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);

                var nextPos = ImGui.GetCursorPos();
                ImGui.SetCursorPos(curPos with { Y = curPos.Y + 3 * ImGuiHelpers.GlobalScale });

                var iconSize = new Vector2(selectableSize.Y - 6 * ImGuiHelpers.GlobalScale);
                var itemIconId = itemDataService.GetItemIconId(materia.ItemId);
                var imageHovered = false;
                if (textureProvider.TryGetFromGameIcon((uint)itemIconId, out var texture)
                    && texture.TryGetWrap(out var iconImage, out var exc))
                {
                    ImGui.Image(iconImage.Handle, iconSize);
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        imageHovered = true;
                        ImGui.BeginTooltip();
                        ImGui.Image(iconImage.Handle, iconSize * 4);
                        ImGui.EndTooltip();
                    }
                }

                if (selectableHovered && !imageHovered)
                {
                    if (selectableClicked)
                        itemFinderService.SearchForItem(materiaInfo.Materia.ItemId);

                    using (ImRaii.Tooltip())
                    {
                        using (ImRaii.PushFont(UiBuilder.IconFont))
                            ImGui.Text(FontAwesomeIcon.Search.ToIconString());
                        ImGui.SameLine();
                        ImGui.Text(string.Format(
                            Resource.MateriaCountsSearchInventory,
                            materia.ItemName
                            ));
                    }
                }

                ImGui.SameLine();
                ImGui.SetCursorPos(curPos + new Vector2(iconSize.X + padding, lineHeight / 2));
                ImGui.Text(materia.ItemName);
                ImGui.TableNextColumn();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1f);
                ImGui.Selectable(materia.StatStrength, size: selectableSize);
            }
            using var rightAlign = ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(1, 0.5f));
            ImGui.TableNextColumn();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1f);
            ImGui.Selectable($"{slotCount}", size: selectableSize);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1f);
            ImGui.Selectable($"{amountNeeded}", size: selectableSize);
            ImGui.TableNextColumn();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1f);
            ImGui.Selectable($"{amountInv}", size: selectableSize);
            ImGui.TableNextColumn();

            Vector4 remainingTextColor;
            if (amountRemaining == 0)
                remainingTextColor = uiTheme.ObtainedCompleteTextColor;
            else if (amountRemaining == amountNeeded)
                remainingTextColor = uiTheme.UnobtainedTextColor;
            else
                remainingTextColor = uiTheme.NotObtainablePartialTextColor;

            remainingTextColor.W *= 0.25f;

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 1f);
            using (ImRaii.PushColor(ImGuiCol.Header, remainingTextColor))
                ImGui.Selectable($"{amountRemaining}", size: selectableSize, selected: true);
        }
    }
}
