using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using Card = ArtificialCreation.Cards.ArtificialCreation;

namespace ArtificialCreation.Diagnostics;

internal static class AutoCapture
{
    private static RunState? _run;
    private static PlaytestReport? _report;
    private static string? _directory;
    private static readonly HashSet<string> Pending = [];
    private static bool _failed;
    private static bool _userEnabled = true;
    internal static string Root => Path.Combine(ProjectSettings.GlobalizePath("user://"), "artificial_creation_playtests");
    internal static bool Enabled => !TestMode.IsOn && !_failed && _userEnabled;
    internal static void Initialize()
    {
        if (!TestMode.IsOn) _userEnabled = !File.Exists(Path.Combine(Root, "disabled"));
    }
    internal static string Status => $"Auto screenshots: {(Enabled ? "on" : "off")}. Report: {_directory ?? Root}";
    internal static void SetEnabled(bool enabled)
    {
        Directory.CreateDirectory(Root);
        string marker = Path.Combine(Root, "disabled");
        if (enabled) File.Delete(marker);
        else File.WriteAllText(marker, "Disabled with acrecord off\n");
        _userEnabled = enabled;
        _failed = false;
    }

    // Every hook is observational and failure-contained. Disk/capture errors must
    // never fail a card play, delay a game Task, or prevent a reward choice.
    internal static void Safe(Action action)
    {
        if (!Enabled) return;
        try { action(); }
        catch (Exception e)
        {
            _failed = true;
            System.Console.Error.WriteLine($"[ArtificialCreation] Auto-capture disabled after an error: {e}");
        }
    }

    internal static void Later(string key, double delay, Func<bool> stillRelevant, Action action)
    {
        Safe(() =>
        {
            if (!Pending.Add(key)) return;
            _ = Wait();
            async Task Wait()
            {
                try
                {
                    var tree = (SceneTree)Engine.GetMainLoop();
                    await tree.ToSignal(tree.CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
                    // A draw can happen while loading a paused save. Wait for the
                    // pause overlay to close rather than photographing the menu.
                    for (int i = 0; RunManager.Instance.IsPaused && i < 300; i++)
                        await tree.ToSignal(tree.CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                    if (RunManager.Instance.IsPaused) return;
                    await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    Safe(() => { if (stillRelevant()) action(); });
                }
                catch (Exception e) { System.Console.Error.WriteLine($"[ArtificialCreation] Capture skipped: {e.Message}"); }
                finally { Pending.Remove(key); }
            }
        });
    }

    internal static void Record(string kind, string message, bool screenshot, Player? player = null)
    {
        Safe(() =>
        {
            var run = RunManager.Instance.DebugOnlyGetState();
            if (run == null) return;
            player ??= LocalContext.GetMe(run);
            if (player == null) return;
            if (!ReferenceEquals(_run, run))
            {
                _run = run;
                string root = Root;
                _directory = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
                Directory.CreateDirectory(_directory);
                File.WriteAllText(Path.Combine(root, "latest.txt"), _directory);
                _report = new PlaytestReport(_directory, run.Rng.StringSeed);
            }
            var state = player.PlayerCombatState;
            var combat = player.Creature.CombatState;
            var holders = NPlayerHand.Instance?.ActiveHolders.Where(h => h.CardModel != null)
                .OrderBy(h => h.GlobalPosition.X).ToArray() ?? [];
            string[] hand = screenshot ? holders.Select(h => h.CardModel!.Title).ToArray() : [];
            var highlights = new List<Highlight>();
            string? filename = null;
            if (screenshot && Engine.GetMainLoop() is SceneTree tree)
            {
                var viewport = tree.Root;
                using var image = viewport.GetTexture().GetImage();
                if (image != null && !image.IsEmpty())
                {
                    filename = $"{_report!.NextNumber:0000}_{kind}.png";
                    if (image.SavePng(Path.Combine(_directory!, filename)) != Error.Ok)
                        throw new IOException("Could not save screenshot");
                    var viewportSize = viewport.GetVisibleRect().Size;
                    for (int i = 0; i < holders.Length; i++)
                    {
                        if (holders[i].CardModel is not Card || holders[i].CardNode is not { } node) continue;
                        // NCard itself is a zero-size anchor. Its Body contains
                        // the actual card rectangle, including fan/hover transforms.
                        var transform = node.Body.GetGlobalTransformWithCanvas();
                        var size = node.Body.Size;
                        if (size.X <= 0 || size.Y <= 0) size = MegaCrit.Sts2.Core.Nodes.Cards.NCard.defaultSize;
                        Vector2[] corners = [transform * Vector2.Zero, transform * new Vector2(size.X, 0),
                            transform * size, transform * new Vector2(0, size.Y)];
                        double x = Math.Clamp(corners.Min(p => p.X) / viewportSize.X * 100, 0, 100);
                        double y = Math.Clamp(corners.Min(p => p.Y) / viewportSize.Y * 100, 0, 100);
                        double right = Math.Clamp(corners.Max(p => p.X) / viewportSize.X * 100, x, 100);
                        double bottom = Math.Clamp(corners.Max(p => p.Y) / viewportSize.Y * 100, y, 100);
                        highlights.Add(new Highlight(x, y, right - x, bottom - y, $"Artificial Creation · {i + 1}/{holders.Length}"));
                    }
                }
            }
            bool includeStatus = screenshot || kind == "resolved";
            string status = includeStatus ? $"HP {player.Creature.CurrentHp}/{player.Creature.MaxHp}" : "";
            if (includeStatus && combat != null && state != null)
                status += $" · Energy {state.Energy} · Orbs {state.OrbQueue.Orbs.Count}/{state.OrbQueue.Capacity}: " +
                    string.Join(", ", state.OrbQueue.Orbs.Select(o => o.Id.Entry));
            _report!.Append(new PlaytestEntry(_report.NextNumber, kind, run.TotalFloor, combat?.RoundNumber ?? 0,
                message, hand, status, filename, highlights.ToArray()));
        });
    }
}
