using BisBuddy.Import;
using BisBuddy.Resources;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace BisBuddy.Windows;

public class ImportGearsetWindow : Window, IDisposable
{
    private string gearsetSourceString = string.Empty;
    private ImportSourceType gearsetSourceType = ImportSourceType.Xivgear;
    private bool importLoading = false;
    private GearsetImportStatusType? importStatus;
    private int importedGearsetCount = -1;

    private static readonly Dictionary<GearsetImportStatusType, string> ImportStatusTypeMessage = new()
    {
        { GearsetImportStatusType.Success, Resource.ImportSuccess },
        { GearsetImportStatusType.InternalError, Resource.ImportFailInternalError },
        { GearsetImportStatusType.InvalidInput, Resource.ImportFailInvalidInput },
        { GearsetImportStatusType.InvalidResponse, Resource.ImportFailInvalidResponse },
        { GearsetImportStatusType.NoGearsets, Resource.ImportFailNoGearsets },
        { GearsetImportStatusType.TooManyGearsets, Resource.ImportFailTooManyGearsets },
    };

    private static readonly Dictionary<ImportSourceType, string> ImportSourceTypeNames = new()
    {
        { ImportSourceType.Xivgear, "Xivgear.app" },
        { ImportSourceType.Etro, "Etro.gg" },
        { ImportSourceType.Json, $"JSON" },
        { ImportSourceType.Teamcraft, "Teamcraft" },
    };

    private static readonly Dictionary<ImportSourceType, string> ImportSourceTypeTooltips = new()
    {
        { ImportSourceType.Xivgear, Resource.ImportXivgearTooltip },
        { ImportSourceType.Etro, Resource.ImportEtroTooltip },
        { ImportSourceType.Json, Resource.ImportJsonTooltip },
        { ImportSourceType.Teamcraft, Resource.ImportTeamcraftTooltip },
    };

    public ImportGearsetWindow(Plugin plugin)
        : base($"{Resource.ImportWindowTitle}##bisbuddy import gearset", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(370, 135),
            MaximumSize = new Vector2(1000, 1000)
        };

        Size = new Vector2(500, 180);
        SizeCondition = ImGuiCond.Appearing;
    }

    private async Task ImportNewGearsets()
    {
        try
        {
            // no gearset to import
            if (gearsetSourceString.IsNullOrEmpty())
            {
                importStatus = GearsetImportStatusType.InvalidInput;
                return;
            }

            importLoading = true;
            var importResult = await Services.ImportGearsetService.ImportGearsets(gearsetSourceType, gearsetSourceString);
            gearsetSourceString = string.Empty;
            importStatus = importResult.StatusType;
            importedGearsetCount = importResult.Gearsets != null ? importResult.Gearsets.Count : -1;
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, $"Internal Error");
            importStatus = GearsetImportStatusType.InternalError;
        }
        finally
        {
            importLoading = false;
        }
    }

    public void Dispose() { }

    public override void OnClose()
    {
        base.OnClose();
        gearsetSourceString = string.Empty;
        importStatus = null;
        importLoading = false;
        importedGearsetCount = -1;
    }

    public override void Draw()
    {

        //ImGui.Text(Resource.ImportWebGearsetText);

        var sourceOptions = Services.ImportGearsetService.RegisteredSources();
        using (var sourceOptionsTable = ImRaii.Table("Source options", sourceOptions.Count, ImGuiTableFlags.BordersInnerV))
        using (ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f)))
        {
            if (!sourceOptionsTable || sourceOptions.Count == 0)
                return;

            foreach (var source in sourceOptions)
            {
                ImGui.TableNextColumn();

                var sourceSelected = gearsetSourceType == source;
                if (ImGui.Selectable(ImportSourceTypeNames.GetValueOrDefault(source, "Unknown Source"), sourceSelected))
                {
                    gearsetSourceType = source;
                    importStatus = null;
                    importedGearsetCount = -1;
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(ImportSourceTypeTooltips.GetValueOrDefault(source, "Unknown Source"));
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var sizeAvailable = ImGui.GetContentRegionAvail();
        var footerHeight =
            ImGui.GetTextLineHeightWithSpacing()
            + ImGui.GetStyle().FramePadding.Y * 2
            + ImGui.GetStyle().ItemSpacing.Y * 3;

        // {Resource.ImportWebGearsetInputLabel}
        ImGui.InputTextMultiline(
            $"###gearsetimportstring",
            ref gearsetSourceString,
            100000,
            new Vector2(sizeAvailable.X, sizeAvailable.Y - footerHeight)
            );

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(ImportSourceTypeTooltips.GetValueOrDefault(gearsetSourceType, "Unknown Source"));

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var sourceName = ImportSourceTypeNames.GetValueOrDefault(gearsetSourceType, "Unknown Source");

        using (ImRaii.Disabled(importLoading || gearsetSourceString == string.Empty))
        {
            if (ImGui.Button($"{string.Format(Resource.ImportGearsetButton, sourceName)}###import gearset button"))
            {
                importStatus = null;
                importedGearsetCount = -1;
                _ = ImportNewGearsets();
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(string.Format(Resource.ImportGearsetTooltip, sourceName));
        }

        ImGui.SameLine();
        if (importStatus != null)
        {
            var message = ImportStatusTypeMessage[importStatus.Value];
            ImGui.Text(string.Format(message, Resource.ImportFailBase, sourceName, importedGearsetCount));
        }
        else if (importLoading)
        {
            ImGui.Text(Resource.ImportGearsetLoading);
        }
    }
}
