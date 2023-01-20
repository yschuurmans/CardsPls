using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;
using System.Linq;

namespace CardsPls.Managers
{
    public class StatusSet
    {
        private readonly SortedList<ushort, (Status, string)> _enabledStatusSet;
        private readonly SortedList<ushort, (Status, string)> _disabledStatusSet;

        public IList<(Status, string)> EnabledStatusSet
            => _enabledStatusSet.Values;

        public IList<(Status, string)> DisabledStatusSet
            => _disabledStatusSet.Values;

        public bool IsEnabled(ushort statusId)
            => _enabledStatusSet.ContainsKey(statusId);

        public StatusSet()
        {
            var sheet = Dalamud.GameData.GetExcelSheet<Status>();
            _enabledStatusSet = new SortedList<ushort, (Status, string)>(sheet!.Where(s => s.CanDispel && s.Name.RawData.Length > 0)
                .ToDictionary(s => (ushort)s.RowId, s => (s, s.Name.ToString().ToLowerInvariant())));
            _disabledStatusSet = new SortedList<ushort, (Status, string)>(_enabledStatusSet.Count);

            var bad = false;
            foreach (var statusId in CardsPls.Config.UnmonitoredStatuses)
            {
                if (_enabledStatusSet.TryGetValue(statusId, out var status))
                {
                    _disabledStatusSet[statusId] = status;
                    _enabledStatusSet.Remove(statusId);
                }
                else
                {
                    bad = true;
                }
            }

            if (!bad)
                return;

            CardsPls.Config.UnmonitoredStatuses = _disabledStatusSet.Select(kvp => kvp.Key).ToHashSet();
            CardsPls.Config.Save();
        }

        public void Swap(ushort statusId)
        {
            if (_enabledStatusSet.TryGetValue(statusId, out var status))
            {
                for (var i = 0; i < _enabledStatusSet.Count; ++i)
                {
                    var (key, value) = _enabledStatusSet.ElementAt(i);
                    if (value.Item2 != status.Item2)
                        continue;

                    _disabledStatusSet.Add(key, value);
                    _enabledStatusSet.Remove(key);
                    CardsPls.Config.UnmonitoredStatuses.Add(key);
                    --i;
                }

                CardsPls.Config.Save();
            }
            else if (_disabledStatusSet.TryGetValue(statusId, out status))
            {
                for (var i = 0; i < _disabledStatusSet.Count; ++i)
                {
                    var (key, value) = _disabledStatusSet.ElementAt(i);
                    if (value.Item2 != status.Item2)
                        continue;

                    _enabledStatusSet.Add(key, value);
                    _disabledStatusSet.Remove(key);
                    CardsPls.Config.UnmonitoredStatuses.Remove(key);
                    --i;
                }

                CardsPls.Config.Save();
            }
            else
            {
                PluginLog.Warning($"Trying to swap Status {statusId}, but it is not a valid status.");
            }
        }

        public void ClearEnabledList()
        {
            var previousCount = CardsPls.Config.UnmonitoredStatuses.Count;
            foreach (var (key, value) in _enabledStatusSet)
            {
                _disabledStatusSet.Add(key, value);
                CardsPls.Config.UnmonitoredStatuses.Add(key);
            }

            _enabledStatusSet.Clear();
            if (previousCount != CardsPls.Config.UnmonitoredStatuses.Count)
                CardsPls.Config.Save();
        }

        public void ClearDisabledList()
        {
            var previousCount = CardsPls.Config.UnmonitoredStatuses.Count;
            foreach (var (key, value) in _disabledStatusSet)
                _enabledStatusSet.Add(key, value);
            _disabledStatusSet.Clear();
            CardsPls.Config.UnmonitoredStatuses.Clear();
            if (previousCount != 0)
                CardsPls.Config.Save();
        }
    }
}
