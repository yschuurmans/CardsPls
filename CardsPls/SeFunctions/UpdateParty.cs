using Dalamud.Game;
using System;

namespace CardsPls.SeFunctions
{
    public delegate void UpdatePartyDelegate(IntPtr hudAgent);

    public sealed class UpdateParty : SeFunctionBase<UpdatePartyDelegate>
    {
        public UpdateParty(ISigScanner sigScanner)
            : base(sigScanner, "40 ?? 48 83 ?? ?? 48 8B ?? 48 ?? ?? ?? 48 ?? ?? ?? ?? ?? ?? 83 ?? ?? ?? ?? ?? ?? 74 ?? 48")
        { }
    }
}
