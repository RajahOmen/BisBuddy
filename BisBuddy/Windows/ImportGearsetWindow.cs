
using BisBuddy.Gear;
using BisBuddy.Resources;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace BisBuddy.Windows;

public class ImportGearsetWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private string gearsetUrl = string.Empty;

    private string gearsetJson = string.Empty;

    private bool webLoading = false;
    private bool jsonLoading = false;

    private GearsetImportStatusType? webImportStatus;

    private GearsetImportStatusType? jsonImportStatus;

    private static readonly Dictionary<GearsetImportStatusType, string> ImportStatusTypeMessage = new()
    {
        { GearsetImportStatusType.Success, Resource.ImportSuccess },
        { GearsetImportStatusType.InternalError, $"{Resource.ImportFailBase}{Resource.ImportInternalError}" },
        { GearsetImportStatusType.NoJson, $"{Resource.ImportFailBase}{Resource.ImportNoJson}" },
        { GearsetImportStatusType.InvalidJson, $"{Resource.ImportFailBase}{Resource.ImportInvalidJson}" },
        { GearsetImportStatusType.InvalidUrl, $"{Resource.ImportFailBase}{Resource.ImportInvalidUrl}" },
        { GearsetImportStatusType.NoWebResponse, $"{Resource.ImportFailBase}{Resource.ImportNoWebResponse}" },
        { GearsetImportStatusType.InvalidWebResponse, $"{Resource.ImportFailBase}{Resource.ImportInvalidWebResponse}" },
        { GearsetImportStatusType.NoGearsets, $"{Resource.ImportFailBase}{Resource.ImportNoGearsets}" },
        { GearsetImportStatusType.TooManyGearsets, $"{Resource.ImportFailBase}{string.Format(Resource.ImportTooManyGearsets, Plugin.MaxGearsetCount)}" }
    };

    public ImportGearsetWindow(Plugin plugin)
        : base($"{Resource.ImportWindowTitle}##bisbuddy import gearset", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 200),
            MaximumSize = new Vector2(1000, 200)
        };
    }

    private async Task ImportNewWebGearset()
    {
        try
        {
            if (gearsetUrl == string.Empty) return; // no gearset to import
            webLoading = true;
            var newGearsets = await Gearset.ImportFromRemote(gearsetUrl, plugin.ItemData);
            if (newGearsets.Count == 0)
            {
                throw new GearsetImportException(GearsetImportStatusType.NoGearsets);
            }
            else if (newGearsets.Count + plugin.Gearsets.Count > Plugin.MaxGearsetCount)
            {
                throw new GearsetImportException(GearsetImportStatusType.TooManyGearsets);
            }
            else
            {
                plugin.Gearsets.AddRange(newGearsets);
                plugin.SaveGearsetsWithUpdate(true);
                Services.Log.Information($"Successfully imported {newGearsets.Count} gearset(s) from {gearsetUrl}");
                gearsetUrl = string.Empty;
                webImportStatus = GearsetImportStatusType.Success;
            }
        }
        catch (GearsetImportException ex)
        {
            Services.Log.Error(ImportStatusTypeMessage.GetValueOrDefault(ex.FailStatusType) ?? "Unknown GearsetImportException");
            webImportStatus = ex.FailStatusType;
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, $"Internal Error");
            webImportStatus = GearsetImportStatusType.InternalError;
        }
        finally
        {
            webLoading = false;
        }
    }

    private void ImportNewJsonGearset()
    {
        try
        {
            if (gearsetJson == string.Empty) return; // no gearset to import
            if (plugin.Gearsets.Count >= Plugin.MaxGearsetCount)
            {
                throw new GearsetImportException(GearsetImportStatusType.TooManyGearsets);
            }
            jsonLoading = true;
            var newGearset = Gearset.ImportFromJson(gearsetJson);
            plugin.Gearsets.Add(newGearset);
            plugin.SaveGearsetsWithUpdate(true);
            Services.Log.Information($"Successfully imported gearset json");
            gearsetJson = string.Empty;
            jsonImportStatus = GearsetImportStatusType.Success;
        }
        catch (GearsetImportException ex)
        {
            Services.Log.Error(ImportStatusTypeMessage.GetValueOrDefault(ex.FailStatusType) ?? "Unknown GearsetImportException");
            jsonImportStatus = ex.FailStatusType;
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, $"Internal Error");
            jsonImportStatus = GearsetImportStatusType.InternalError;
        }
        finally
        {
            jsonLoading = false;
        }
    }

    public void Dispose() { }

    public override void OnClose()
    {
        base.OnClose();
        gearsetUrl = string.Empty;
        gearsetJson = string.Empty;
        webImportStatus = null;
        jsonImportStatus = null;
        webLoading = false;
        jsonLoading = false;
    }

    public override void Draw()
    {
        ImGui.Spacing();

        ImGui.Text(Resource.ImportWebGearsetText);
        ImGui.InputText($"{Resource.ImportWebGearsetInputLabel}###gearseturl", ref gearsetUrl, 512, ImGuiInputTextFlags.None);

        using (ImRaii.Disabled(webLoading || gearsetUrl == string.Empty))
        {
            if (ImGui.Button($"{Resource.ImportWebGearsetButton}###import web button"))
            {
                webImportStatus = null;
                _ = ImportNewWebGearset();
            }
        }

        ImGui.SameLine();
        if (webImportStatus != null)
        {
            ImGui.Text(ImportStatusTypeMessage[webImportStatus.Value]);
        }
        else if (webLoading)
        {
            ImGui.Text(Resource.ImportWebGearsetLoading);
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.Text(Resource.ImportJsonGearsetText);

        ImGui.InputText($"{Resource.ImportJsonGearsetInputLabel}##importgearsetjson", ref gearsetJson, 100000);

        using (ImRaii.Disabled(jsonLoading || gearsetJson == string.Empty))
        {
            if (ImGui.Button($"{Resource.ImportJsonGearsetButton}###import json button"))
            {
                jsonImportStatus = null;
                ImportNewJsonGearset();
            }
        }

        ImGui.SameLine();
        if (jsonImportStatus != null)
        {
            ImGui.Text(ImportStatusTypeMessage[jsonImportStatus.Value]);
        }
        else if (jsonLoading)
        {
            ImGui.Text(Resource.ImportJsonGearsetLoading);
        }
    }
}
