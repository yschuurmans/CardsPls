using CardsPls.Managers;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CardsPls.GUI
{
    public class Interface : IDisposable
    {
        public const string PluginName = "CardsPls";

        private readonly string _configHeader;
        private readonly CardsPls _plugin;

        private string _statusFilter = string.Empty;
        private readonly HashSet<string> _seenNames;

        public bool Visible;

        public bool TestMode = false;

        private static void ChangeAndSave<T>(T value, T currentValue, Action<T> setter) where T : IEquatable<T>
        {
            if (value.Equals(currentValue))
                return;

            setter(value);
            CardsPls.Config.Save();
        }

        public Interface(CardsPls plugin)
        {
            _plugin = plugin;
            _configHeader = CardsPls.Version.Length > 0 ? $"{PluginName} v{CardsPls.Version}###{PluginName}" : PluginName;
            _seenNames = new HashSet<string>(_plugin.StatusSet.DisabledStatusSet.Count + _plugin.StatusSet.EnabledStatusSet.Count);

            Dalamud.PluginInterface.UiBuilder.Draw += Draw;
            Dalamud.PluginInterface.UiBuilder.OpenConfigUi += Enable;
        }

        private static void DrawCheckbox(string name, string tooltip, bool value, Action<bool> setter)
        {
            var tmp = value;
            if (ImGui.Checkbox(name, ref tmp))
                ChangeAndSave(tmp, value, setter);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
        }

        private void DrawEnabledCheckbox()
            => DrawCheckbox("Enabled", "Enable or disable the plugin.", CardsPls.Config.Enabled, e =>
            {
                CardsPls.Config.Enabled = e;
                if (e)
                    _plugin.Enable();
                else
                    _plugin.Disable();
            });

        private void DrawHideSymbolsOnSelfCheckbox()
            => DrawCheckbox("Hide Symbols on Self", "Hide the symbol and/or text drawn into the world on the player character.",
                CardsPls.Config.HideSymbolsOnSelf, e => CardsPls.Config.HideSymbolsOnSelf = e);


        private void DrawShowGroupCheckbox()
            => DrawCheckbox("Highlight in Party Frames",
                "Highlights players in your party frames according to your color and state selection.",
                CardsPls.Config.ShowGroupFrame,
                e => CardsPls.Config.ShowGroupFrame = e);

        private void DrawShowAllianceCheckbox()
            => DrawCheckbox("Highlight in Alliance Frames",
                "Highlights players in your alliance frames according to your color and state selection.",
                CardsPls.Config.ShowAllianceFrame,
                e => CardsPls.Config.ShowAllianceFrame = e);

        private void DrawRestrictJobsCheckbox()
            => DrawCheckbox("Restrict to Astrologian",
                "Only display the card information when you are the job with cards.", CardsPls.Config.RestrictedJobs,
                e => CardsPls.Config.RestrictedJobs = e);

        private void ShowCardIconInWorld()
            => DrawCheckbox("Show Card icon in world",
                "Show card icon in the overworld under the player with the active \"Has Card\" effects.", CardsPls.Config.ShowIconCard,
                e => CardsPls.Config.ShowIconCard = e);
        private void ShowCardTextInWorld()
            => DrawCheckbox("Show Card text in world",
                "Show card text in the overworld under the player with the active \"Has Card\" effects.", CardsPls.Config.ShowInWorldTextCard,
                e => CardsPls.Config.ShowInWorldTextCard = e);

        private void DrawTestModeCheckBox1()
            => DrawCheckbox("Test Player Card", "Should show the active \"Has Card\" effects on the player character and party frames.",
                ActorWatcher.TestMode == 1, e => ActorWatcher.TestMode = e ? 1 : 0);


        private void DrawSingleStatusEffectList(string header, bool which, float width)
        {
            using var group = ImGuiRaii.NewGroup();
            var list = which ? _plugin.StatusSet.DisabledStatusSet : _plugin.StatusSet.EnabledStatusSet;
            _seenNames.Clear();
            if (ImGui.BeginListBox($"##{header}box", width / 2 * Vector2.UnitX))
            {
                for (var i = 0; i < list.Count; ++i)
                {
                    var (status, name) = list[i];
                    if (!name.Contains(_statusFilter) || _seenNames.Contains(name))
                        continue;

                    _seenNames.Add(name);
                    if (ImGui.Selectable($"{status.Name}##status{status.RowId}"))
                    {
                        _plugin.StatusSet.Swap((ushort)status.RowId);
                        --i;
                    }
                }

                ImGui.EndListBox();
            }

            if (which)
            {
                if (ImGui.Button("Disable All Statuses", width / 2 * Vector2.UnitX))
                    _plugin.StatusSet.ClearEnabledList();
            }
            else if (ImGui.Button("Enable All Statuses", width / 2 * Vector2.UnitX))
            {
                _plugin.StatusSet.ClearDisabledList();
            }
        }

        private static void DrawStatusSelectorTitles(float width)
        {
            const string disabledHeader = "Disabled Statuses";
            const string enabledHeader = "Monitored Statuses";
            var pos1 = width / 4 - ImGui.CalcTextSize(disabledHeader).X / 2;
            var pos2 = 3 * width / 4 + ImGui.GetStyle().ItemSpacing.X - ImGui.CalcTextSize(enabledHeader).X / 2;
            ImGui.SetCursorPosX(pos1);
            ImGui.AlignTextToFramePadding();
            ImGui.Text(disabledHeader);
            ImGui.SameLine(pos2);
            ImGui.AlignTextToFramePadding();
            ImGui.Text(enabledHeader);
        }

        private void DrawColorPicker(string name, string tooltip, uint value, uint defaultValue, Action<uint> setter)
        {
            const ImGuiColorEditFlags flags = ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.NoInputs;

            var tmp = ImGui.ColorConvertU32ToFloat4(value);
            if (ImGui.ColorEdit4($"##{name}", ref tmp, flags))
                ChangeAndSave(ImGui.ColorConvertFloat4ToU32(tmp), value, setter);
            ImGui.SameLine();
            if (ImGui.Button($"Default##{name}"))
                ChangeAndSave(defaultValue, value, setter);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    $"Reset to default: #{defaultValue & 0xFF:X2}{(defaultValue >> 8) & 0xFF:X2}{(defaultValue >> 16) & 0xFF:X2}{defaultValue >> 24:X2}");
            ImGui.SameLine();
            ImGui.Text(name);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
        }

        private void DrawInWorldBackgroundColorPicker()
            => DrawColorPicker("In World Background",
                "The background color for text that is drawn into the world on corpses for raises.",
                CardsPls.Config.InWorldBackgroundColor, CardsPlsConfig.DefaultInWorldBackgroundColorCard,
                c => CardsPls.Config.InWorldBackgroundColor = c);

        private void DrawCardColorPicker()
            => DrawColorPicker("Has Card Status Effect",
                "The highlight color for a player that has any monitored detrimental status effect.",
                CardsPls.Config.DispellableColor, CardsPlsConfig.DefaultCardColor, c => CardsPls.Config.DispellableColor = c);

        private void DrawScaleButton()
        {
            const float min = 0.1f;
            const float max = 3.0f;
            const float step = 0.005f;

            var tmp = CardsPls.Config.IconScale;
            if (ImGui.DragFloat("In World Icon Scale", ref tmp, step, min, max))
                ChangeAndSave(tmp, CardsPls.Config.IconScale, f => CardsPls.Config.IconScale = Math.Max(min, Math.Min(f, max)));
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Set the scale of the Raised icon that is drawn into the world on raised corpses.");
        }

        private static readonly string[] RectTypeStrings = new[]
        {
            "Fill",
            "Only Outline",
            "Only Full Alpha Outline",
            "Fill and Full Alpha Outline",
        };

        private void DrawRectTypeSelector()
        {
            var type = (int)CardsPls.Config.RectType;
            if (!ImGui.Combo("Rectangle Type", ref type, RectTypeStrings, RectTypeStrings.Length))
                return;

            ChangeAndSave(type, (int)CardsPls.Config.RectType, t => CardsPls.Config.RectType = (RectType)t);
        }

        public void Draw()
        {
            if (!Visible)
                return;

            var buttonHeight = ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2;
            var horizontalSpacing = new Vector2(0, ImGui.GetTextLineHeightWithSpacing());

            var height = 15 * buttonHeight
              + 6 * horizontalSpacing.Y
              + 27 * ImGui.GetStyle().ItemSpacing.Y;
            var width = 450 * ImGui.GetIO().FontGlobalScale;
            var constraints = new Vector2(width, height);
            ImGui.SetNextWindowSizeConstraints(constraints, constraints);

            if (!ImGui.Begin(_configHeader, ref Visible, ImGuiWindowFlags.NoResize))
                return;

            try
            {
                DrawEnabledCheckbox();

                if (ImGui.CollapsingHeader("General Settings"))
                {
                    DrawRestrictJobsCheckbox();

                    DrawHideSymbolsOnSelfCheckbox();
                    DrawShowGroupCheckbox();
                    DrawShowAllianceCheckbox();
                    ShowCardIconInWorld();
                    ShowCardTextInWorld();
                    DrawRectTypeSelector();
                    DrawScaleButton();
                    ImGui.Dummy(horizontalSpacing);
                }

                if (ImGui.CollapsingHeader("Colors"))
                {
                    DrawCardColorPicker();
                    DrawInWorldBackgroundColorPicker();
                    ImGui.Dummy(horizontalSpacing);
                }

                if (ImGui.CollapsingHeader("Testing"))
                {
                    DrawTestModeCheckBox1();
                }
            }
            finally
            {
                ImGui.End();
            }
        }

        public void Enable()
            => Visible = true;

        public void Dispose()
        {
            Dalamud.PluginInterface.UiBuilder.Draw -= Draw;
            Dalamud.PluginInterface.UiBuilder.OpenConfigUi -= Enable;
        }
    }
}
