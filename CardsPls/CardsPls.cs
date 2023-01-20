using CardsPls.GUI;
using CardsPls.Managers;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using System.Reflection;

namespace CardsPls
{
    // auto-format:off

    public partial class CardsPls : IDalamudPlugin
    {
        public string Name
            => "CardsPls";

        public static string Version = "";

        public static CardsPlsConfig Config { get; private set; } = null!;
        private readonly ActorWatcher _actorWatcher;
        private readonly Overlay _overlay;
        private readonly Interface _interface;

        public StatusSet StatusSet;

        public CardsPls(DalamudPluginInterface pluginInterface)
        {
            Dalamud.Initialize(pluginInterface);
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
            Config = CardsPlsConfig.Load();

            StatusSet = new StatusSet();
            _actorWatcher = new ActorWatcher(StatusSet);
            _overlay = new Overlay(_actorWatcher);
            _interface = new Interface(this);

            if (Config.Enabled)
                Enable();
            else
                Disable();
            Dalamud.Commands.AddHandler("/cardspls", new CommandInfo(OnCardsPls)
            {
                HelpMessage = "Open the configuration window for CardsPls.",
                ShowInHelp = true,
            });
        }

        public void OnCardsPls(string _, string arguments)
        {
            _interface!.Visible = !_interface.Visible;
        }

        public void Enable()
        {
            _actorWatcher!.Enable();
            _overlay!.Enable();
        }

        public void Disable()
        {
            _actorWatcher!.Disable();
            _overlay!.Disable();
        }

        public void Dispose()
        {
            Dalamud.Commands.RemoveHandler("/cardspls");
            _interface.Dispose();
            _overlay.Dispose();
            _actorWatcher.Dispose();
        }
    }
}
