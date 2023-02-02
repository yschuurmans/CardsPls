using Dalamud.Configuration;
using System.Collections.Generic;

namespace CardsPls
{
    public enum RectType : byte
    {
        Fill = 0,
        OnlyOutline,
        OnlyFullAlphaOutline,
        FillAndFullAlphaOutline,
    }

    public class CardsPlsConfig : IPluginConfiguration
    {
        public const uint DefaultInWorldBackgroundColorCard = 0xC8143C0A;
        public const uint DefaultCardColor = 0x2C00FF42;


        public int Version { get; set; } = 1;
        public float IconScale { get; set; } = 1f;
        public bool Enabled { get; set; } = true;
        public RectType RectType { get; set; } = RectType.FillAndFullAlphaOutline;
        public bool ShowCasterNames { get; set; } = true;
        public bool ShowAllianceFrame { get; set; } = true;
        public bool ShowGroupFrame { get; set; } = true;
        public bool HideSymbolsOnSelf { get; set; } = false;

        public bool EnabledCards { get; set; } = true;
        public bool RestrictedJobs { get; set; } = false;
        public uint InWorldBackgroundColor { get; set; } = DefaultInWorldBackgroundColorCard;
        public bool ShowIcon { get; set; } = true;
        public bool ShowInWorldText { get; set; } = true;


        public bool EnabledDispel { get; set; } = true;
        public bool RestrictedJobsDispel { get; set; } = false;
        public uint DispellableColor { get; set; } = DefaultCardColor;
        public bool ShowIconCard { get; set; } = true;
        public bool ShowInWorldTextCard { get; set; } = true;
        public HashSet<ushort> UnmonitoredStatuses { get; set; } = new();

        public void Save()
            => Dalamud.PluginInterface.SavePluginConfig(this);

        public static CardsPlsConfig Load()
        {
            if (Dalamud.PluginInterface.GetPluginConfig() is CardsPlsConfig cfg)
                return cfg;

            cfg = new CardsPlsConfig();
            cfg.Save();
            return cfg;
        }
    }
}
