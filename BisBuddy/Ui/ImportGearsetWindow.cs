using BisBuddy.Extensions;
using BisBuddy.Import;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Gearsets;
using BisBuddy.Services.ImportGearset;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace BisBuddy.Ui;

public class ImportGearsetWindow : Window, IDisposable
{
    private readonly ITypedLogger<ImportGearsetWindow> logger;
    private readonly IClientState clientState;
    private readonly IGearsetsService gearsetsService;
    private readonly List<IImportGearsetSource> importGearsetSources;
    private readonly IAttributeService attributeService;

    private string gearsetSourceString = string.Empty;
    private ImportGearsetSourceType gearsetSourceType = ImportGearsetSourceType.Xivgear;
    private bool importLoading = false;
    private GearsetImportStatusType? importStatus;
    private int importedGearsetCount = -1;

    public ImportGearsetWindow(
        ITypedLogger<ImportGearsetWindow> logger,
        IClientState clientState,
        IGearsetsService gearsetsService,
        IEnumerable<IImportGearsetSource> importGearsetSources,
        IAttributeService attributeService
        )
        : base($"{Resource.ImportWindowTitle}##bisbuddy import gearset", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(370, 135),
            MaximumSize = new Vector2(1000, 1000)
        };

        Size = new Vector2(500, 180);
        SizeCondition = ImGuiCond.Appearing;

        this.logger = logger;
        this.clientState = clientState;
        this.gearsetsService = gearsetsService;
        this.importGearsetSources = importGearsetSources.ToList();
        this.attributeService = attributeService;
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
            var importResult = await gearsetsService.AddGearsetsFromSource(gearsetSourceType, gearsetSourceString);
            gearsetSourceString = string.Empty;
            importStatus = importResult.StatusType;
            importedGearsetCount = importResult.Gearsets != null ? importResult.Gearsets.Count : -1;
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Internal Error");
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
        if (clientState.IsLoggedIn)
            drawLoggedIn();
        else
            drawLoggedOut();
    }

    private void drawLoggedOut()
    {
        ImGui.NewLine();
        ImGuiHelpers.CenteredText(Resource.ImportGearsetLoggedOut);
    }

    private void drawLoggedIn()
    {
        using (var sourceOptionsTable = ImRaii.Table("Source options", importGearsetSources.Count, ImGuiTableFlags.BordersInnerV))
        using (ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f)))
        {
            if (!sourceOptionsTable || importGearsetSources.Count == 0)
                return;

            foreach (var source in importGearsetSources)
            {
                ImGui.TableNextColumn();

                var sourceSelected = gearsetSourceType == source.SourceType;
                var sourceDisplay = attributeService.GetEnumAttribute<DisplayAttribute>(source.SourceType)!;

                if (ImGui.Selectable(sourceDisplay.GetName(), sourceSelected))
                {
                    gearsetSourceType = source.SourceType;
                    importStatus = null;
                    importedGearsetCount = -1;
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(sourceDisplay.GetDescription()!);
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

        var gearsetSourceDisplay = attributeService
                .GetEnumAttribute<DisplayAttribute>(gearsetSourceType)!;

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(gearsetSourceDisplay.GetDescription()!);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var sourceName = gearsetSourceDisplay.GetName()!;

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
        if (importStatus is GearsetImportStatusType importedStatus)
        {
            var importStatusDescription = attributeService
                .GetEnumAttribute<DisplayAttribute>(importedStatus)!
                .GetDescription()!;
            ImGui.Text(string.Format(importStatusDescription, Resource.ImportFailBase, sourceName, importedGearsetCount));
        }
        else if (importLoading)
        {
            ImGui.Text(Resource.ImportGearsetLoading);
        }
    }
}
