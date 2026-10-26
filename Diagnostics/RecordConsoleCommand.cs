using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;

namespace ArtificialCreation.Diagnostics;

public sealed class RecordConsoleCommand : AbstractConsoleCmd
{
    public override string CmdName => "acrecord";
    public override string Args => "on | off | status";
    public override string Description => "Enable or disable automatic Artificial Creation playtest screenshots, or show the report location.";
    public override bool IsNetworked => false;
    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        string action = args.FirstOrDefault()?.ToLowerInvariant() ?? "status";
        if (action is not ("on" or "off" or "status")) return new CmdResult(false, $"Usage: acrecord {Args}");
        try
        {
            if (action != "status") AutoCapture.SetEnabled(action == "on");
            return new CmdResult(true, AutoCapture.Status);
        }
        catch (Exception ex) { return new CmdResult(false, ex.Message); }
    }
}
