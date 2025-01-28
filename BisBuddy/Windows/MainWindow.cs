using BisBuddy.Gear;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BisBuddy.Windows;

public partial class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    internal bool InventoryScanRunning = false;
    internal int InventoryScanUpdateCount = -1;

    private static Vector4 UnobtainedColor = new(1.0f, 0.2f, 0.2f, 1.0f);
    private static Vector4 ObtainedColor = new(0.2f, 1.0f, 0.2f, 1.0f);

    public MainWindow(Plugin plugin)
        : base($"{Plugin.PluginName}##bisbuddymainwindow")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(530, 250),
            MaximumSize = new Vector2(1000, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public override void OnClose()
    {
        base.OnClose();
        InventoryScanRunning = false;
        InventoryScanUpdateCount = -1;
    }

    public void Dispose()
    {
    }

    private void drawHeader()
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var configButtonSize = ImGui.CalcTextSize(FontAwesomeIcon.Cog.ToIconString()) + ImGui.GetStyle().FramePadding * 2;
        if (ImGui.Button($"{FontAwesomeIcon.Cog.ToIconString()}##bisbuddysettings", configButtonSize))
        {
            plugin.ToggleConfigUI();
        }
        ImGui.PopFont();

        ImGui.SameLine();

        ImGui.BeginDisabled(!Services.ClientState.IsLoggedIn);
        ImGui.BeginDisabled(plugin.Gearsets.Count >= Plugin.MaxGearsetCount);
        if (ImGui.Button("New Gearset##importgearset"))
        {
            if (Services.ClientState.IsLoggedIn)
            {
                plugin.ToggleImportGearsetUI();
            }
        }
        if (ImGui.IsItemHovered())
        {
            var tooltip =
                plugin.Gearsets.Count >= Plugin.MaxGearsetCount
                ? $"Maximum of {Plugin.MaxGearsetCount} gearsets"
                : "Import a new gearset";
            ImGui.SetTooltip(tooltip);
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(InventoryScanRunning);
        if (ImGui.Button("Sync Inventory##scaninventory"))
        {
            InventoryScanRunning = true;
            plugin.UpdateFromInventory(plugin.Gearsets);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Update gearsets with items from your inventory");
        ImGui.EndDisabled();
        ImGui.EndDisabled();

        if (InventoryScanRunning || InventoryScanUpdateCount >= 0)
        {
            var text = InventoryScanRunning
                ? "Loading..."
                : $"{InventoryScanUpdateCount} gearpieces updated";

            ImGui.SameLine();
            ImGui.Text(text);
        }
    }

    private void drawNoGearsets()
    {
        var errorText = Services.ClientState.IsLoggedIn ?
            "No gearsets found. Add one or more gearsets to get started"
            : "Please log in to view/add gearsets";
        var textWidth = ImGui.CalcTextSize(errorText).X;
        var offsetX = (ImGui.GetWindowWidth() - textWidth) * 0.5f;
        ImGui.SetCursorPosX(offsetX); // Center text
        ImGui.Text(errorText);
    }

    private void drawGearsets(List<Gearset> gearsets)
    {
        var gearsetsToDelete = new List<int>();

        for (var i = 0; i < gearsets.Count; i++)
        {
            var gearset = gearsets[i];
            ImGui.PushID(gearset.Id);
            var deleteGearset = drawGearset(gearset);
            if (deleteGearset)
            {
                gearsetsToDelete.Add(i);
            }
            ImGui.PopID();
        }

        if (gearsetsToDelete.Count > 0)
        {
            foreach (var gearsetIndex in gearsetsToDelete)
            {
                Services.Log.Verbose($"Removed gearset {gearsetIndex}");
                gearsets.RemoveAt(gearsetIndex);
            }
            plugin.SaveGearsetsWithUpdate();
        }
    }

    public override void Draw()
    {
        var headerWidth = ImGui.GetWindowWidth();
        var headerHeight = ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2;
        if (ImGui.BeginChild($"###{Plugin.PluginName}_main_header", new Vector2(headerWidth, headerHeight), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            drawHeader();
            ImGui.EndChild();
            ImGui.Spacing();
            ImGui.Separator();
        }

        var childWidth = ImGui.GetWindowWidth() - 12;
        ImGui.BeginChild($"###{Plugin.PluginName}_main_content", new Vector2(childWidth, 0), false, ImGuiWindowFlags.AlwaysAutoResize);

        if (plugin.Gearsets.Count == 0)
        {
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Spacing();

            drawNoGearsets();
        }
        else
        {
            drawGearsets(plugin.Gearsets);
        }

        ImGui.EndChild();
    }
}
