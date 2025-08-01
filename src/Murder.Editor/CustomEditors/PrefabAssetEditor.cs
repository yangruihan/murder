﻿using Bang;
using Bang.Components;
using ImGuiNET;
using Murder.Assets;
using Murder.Core.Graphics;
using Murder.Diagnostics;
using Murder.Editor;
using Murder.Editor.Attributes;
using Murder.Editor.CustomComponents;
using Murder.Editor.CustomDiagnostics;
using Murder.Editor.ImGuiExtended;
using Murder.Editor.Reflection;
using Murder.Editor.Stages;
using Murder.Editor.Utilities;
using Murder.Prefabs;
using System.Collections.Immutable;

namespace Murder.Editor.CustomEditors
{
    [CustomEditorOf(typeof(PrefabAsset))]
    internal class PrefabAssetEditor : AssetEditor
    {
        protected override void OnSwitchAsset(ImGuiRenderer imGuiRenderer, RenderContext renderContext, bool forceInit)
        {
            if (_asset is null)
            {
                return;
            }

            if (forceInit || !Stages.ContainsKey(_asset.Guid))
            {
                Stage stage = new(imGuiRenderer, renderContext, Stage.StageType.EnableSelectChildren, _asset.Guid);

                int entity = stage.AddEntity((PrefabAsset)_asset);
                stage.SelectEntity(entity, true, clear: true);
                stage.EditorHook.EditorMode = EditorHook.EditorModes.EditMode;
                stage.EditorHook.CanSwitchModes = false;

                InitializeStage(stage, _asset.Guid);

                stage.ActivateSystemsWith(enable: true, typeof(PrefabEditorAttribute));
            }

            _lastOpenedEntity = _asset as IEntity;
        }

        private IEntity? _lastOpenedEntity = null;

        protected override IEntity? SelectedEntityImpl => _lastOpenedEntity;

        public override void UpdateEditor()
        {
            if (_asset is null || !Stages.ContainsKey(_asset!.Guid))
            {
                return;
            }

            Stages[_asset.Guid].Update();
        }

        public override void DrawEditor()
        {
            GameLogger.Verify(Stages is not null);
            GameLogger.Verify(_asset is not null);

            if (ImGui.BeginTable("prefab_table", 2, ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("a", ImGuiTableColumnFlags.WidthFixed, 480, 0);
                ImGui.TableSetupColumn("b", ImGuiTableColumnFlags.WidthStretch, -1f, 1);

                if (ImGui.TableNextColumn())
                {
                    ImGui.BeginChild(id: 12, ImGui.GetContentRegionAvail() - ImGui.GetStyle().FramePadding, ImGuiChildFlags.None, ImGuiWindowFlags.NoDecoration);
                    DrawSelectorPicker();
                    DrawEntity((IEntity)_asset, _asset is not PrefabAsset);
                    DrawDimensions();

                    ImGui.EndChild();
                }

                if (ImGui.TableNextColumn())
                {
                    if (Stages.ContainsKey(_asset.Guid))
                    {
                        Stages[_asset.Guid].EditorHook.DrawSelection = false;

                        Stages[_asset.Guid].Draw();
                        Stages[_asset.Guid].PersistInfo(_asset.Guid);
                    }
                }

                ImGui.EndTable();
            }
        }

        private static readonly Lazy<EditorMember> _dimensionsField =
            new(() => typeof(PrefabAsset).TryGetFieldForEditor(nameof(PrefabAsset.Dimensions))!);

        private void DrawSelectorPicker()
        {
            if (_asset is PrefabAsset prefab)
            {
                ImGui.SameLine();
                if (ImGuiHelpers.ColoredIconButton('\uf1fb', $"#show_{prefab.Guid}", prefab.ShowOnPrefabSelector))
                {
                    prefab.ShowOnPrefabSelector = !prefab.ShowOnPrefabSelector;
                    prefab.FileChanged = true;
                }

                ImGuiHelpers.HelpTooltip("Show this entity on the world entity picker");
            }
        }

        private void DrawDimensions()
        {
            GameLogger.Verify(_asset is not null);
            GameLogger.Verify(Stages is not null);

            using RectangleBox box = new(color: Game.Profile.Theme.Accent);
            ImGuiHelpers.ColorIcon('\uf545', Game.Profile.Theme.Accent);

            ImGui.PushStyleColor(ImGuiCol.Text, Game.Profile.Theme.Accent);

            PrefabAsset prefab = (PrefabAsset)_asset;
            if (ImGui.TreeNodeEx("Dimensions"))
            {
                object copy = prefab.Dimensions;
                if (CustomComponent.ProcessInput(prefab, _dimensionsField.Value, () => (CustomComponent.ShowEditorOf(copy), copy)))
                {
                    prefab.FileChanged = true;
                }

                Stages[_asset.Guid].AddDimension(prefab.Guid, prefab.Dimensions);

                ImGui.TreePop();
            }
            else if (Stages.ContainsKey(_asset.Guid))
            {
                Stages[_asset.Guid].ClearDimension(prefab.Guid);
            }

            ImGui.PopStyleColor();
        }

        public override bool RunDiagnostics(Guid guid)
        {
            PrefabAsset? asset = Game.Data.TryGetAsset<PrefabAsset>(guid);
            if (asset is null)
            {
                GameLogger.Warning($"Unable to retrieve asset {guid}.");
                return false;
            }

            bool isValid = true;

            foreach (IComponent c in asset.Components)
            {
                isValid |= CustomDiagnostic.ScanAllMembers(asset.GetSimplifiedName(), c, outputResult: true);
            }

            return isValid;
        }
    }
}