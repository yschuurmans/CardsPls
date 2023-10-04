using CardsPls.Enums;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace CardsPls.Managers;

public readonly struct ActorState
{
    public readonly uint Caster;
    public readonly bool HasCard;

    public ActorState(uint caster, bool hasStatus)
    {
        Caster = caster;
        HasCard = hasStatus;
    }

    public ActorState SetHasStatus(bool hasStatus)
        => new(Caster, hasStatus);

    public static ActorState Nothing = new(0, false);
}

public class ActorWatcher : IDisposable
{
    public static int TestMode;
    private bool _outsidePvP = true;
    private bool _enabled;
    private readonly StatusSet _statusSet;
    private const int ActorTablePlayerLength = 200;
    private readonly ExcelSheet<TerritoryType> _territories;

    public readonly Dictionary<uint, ActorState> CardList = new(128);
    public readonly Dictionary<uint, string> ActorNames = new();
    public readonly Dictionary<uint, Vector3> ActorPositions = new();
    public (uint, ActorState) PlayerRez = (0, ActorState.Nothing);

    private readonly List<uint> CardStatusIDs = new List<uint> { 1882, 1883, 1884, 1885, 1886, 1887 };

    public ActorWatcher(StatusSet statusSet)
    {
        _statusSet = statusSet;
        _territories = Dalamud.GameData.GetExcelSheet<TerritoryType>()!;

        CheckPvP(Dalamud.ClientState.TerritoryType);
    }

    public void Enable()
    {
        if (_enabled)
            return;

        Dalamud.Framework.Update += OnFrameworkUpdate;
        Dalamud.ClientState.TerritoryChanged += CheckPvP;
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled)
            return;

        Dalamud.Framework.Update -= OnFrameworkUpdate;
        Dalamud.ClientState.TerritoryChanged -= CheckPvP;
        _enabled = false;
        CardList.Clear();
        PlayerRez = (0, ActorState.Nothing);
    }

    public void Dispose()
        => Disable();

    private void CheckPvP(ushort territoryId)
    {
        var row = _territories.GetRow(territoryId);
        _outsidePvP = !(row?.IsPvpZone ?? false);
    }

    public (Job job, byte level) CurrentPlayerJob()
    {
        var player = Dalamud.ClientState.LocalPlayer;
        if (player == null || !IsPlayer(player))
            return (Job.ADV, 0);

        return (PlayerJob(player), player.Level);
    }

    private static unsafe (uint, uint) GetCurrentCast(BattleChara player)
    {
        var battleChara = (FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)player.Address;
        ref var cast = ref *battleChara->GetCastInfo;
        if (cast.ActionType != ActionType.Action)
            return (0, 0);

        return (cast.ActionID, cast.CastTargetID);
    }

    private static bool IsPlayer(GameObject actor)
        => actor.ObjectKind == ObjectKind.Player;

    private static bool IsDead(Character player)
        => player.CurrentHp <= 0;

    private static Job PlayerJob(Character player)
        => (Job)player.ClassJob.Id;

    private unsafe bool HasCard(PlayerCharacter player)
    {
        return player.StatusList.Any(status => CardStatusIDs.Contains(status.StatusId));
    }

    private void IterateActors()
    {
        for (var i = 0; i < ActorTablePlayerLength; i += 2)
        {
            var actor = Dalamud.Objects[i];
            if (actor is not PlayerCharacter player)
                continue;

            var hasCard = HasCard(player);

            if (hasCard)
            {
                ActorPositions[player.ObjectId] = player.Position;
                CardList.Add(player.ObjectId, new ActorState(0, true));
            }
        }
    }

    private void ActorNamesAdd(GameObject actor)
    {
        if (!ActorNames.TryGetValue(actor.ObjectId, out var name))
            ActorNames[actor.ObjectId] = actor.Name.ToString();
    }

    private unsafe void HandleTestMode()
    {
        var p = Dalamud.ClientState.LocalPlayer;
        if (p == null)
            return;

        ActorNamesAdd(p);
        ActorPositions[p.ObjectId] = p.Position;

        var t = Dalamud.Targets.Target ?? p;
        var tObjectId = Dalamud.Targets.Target?.ObjectId ?? 10;
        switch (TestMode)
        {
            case 1:
                CardList[p.ObjectId] = new ActorState(0, true);
                return;
            case 2:
                CardList[p.ObjectId] = new ActorState(t.ObjectId, false);
                ActorNamesAdd(t);
                return;
            case 3:
                CardList[p.ObjectId] = new ActorState(tObjectId, false);
                PlayerRez = (p.ObjectId, new ActorState(p.ObjectId, false));
                return;
            case 4:
                CardList[p.ObjectId] = new ActorState(0, true);
                return;
            case 5:
                CardList[p.ObjectId] = new ActorState(t.ObjectId, true);
                ActorNamesAdd(t);
                return;
            case 6:
                CardList[p.ObjectId] = new ActorState(tObjectId, false);
                PlayerRez = (p.ObjectId, new ActorState(p.ObjectId, true));
                return;
        }
    }


    public void OnFrameworkUpdate(object _)
    {
        if (!_outsidePvP)
            return;

        CardList.Clear();
        PlayerRez = (0, PlayerRez.Item2);
        if (TestMode == 0)
            IterateActors();
        else
            HandleTestMode();
    }
}
