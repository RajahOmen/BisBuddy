using System;
using ImGuiNET;
using BisBuddy.Gear;
using BisBuddy.Services.Gearsets;
using Dalamud.Interface.Utility.Raii;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using BisBuddy.Resources;
using BisBuddy.Ui.Components;
using static Dalamud.Interface.Windowing.Window;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using BisBuddy.Services.Configuration;
using BisBuddy.Util;
using BisBuddy.Services;
using System.Linq;
using Dalamud.Utility;

namespace BisBuddy.Ui.Main.Tabs
{
    public class UserGearsetsTab : TabRenderer, IDisposable
    {
        private readonly IClientState clientState;
        private readonly ITextureProvider textureProvider;
        private readonly IGearsetsService gearsetsService;
        private readonly IConfigurationService configurationService;
        private readonly IWindowService windowService;
        private readonly UiComponents uiComponents;
        private readonly IComponentRendererFactory componentRendererFactory;

        private Gearset? activeGearset = null;
        private Gearpiece? nextActiveGearpiece = null;
        private Gearset? gearsetToDelete = null;
        private GearsetSortType activeSortType = GearsetSortType.Priority;
        private bool sortDescending = false;
        private bool firstLoggedInDrawCall = false;

        public WindowSizeConstraints? TabSizeConstraints => new()
        {
            MinimumSize = new(500, 150),
            MaximumSize = new(0, 0)
        };

        public UserGearsetsTab(
            IClientState clientState,
            ITextureProvider textureProvider,
            IGearsetsService gearsetsService,
            IConfigurationService configurationService,
            IWindowService windowService,
            UiComponents uiComponents,
            IComponentRendererFactory componentRendererFactory
            )
        {
            this.clientState = clientState;
            this.textureProvider = textureProvider;
            this.gearsetsService = gearsetsService;
            this.configurationService = configurationService;
            this.windowService = windowService;
            this.uiComponents = uiComponents;
            this.componentRendererFactory = componentRendererFactory;
            this.clientState.Login += handleLogin;
        }

        public void Dispose()
        {
            clientState.Login -= handleLogin;
        }

        public void SetTabState(TabState state)
        {
            if (state is not UserGearsetsTabState gearsetsState)
                throw new ArgumentException($"State must be type {nameof(UserGearsetsTabState)}");

            activeGearset = gearsetsState.activeGearset;
            nextActiveGearpiece = gearsetsState.activeGearpiece;
        }

        public void PreDraw()
        {
            removeDeletedGearset();
        }

        public void Draw()
        {
            if (clientState.IsLoggedIn)
            {
                if (firstLoggedInDrawCall && gearsetsService.CurrentGearsets.Count > 0)
                {
                    activeGearset ??= gearsetsService.CurrentGearsets[0];
                    firstLoggedInDrawCall = false;
                }

                DrawGearsetsNavigationPanel();

                ImGui.SameLine();
                
                DrawGearsetPanel();
            } else
            {
                DrawLoggedOut();
            }
        }

        private void removeDeletedGearset()
        {
            if (gearsetToDelete is not Gearset gearset)
                return;

            if (gearsetToDelete == activeGearset && gearsetsService.CurrentGearsets.Count > 1)
            {
                var deleteIdx = gearsetsService.CurrentGearsets.ToList().IndexOf(gearsetToDelete);
                var newIdx = deleteIdx >= gearsetsService.CurrentGearsets.Count - 1
                    ? deleteIdx - 1
                    : deleteIdx + 1;
                activeGearset = gearsetsService.CurrentGearsets.ElementAt(newIdx);
            }
            else
            {
                activeGearset = null;
            }
            gearsetsService.RemoveGearset(gearset);
            gearsetToDelete = null;
        }

        private void DrawGearsetsNavigationPanel()
        {
            var panelSize = new Vector2(230, 0);
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0)))
            using (ImRaii.Child("gearsets_selector", panelSize * ImGuiHelpers.GlobalScale))
            {
                // draw the list of gearsets to select from
                var panelSelectableSize = new Vector2(panelSize.X, 35) * ImGuiHelpers.GlobalScale;
                var availHeight = ImGui.GetContentRegionAvail().Y;
                var buttonSize = new Vector2(35, 25) * ImGuiHelpers.GlobalScale;
                var iconSize = new Vector2(25, 25) * ImGuiHelpers.GlobalScale;
                var iconYOffset = (panelSelectableSize.Y - iconSize.Y) / 2;
                var itemSpacing = new Vector2(10, 0) * ImGuiHelpers.GlobalScale;
                using (ImRaii.Child("gearsets_selector_panel", new Vector2(0, availHeight - buttonSize.Y), border: true))
                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, itemSpacing))
                {
                    if (gearsetsService.CurrentGearsets.Count == 0)
                    {
                        var verticalOffset = (availHeight - (buttonSize.Y + ImGui.GetTextLineHeight())) * 0.4f;
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + verticalOffset);
                        ImGuiHelpers.CenteredText(Resource.UserGearsetsTabNoGearsetsTextNavigation);
                    }
                    foreach (var gearset in gearsetsService.CurrentGearsets)
                    {
                        var cursorPos = ImGui.GetCursorPos();
                        var gearsetSelected = gearset == activeGearset;

                        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.6f, !gearset.IsActive))
                        {
                            var gearsetLabel = $"{gearset.Name}";
                            var gearsetNameWidth = ImGui.CalcTextSize(gearsetLabel).X;

                            var priorityText = $"[{gearset.Priority}]";
                            var priorityTextSize = ImGui.CalcTextSize(priorityText);
                            var elementsWidthWithPriority = gearsetNameWidth
                                + priorityTextSize.X
                                + iconSize.X
                                + (itemSpacing.X * 3);
                            // priority number
                            if (gearset.Priority != Constants.DefaultGearsetPriority
                                && elementsWidthWithPriority <= panelSelectableSize.X
                                )
                            {
                                var textOffsetY = (panelSelectableSize.Y - priorityTextSize.Y) / 2;
                                ImGui.SetCursorPos(new(ImGui.GetContentRegionMax().X - priorityTextSize.X, cursorPos.Y + textOffsetY));
                                ImGui.Text(priorityText);
                                if (ImGui.IsItemHovered())
                                    ImGui.SetTooltip(Resource.GearsetSortPriority);
                                ImGui.SameLine();
                            }

                            ImGui.SetCursorPos(cursorPos);

                            // gearset name
                            if (UiComponents.SelectableCentered(
                                $"{gearsetLabel}##{gearset.Id}",
                                centerY: true,
                                labelPosOffset: new(iconSize.X, 0),
                                labelPosOffsetScaled: new(5, 0),
                                selected: gearsetSelected,
                                size: panelSelectableSize
                                ))
                            {
                                if (activeGearset == gearset)
                                    activeGearset = null;
                                else
                                    activeGearset = gearset;
                            }
                        }

                        ImGui.SetCursorPos(new(cursorPos.X, cursorPos.Y + iconYOffset));

                        var classJobInfo = gearset.ClassJobInfo;
                        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, panelSelectableSize.Y - iconSize.Y)))
                            if (textureProvider.GetFromGameIcon(classJobInfo.IconId).TryGetWrap(out var texture, out var exception))
                            {
                                using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.8f, gearsetSelected))
                                    ImGui.Image(texture.ImGuiHandle, iconSize);
                                if (ImGui.IsItemHovered())
                                    ImGui.SetTooltip(classJobInfo.Name);
                            }
                        ImGui.SetCursorPos(new(cursorPos.X, cursorPos.Y + panelSelectableSize.Y));
                    }
                }

                // draw buttons to add, delete, or sort gearsets
                using (ImRaii.Child("gearsets_selector_buttons", new Vector2(0, buttonSize.Y)))
                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(3, 3)))
                {
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                    {
                        if (UiComponents.SelectableCentered(FontAwesomeIcon.Plus.ToIconString(), size: buttonSize, labelPosOffsetScaled: new(1.5f, -1)))
                            windowService.SetWindowState(WindowType.ImportGearset, open: true);

                        ImGui.SameLine();

                        var sortDirectionIconString = sortDescending
                            ? FontAwesomeIcon.SortAmountDown.ToIconString()
                            : FontAwesomeIcon.SortAmountUp.ToIconString();
                        if (UiComponents.SelectableCentered(label: sortDirectionIconString, size: buttonSize, labelPosOffsetScaled: new(0.5f, -0.5f)))
                        {
                            sortDescending = !sortDescending;
                            gearsetsService.ChangeGearsetSortOrder(activeSortType, sortDescending);
                        }
                    }
                    ImGui.SameLine();

                    var availWidth = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X;
                    var nextSortType = uiComponents.DrawCachedEnumSelectableDropdown(activeSortType, size: new(availWidth - buttonSize.X, buttonSize.Y));
                    if (nextSortType != activeSortType)
                    {
                        activeSortType = nextSortType;
                        gearsetsService.ChangeGearsetSortOrder(activeSortType, sortDescending);
                    }

                    ImGui.SameLine();
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                    using (ImRaii.Disabled(activeGearset == null))
                    using (ImRaii.PushColor(ImGuiCol.HeaderHovered, ImGui.GetColorU32(new Vector4(0.6f, 0.1f, 0.1f, 0.5f))))
                    using (ImRaii.PushColor(ImGuiCol.HeaderActive, ImGui.GetColorU32(new Vector4(0.6f, 0.1f, 0.1f, 1))))
                    {
                        if (UiComponents.SelectableCentered(FontAwesomeIcon.Trash.ToIconString(), size: buttonSize, labelPosOffsetScaled: new(0.25f, 0)))
                            gearsetToDelete = activeGearset;
                    }
                }

            }
        }

        private void DrawGearsetPanel()
        {
            if (activeGearset != null)
            {
                using (ImRaii.PushId(activeGearset.Id))
                {
                    componentRendererFactory
                        .GetComponentRenderer(activeGearset)
                        .Draw();
                }
            }
            else
            {
                using var _ = ImRaii.Child("gearset_view_panel", new Vector2(0, 0), border: true);
                ImGui.NewLine();
                ImGui.NewLine();
                ImGuiHelpers.CenteredText(Resource.UserGearsetsTabNoGearsetsTextPanel);
            }
        }

        /// <summary>
        /// Does not display gearsets when the user is logged out, shows information text informing user
        /// </summary>
        private void DrawLoggedOut()
        {
            ImGui.NewLine();
            ImGuiHelpers.CenteredText(Resource.GearsetsTabLoggedOutText);
        }

        private void handleLogin() =>
            firstLoggedInDrawCall = true;
    }
}
