
using BisBuddy.Gear;
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

    private static readonly string GearsetImportFailBase = "Import Fail: ";
    private static readonly Dictionary<GearsetImportStatusType, string> ImportStatusTypeMessage = new()
    {
        { GearsetImportStatusType.Success, "Import Success" },
        { GearsetImportStatusType.InternalError, $"{GearsetImportFailBase}Internal Error" },
        { GearsetImportStatusType.NoJson, $"{GearsetImportFailBase}No JSON to Import" },
        { GearsetImportStatusType.InvalidJson, $"{GearsetImportFailBase}Invalid JSON" },
        { GearsetImportStatusType.InvalidUrl, $"{GearsetImportFailBase}Invalid URL" },
        { GearsetImportStatusType.NoWebResponse, $"{GearsetImportFailBase}No Response from URL" },
        { GearsetImportStatusType.InvalidWebResponse, $"{GearsetImportFailBase}Invalid Response from URL" },
        { GearsetImportStatusType.NoGearsets, $"{GearsetImportFailBase}No Gearsets Found" }
    };

    public ImportGearsetWindow(Plugin plugin)
        : base("Add New Gearset##bisbuddy import gearset", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
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
                Services.Log.Error("No gearsets found in URL");
                webImportStatus = GearsetImportStatusType.NoGearsets;
            }
            else
            {
                plugin.Gearsets.AddRange(newGearsets);

                if (plugin.Configuration.AutoScanInventory)
                {
                    plugin.UpdateFromInventory(newGearsets);
                }

                plugin.SaveGearsetsWithUpdate();
                Services.Log.Information($"Successfully imported {newGearsets.Count} gearset(s) from {gearsetUrl}");
                gearsetUrl = string.Empty;
                webImportStatus = GearsetImportStatusType.Success;
            }
        }
        catch (GearsetImportException ex)
        {
            webImportStatus = ex.FailStatusType;
        }
        catch (Exception)
        {
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
            jsonLoading = true;
            var newGearset = Gearset.ImportFromJson(gearsetJson);
            plugin.Gearsets.Add(newGearset);

            if (plugin.Configuration.AutoScanInventory)
            {
                plugin.UpdateFromInventory([newGearset]);
            }

            plugin.SaveGearsetsWithUpdate();
            Services.Log.Information($"Successfully imported gearset json");
            gearsetJson = string.Empty;
            jsonImportStatus = GearsetImportStatusType.Success;
        }
        catch (GearsetImportException ex)
        {
            jsonImportStatus = ex.FailStatusType;
        }
        catch (Exception)
        {
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

        ImGui.Text("Import gearset(s) from Xivgear.app or Etro.gg");
        ImGui.InputText("URL###gearseturl", ref gearsetUrl, 512, ImGuiInputTextFlags.None);

        ImGui.BeginDisabled(webLoading || gearsetUrl == string.Empty);
        if (ImGui.Button("Import###import web button"))
        {
            webImportStatus = null;
            _ = ImportNewWebGearset();
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (webImportStatus != null)
        {
            ImGui.Text(ImportStatusTypeMessage[webImportStatus.Value]);
        }
        else if (webLoading)
        {
            ImGui.Text("Loading...");
        }

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Spacing();

        ImGui.Text("Import gearset from JSON");

        ImGui.InputText("JSON##importgearsetjson", ref gearsetJson, 10000);

        ImGui.BeginDisabled(jsonLoading || gearsetJson == string.Empty);
        if (ImGui.Button("Import###import json button"))
        {
            jsonImportStatus = null;
            ImportNewJsonGearset();
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (jsonImportStatus != null)
        {
            ImGui.Text(ImportStatusTypeMessage[jsonImportStatus.Value]);
        }
        else if (jsonLoading)
        {
            ImGui.Text("Loading...");
        }
    }
}
