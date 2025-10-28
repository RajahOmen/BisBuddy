using BisBuddy.Gear;
using BisBuddy.Mediators;
using BisBuddy.Resources;
using BisBuddy.Services;
using BisBuddy.Services.Configuration;
using BisBuddy.Services.Gearsets;
using BisBuddy.Ui.Renderers.Components;
using BisBuddy.Ui.Windows;
using BisBuddy.Util;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using System;
using System.Linq;
using System.Numerics;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Main
{
    public class UserGearsetsTab : TabRenderer<MainWindowTab>, IDisposable
    {
        private readonly ITypedLogger<UserGearsetsTab> logger;
        private readonly IClientState clientState;
        private readonly ITextureProvider textureProvider;
        private readonly IGearsetsService gearsetsService;
        private readonly IConfigurationService configurationService;
        private readonly IWindowService windowService;
        private readonly UiComponents uiComponents;
        private readonly IRendererFactory rendererFactory;
        private readonly IInventoryUpdateDisplayService inventoryUpdate;
        private readonly IDebugService debugService;

        private Gearset? activeGearset = null;
        private Gearpiece? nextActiveGearpiece = null;
        private Gearset? gearsetToDelete = null;
        private GearsetSortType activeSortType = GearsetSortType.ImportDate;
        private bool sortDescending = false;
        private bool firstLoggedInDrawCall = true;
        private UiTheme uiTheme => configurationService.UiTheme;

        public WindowSizeConstraints? TabSizeConstraints => new()
        {
            MinimumSize = new(500, 150),
            MaximumSize = new(0, 0)
        };

        public bool ShouldDraw => true;

        public UserGearsetsTab(
            ITypedLogger<UserGearsetsTab> logger,
            IClientState clientState,
            ITextureProvider textureProvider,
            IGearsetsService gearsetsService,
            IConfigurationService configurationService,
            IWindowService windowService,
            UiComponents uiComponents,
            IRendererFactory rendererFactory,
            IInventoryUpdateDisplayService inventoryUpdate,
            IDebugService debugService
            )
        {
            this.logger = logger;
            this.clientState = clientState;
            this.textureProvider = textureProvider;
            this.gearsetsService = gearsetsService;
            this.configurationService = configurationService;
            this.windowService = windowService;
            this.uiComponents = uiComponents;
            this.rendererFactory = rendererFactory;
            this.inventoryUpdate = inventoryUpdate;
            this.debugService = debugService;
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
            debugService.AssertMainThreadDebug();

            if (!clientState.IsLoggedIn)
            {
                DrawLoggedOut();
                return;
            }

            if (firstLoggedInDrawCall && gearsetsService.CurrentGearsets.Count > 0)
            {
                activeGearset ??= gearsetsService.CurrentGearsets[0];
                firstLoggedInDrawCall = false;
            }

            var tableFlags =
                ImGuiTableFlags.BordersInnerV
                | ImGuiTableFlags.BordersOuter;

            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(0f, 0f));
            using var table = ImRaii.Table("gearset_table", 2, tableFlags);
            ImGui.PopStyleVar();
            if (!table)
                return;

            ImGui.TableSetupColumn("###navigation", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("###gearset_details", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            UiComponents.PushTableClipRect();
            try
            {
                DrawGearsetsNavigationPanel();
            }
            finally
            {
                ImGui.PopClipRect();
            }

            ImGui.TableNextColumn();

            UiComponents.PushTableClipRect();
            try
            {
                DrawGearsetPanel();
            }
            finally
            {
                ImGui.PopClipRect();
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
            var panelSize = new Vector2(200, 0);
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0)))
            using (ImRaii.Child("gearsets_selector", panelSize * ImGuiHelpers.GlobalScale))
            {
                // draw the list of gearsets to select from
                var panelSelectableSize = new Vector2(panelSize.X, 35) * ImGuiHelpers.GlobalScale;
                var availHeight = ImGui.GetContentRegionAvail().Y;
                var buttonSize = new Vector2(30, 24) * ImGuiHelpers.GlobalScale;
                var iconSize = new Vector2(28, 28) * ImGuiHelpers.GlobalScale;
                var iconYOffset = (panelSelectableSize.Y - iconSize.Y) / 2;
                var itemSpacing = new Vector2(10, 0) * ImGuiHelpers.GlobalScale;
                var padding = new Vector2(4f, 3f);
                var iconXOffset = iconYOffset - padding.X / 2;
                var gradientSize = new Vector2(panelSelectableSize.X * (2f / 3f), panelSelectableSize.Y);

                var gradScrenPosX = ImGui.GetCursorScreenPos().X;

                using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, padding))
                using (ImRaii.Child("gearsets_selector_panel", new Vector2(0, availHeight - buttonSize.Y), border: false, ImGuiWindowFlags.AlwaysUseWindowPadding))
                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, itemSpacing))
                {
                    if (gearsetsService.CurrentGearsets.Count == 0)
                    {
                        var verticalOffset = (availHeight - (buttonSize.Y + ImGui.GetTextLineHeight())) * 0.4f;
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + verticalOffset);
                        ImGuiHelpers.CenteredText(Resource.UserGearsetsTabNoGearsetsTextNavigation);
                    }

                    var gearsetsToDraw = gearsetsService.CurrentGearsets.ToList();
                    foreach (var gearset in gearsetsToDraw)
                    {
                        using var _ = ImRaii.PushId(gearset.Id);
                        var cursorPos = ImGui.GetCursorPos();
                        var cursorScreenPos = ImGui.GetCursorScreenPos();

                        var gearsetSelected = gearset == activeGearset;
                        var mainSelectableHovered = false;

                        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.6f, !gearset.IsActive))
                        {
                            var gearsetLabel = $"{gearset.Name}";
                            var gearsetNameSize = ImGui.CalcTextSize(gearsetLabel);

                            var rightEdgePadding = padding.X;
                            var drawingPriority = gearset.Priority != Constants.DefaultGearsetPriority;
                            var availRegion = ImGui.GetContentRegionAvail();
                            if (drawingPriority)
                            {
                                var priorityText = $"[{gearset.Priority}]";
                                var priorityTextSize = ImGui.CalcTextSize(priorityText);
                                rightEdgePadding += priorityTextSize.X;
                                var textOffsetY = (panelSelectableSize.Y - priorityTextSize.Y) / 2;
                                ImGui.SetCursorPos(new(availRegion.X + padding.X - priorityTextSize.X, cursorPos.Y + textOffsetY));
                                using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.6f))
                                    ImGui.Text(priorityText);
                                if (ImGui.IsItemHovered())
                                    ImGui.SetTooltip(Resource.GearsetSortPriority);
                            }

                            ImGui.SetCursorPos(cursorPos);

                            if (ImGui.Selectable("###gearset_selectable", gearsetSelected, ImGuiSelectableFlags.None, panelSelectableSize))
                                activeGearset = activeGearset != gearset
                                    ? gearset
                                    : null;
                            mainSelectableHovered = ImGui.IsItemHovered();
                            // right click context menu
                            rendererFactory
                                .GetRenderer(gearset, RendererType.ContextMenu)
                                .Draw();

                            if (uiTheme.ShowGearsetColorAccentFlag && gearset.IsActive)
                            {
                                var gradientPosTopLeft = new Vector2(gradScrenPosX, cursorScreenPos.Y);
                                var gradientPosBotRight = gradientPosTopLeft + gradientSize;
                                var drawList = ImGui.GetWindowDrawList();
                                var highlightColor = gearset.HighlightColor ?? configurationService.DefaultHighlightColor;
                                var color = highlightColor.BaseColor;
                                if (gearsetSelected)
                                    color.W *= 0.5f;
                                var col = ImGui.GetColorU32(color);
                                var colNone = ImGui.GetColorU32(Vector4.Zero);
                                drawList.AddRectFilledMultiColor(
                                    gradientPosTopLeft, gradientPosBotRight, col, 0, 0, col
                                    );
                            }

                            var textPosOffset = new Vector2(
                                x: iconSize.X + iconXOffset + 5f * ImGuiHelpers.GlobalScale,
                                y: (panelSelectableSize.Y - gearsetNameSize.Y) / 2
                                );

                            ImGui.SetCursorPos(cursorPos + textPosOffset);

                            var botRight = cursorScreenPos + new Vector2(availRegion.X, panelSelectableSize.Y);
                            botRight.X -= rightEdgePadding;
                            ImGui.PushClipRect(cursorScreenPos, botRight, true);
                            try
                            {
                                ImGui.Text(gearsetLabel);
                            }
                            finally
                            {
                                ImGui.PopClipRect();
                            }
                        }

                        ImGui.SetCursorPos(new(cursorPos.X + iconXOffset, cursorPos.Y + iconYOffset));

                        var classJobInfo = gearset.ClassJobInfo;
                        debugService.AssertMainThreadDebug();
                        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, panelSelectableSize.Y - iconSize.Y)))
                            if (textureProvider.GetFromGameIcon(classJobInfo.IconIdFramed).TryGetWrap(out var texture, out var exception))
                            {
                                using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.8f, gearsetSelected || mainSelectableHovered))
                                using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 0.6f, !gearset.IsActive))
                                    ImGui.Image(texture.Handle, iconSize);
                                if (ImGui.IsItemHovered())
                                    ImGui.SetTooltip(classJobInfo.Name);
                            }
                        ImGui.SetCursorPos(new(cursorPos.X, cursorPos.Y + panelSelectableSize.Y));
                    }
                }

                ImGui.Separator();
                // draw buttons to add, delete, or sort gearsets
                var oldWindowPadding = ImGui.GetStyle().WindowPadding;
                using (ImRaii.PushStyle(ImGuiStyleVar.CellPadding, Vector2.Zero))
                using (var table = ImRaii.Table("gearsets_selector_buttons_table", 5))
                {
                    if (!table)
                        return;

                    ImGui.TableSetupColumn("###add_gearset", ImGuiTableColumnFlags.WidthFixed, buttonSize.X);
                    ImGui.TableSetupColumn("###sync_inventory", ImGuiTableColumnFlags.WidthFixed, buttonSize.X);
                    ImGui.TableSetupColumn("###sort_direction", ImGuiTableColumnFlags.WidthFixed, buttonSize.X);
                    ImGui.TableSetupColumn("###sort_type", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("###delete_gearset", ImGuiTableColumnFlags.WidthFixed, buttonSize.X);

                    ImGui.TableNextColumn();

                    // ADD GEARSET
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                        if (UiComponents.SelectableCentered(FontAwesomeIcon.Plus.ToIconString(), size: buttonSize, labelPosOffsetScaled: new(1.5f, -1)))
                            windowService.SetWindowOpenState(WindowType.ImportGearset, open: true);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Resource.NewGearsetTooltip);

                    ImGui.TableNextColumn();

                    // SYNC INVENTORY
                    using (ImRaii.Disabled(inventoryUpdate.UpdateIsQueued))
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                        if (UiComponents.SelectableCentered(FontAwesomeIcon.Sync.ToIconString(), size: buttonSize, labelPosOffsetScaled: new(1.5f, -1)))
                            gearsetsService.QueueUpdateFromInventory(saveChanges: true, manualUpdate: true);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Resource.SyncInventoryTooltip);

                    ImGui.TableNextColumn();

                    // SORT DIRECTION
                    var sortDirectionIconString = sortDescending
                        ? FontAwesomeIcon.SortAmountDown.ToIconString()
                        : FontAwesomeIcon.SortAmountUp.ToIconString();
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                        if (UiComponents.SelectableCentered(label: sortDirectionIconString, size: buttonSize, labelPosOffsetScaled: new(0.5f, -0.5f)))
                        {
                            sortDescending = !sortDescending;
                            gearsetsService.ChangeGearsetSortOrder(activeSortType, sortDescending);
                        }

                    ImGui.TableNextColumn();

                    // SORT KIND
                    var availWidth = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X;
                    if (uiComponents.DrawCachedEnumSelectableDropdown(activeSortType, out var newSortType, size: new(0, buttonSize.Y)))
                    {
                        activeSortType = newSortType;
                        gearsetsService.ChangeGearsetSortOrder(activeSortType, sortDescending);
                    }

                    ImGui.TableNextColumn();

                    // DELETE GEARSET
                    var canDelete = activeGearset != null && ImGui.IsKeyDown(ImGuiKey.LeftShift);
                    using (ImRaii.Disabled(!canDelete))
                    using (ImRaii.PushColor(ImGuiCol.HeaderHovered, ImGui.GetColorU32(new Vector4(0.6f, 0.1f, 0.1f, 0.25f))))
                    using (ImRaii.PushColor(ImGuiCol.HeaderActive, uiTheme.DeleteColor))
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                        if (UiComponents.SelectableCentered(FontAwesomeIcon.Trash.ToIconString(), size: buttonSize, labelPosOffsetScaled: new(0.05f, -0.05f)))
                            gearsetToDelete = activeGearset;
                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        var gearsetName = activeGearset != null
                            ? $"\"{activeGearset.Name}\""
                            : Resource.DeleteGearsetTooltipGearsetNameDummy;
                        if (canDelete)
                        {
                            ImGui.SetTooltip(Resource.DeleteGearsetTooltip.Format(gearsetName));
                        }
                        else
                        {
                            ImGui.SetTooltip(Resource.DeleteGearsetDisabledTooltip.Format(gearsetName));
                        }
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
                    rendererFactory
                        .GetRenderer(activeGearset, RendererType.Component)
                        .Draw();
                }
            }
            else
            {
                using var _ = ImRaii.Child("gearset_view_panel", new Vector2(0, 0), border: false);
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
            using (ImRaii.Child("logged_out_gearsets_panel", ImGui.GetContentRegionAvail(), border: true))
            {
                ImGui.NewLine();
                ImGuiHelpers.CenteredText(Resource.GearsetsTabLoggedOutText);
            }
        }

        private void handleLogin() =>
            firstLoggedInDrawCall = true;
    }
}
