﻿using CardsPls.Enums;
using CardsPls.Managers;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Data.Files;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;

namespace CardsPls.GUI
{
    public class Overlay : IDisposable
    {
        private readonly HudManager _hudManager;
        private readonly ActorWatcher _actorWatcher;

        private IReadOnlyDictionary<uint, ActorState> CardList
            => _actorWatcher.CardList;

        private IReadOnlyDictionary<uint, string> Names
            => _actorWatcher.ActorNames;

        private IReadOnlyDictionary<uint, Vector3> Positions
            => _actorWatcher.ActorPositions;

        private (uint, ActorState) PlayerRez
            => _actorWatcher.PlayerRez;

        private bool _enabled;

        public void Enable()
        {
            if (_enabled)
                return;

            _enabled = true;
            Dalamud.PluginInterface.UiBuilder.Draw += Draw;
        }

        public void Disable()
        {
            if (!_enabled)
                return;

            _enabled = false;
            Dalamud.PluginInterface.UiBuilder.Draw -= Draw;
        }

        private readonly IDalamudTextureWrap? _cardIcon;
        private static readonly Vector4 BlackColor = new(0, 0, 0, 1);
        private static readonly Vector4 WhiteColor = new(1, 1, 1, 1);

        private bool _drawCards = true;

        private static TexFile? GetHdIcon(uint id)
        {
            var path = $"ui/icon/{id / 1000 * 1000:000000}/{id:000000}_hr1.tex";
            return Dalamud.GameData.GetFile<TexFile>(path);
        }

        private static IDalamudTextureWrap? BuildCardIcon()
        {
            const int raiseIconId = 003102;
            return Dalamud.Textures.GetIcon(raiseIconId);
        }

        private static readonly Vector2 DefaultIconSize = new(48, 64);

        public Overlay(ActorWatcher actorWatcher)
        {
            _actorWatcher = actorWatcher;
            _hudManager = new HudManager();
            _cardIcon = BuildCardIcon();
        }

        public void Dispose()
        {
            Disable();
            _hudManager.Dispose();
            _cardIcon?.Dispose();
        }

        private (string, bool, bool) GetText(string name, ActorState state)
        {
            if (state.HasCard)
                return _drawCards
                    ? ("Has Card", CardsPls.Config.ShowIconCard, CardsPls.Config.ShowInWorldTextCard)
                    : ("", false, false);

            return ("", false, false);
        }

        public void DrawInWorld(Vector2 pos, string name, ActorState state)
        {
            var (text, drawIcon, drawText) = GetText(name, state);

            if (drawIcon)
            {
                var icon = _cardIcon;
                if (icon == null)
                    return;

                var scaledIconSize = DefaultIconSize * CardsPls.Config.IconScale * ImGui.GetIO().FontGlobalScale;

                ImGui.SetCursorPos(new Vector2(pos.X - scaledIconSize.X / 2f, pos.Y - scaledIconSize.Y) - ImGui.GetMainViewport().Pos);
                ImGui.Image(icon.ImGuiHandle, scaledIconSize);
            }

            if (drawText)
            {
                var color = CardsPls.Config.InWorldBackgroundColor;
                var textSize = ImGui.CalcTextSize(text);
                ImGui.SetCursorPos(new Vector2(pos.X, pos.Y)
                  - new Vector2(textSize.X / 2 + ImGui.GetStyle().FramePadding.X, 0)
                  - ImGui.GetMainViewport().Pos);
                using var imgui = new ImGuiRaii()
                    .PushColor(ImGuiCol.Button, color);
                ImGui.Button(text);
            }
        }

        private static ImDrawListPtr? BeginRezRects()
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
              | ImGuiWindowFlags.NoSavedSettings
              | ImGuiWindowFlags.NoMove
              | ImGuiWindowFlags.NoMouseInputs
              | ImGuiWindowFlags.NoFocusOnAppearing
              | ImGuiWindowFlags.NoBackground
              | ImGuiWindowFlags.NoNav;

            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos);
            ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);
            return !ImGui.Begin("##rezRects", flags) ? null : ImGui.GetWindowDrawList();
        }

        private static unsafe Vector2 GetNodePosition(AtkResNode* node)
        {
            var pos = new Vector2(node->X, node->Y);
            var par = node->ParentNode;
            while (par != null)
            {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += new Vector2(par->X, par->Y);
                par = par->ParentNode;
            }

            return pos;
        }

        private static void TextShadowed(string text, Vector4 foregroundColor, Vector4 shadowColor, byte shadowWidth = 1)
        {
            var x = ImGui.GetCursorPosX();
            var y = ImGui.GetCursorPosY();

            for (var i = -shadowWidth; i <= shadowWidth; i++)
            {
                for (var j = -shadowWidth; j <= shadowWidth; j++)
                {
                    if (i == 0 && j == 0)
                        continue;

                    ImGui.SetCursorPosX(x + i);
                    ImGui.SetCursorPosY(y + j);
                    ImGui.TextColored(shadowColor, text);
                }
            }

            ImGui.SetCursorPosX(x);
            ImGui.SetCursorPosY(y);
            ImGui.TextColored(foregroundColor, text);
        }

        private static readonly Vector2 GreenBoxPositionOffset = new(5, 5);
        private static readonly Vector2 GreenBoxSizeOffset = new(8, 10);
        private const float GreenBoxEdgeRounding = 10;

        private static void DrawRect(ImDrawListPtr drawPtr, Vector2 rectMin, Vector2 rectMax, uint color, float rounding, RectType type)
        {
            rectMin += ImGui.GetMainViewport().Pos;
            rectMax += ImGui.GetMainViewport().Pos;
            switch (type)
            {
                case RectType.Fill:
                    drawPtr.AddRectFilled(rectMin, rectMax, color, rounding);
                    break;
                case RectType.OnlyOutline:
                    drawPtr.AddRect(rectMin, rectMax, color, rounding);
                    break;
                case RectType.OnlyFullAlphaOutline:
                    color |= 0xFF000000;
                    drawPtr.AddRect(rectMin, rectMax, color, rounding);
                    break;
                case RectType.FillAndFullAlphaOutline:
                    drawPtr.AddRectFilled(rectMin, rectMax, color, rounding);
                    color |= 0xFF000000;
                    drawPtr.AddRect(rectMin, rectMax, color, rounding);
                    break;
                default: throw new InvalidEnumArgumentException();
            }
        }

        private static unsafe void DrawPartyRect(ImDrawListPtr drawPtr, AtkUnitBase* partyList, int idx, uint color, RectType type, bool names,
            string caster = "")
        {
            idx = 22 - idx;
            var nodePtr = (AtkComponentNode*)partyList->UldManager.NodeList[idx];
            var colNode = nodePtr->Component->UldManager.NodeList[2];
            var rectMin = GetNodePosition(colNode) + GreenBoxPositionOffset * partyList->Scale;
            var rectSize = (new Vector2(colNode->Width, colNode->Height) - GreenBoxSizeOffset) * partyList->Scale;

            DrawRect(drawPtr, rectMin, rectMin + rectSize, color, GreenBoxEdgeRounding * partyList->Scale, type);
            if (!names || caster.Length <= 0)
                return;

            ImGui.SetCursorPosY(rectMin.Y + (rectSize.Y - ImGui.GetTextLineHeight()) / 2);
            ImGui.SetCursorPosX(rectMin.X + rectSize.X - 5 - ImGui.CalcTextSize(caster).X);
            TextShadowed(caster, WhiteColor, BlackColor, 2);
        }

        private static unsafe void DrawAllianceRect(ImDrawListPtr drawPtr, AtkUnitBase* allianceList, int idx, uint color, RectType type,
            bool names,
            string caster = "")
        {
            idx = 9 - idx;
            var nodePtr = allianceList->UldManager.NodeList[idx];
            var comp = ((AtkComponentNode*)nodePtr)->Component;
            var gridNode = comp->UldManager.NodeList[2]->ChildNode;
            var rectMin = GetNodePosition(gridNode) + GreenBoxPositionOffset * allianceList->Scale;
            var rectSize = (new Vector2(gridNode->Width, gridNode->Height) - GreenBoxSizeOffset) * allianceList->Scale;

            DrawRect(drawPtr, rectMin, rectMin + rectSize, color, GreenBoxEdgeRounding * allianceList->Scale, type);
            if (!names || caster.Length <= 0)
                return;

            ImGui.SetCursorPosY(rectMin.Y + (rectSize.Y - ImGui.GetTextLineHeight()) / 2);
            ImGui.SetCursorPosX(rectMin.X + rectSize.X - 5 - ImGui.CalcTextSize(caster).X);
            TextShadowed(caster, WhiteColor, BlackColor);
        }


        private string GetActorName(uint corpse, uint caster)
        {
            if (corpse == caster)
                return "LIMIT BREAK";
            if (caster == 0)
                return string.Empty;

            return Names.TryGetValue(caster, out var name2) ? name2 : "Unknown";
        }

        private Vector3? GetActorPosition(uint corpse)
            => Positions.TryGetValue(corpse, out var pos) ? pos : null;

        public unsafe void DrawOnPartyFrames(ImDrawListPtr drawPtr)
        {
            if (!_drawCards)
                return;

            var party = (AtkUnitBase*)Dalamud.GameGui.GetAddonByName("_PartyList", 1);
            var drawParty = CardsPls.Config.ShowGroupFrame && party != null && party->IsVisible;

            var alliance1 = (AtkUnitBase*)Dalamud.GameGui.GetAddonByName("_AllianceList1", 1);
            var drawAlliance1 = CardsPls.Config.ShowAllianceFrame && alliance1 != null && alliance1->IsVisible;

            var alliance2 = (AtkUnitBase*)Dalamud.GameGui.GetAddonByName("_AllianceList2", 1);
            var drawAlliance2 = CardsPls.Config.ShowAllianceFrame && alliance2 != null && alliance2->IsVisible;

            var anyParty = drawParty || drawAlliance1 || drawAlliance2;

            void DrawWhichRect(uint corpse, ActorState state)
            {
                var name = GetActorName(corpse, state.Caster);
                if (anyParty)
                {
                    var group = _hudManager.FindGroupMemberById(corpse);
                    if (group != null)
                    {
                        var color = CardsPls.Config.DispellableColor;
                        if (color != 0)
                            switch (@group.Value.groupIdx)
                            {
                                case 0:
                                    if (drawParty)
                                        DrawPartyRect(drawPtr, party, @group.Value.idx, color, CardsPls.Config.RectType,
                                            CardsPls.Config.ShowCasterNames, name);
                                    break;
                                case 1:
                                    if (drawAlliance1)
                                        DrawAllianceRect(drawPtr, alliance1, @group.Value.idx, color, CardsPls.Config.RectType,
                                            CardsPls.Config.ShowCasterNames,
                                            name);
                                    break;
                                case 2:
                                    if (drawAlliance2)
                                        DrawAllianceRect(drawPtr, alliance2, @group.Value.idx, color, CardsPls.Config.RectType,
                                            CardsPls.Config.ShowCasterNames,
                                            name);
                                    break;
                            }
                    }
                }

                if (corpse == state.Caster)
                    return;

                if (CardsPls.Config.HideSymbolsOnSelf && corpse == PlayerRez.Item2.Caster)
                    return;

                var pos = GetActorPosition(corpse);
                if (pos != null && Dalamud.GameGui.WorldToScreen(pos.Value, out var screenPos))
                    DrawInWorld(screenPos, name, state);
            }

            foreach (var (actorId, state) in CardList)
                DrawWhichRect(actorId, state);
        }

        public void Draw()
        {
            if (CardList.Count == 0)
                return;

            _drawCards = CardsPls.Config.EnabledCards;

            if (CardsPls.Config.RestrictedJobs)
            {
                var (job, level) = _actorWatcher.CurrentPlayerJob();

                if (job != Job.AST || level < 30)
                    _drawCards = false;
            }

            var drawPtrOpt = BeginRezRects();
            if (drawPtrOpt == null)
                return;

            try
            {
                var drawPtr = drawPtrOpt.Value;
                DrawOnPartyFrames(drawPtr);
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "");
            }
            finally
            {
                ImGui.End();
            }
        }
    }
}
