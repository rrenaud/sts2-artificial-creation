using System.Net;
using System.Text;
using System.Text.Json;

namespace ArtificialCreation.Diagnostics;

public sealed record Highlight(double X, double Y, double Width, double Height, string Label);
public sealed record PlaytestEntry(int Number, string Kind, int Floor, int Turn, string Message,
    string[] Hand, string Status, string? Screenshot = null, Highlight[]? Highlights = null);

public sealed class PlaytestReport(string directory, string seed)
{
    private readonly List<PlaytestEntry> _entries = [];

    public void Append(PlaytestEntry entry)
    {
        Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "events.jsonl"), JsonSerializer.Serialize(entry) + "\n");
        _entries.Add(entry);
        // Replace the report atomically so it is readable while the game is running.
        string temporary = Path.Combine(directory, "report.html.tmp");
        File.WriteAllText(temporary, Render(seed, _entries));
        File.Move(temporary, Path.Combine(directory, "report.html"), overwrite: true);
    }

    public static string Render(string seed, IEnumerable<PlaytestEntry> entries)
    {
        static string E(string text) => WebUtility.HtmlEncode(text);
        var html = new StringBuilder("<!doctype html><html lang='en'><meta charset='utf-8'><meta name='viewport' content='width=device-width'><title>Artificial Creation playtest</title><style>body{background:#111820;color:#e5edf4;font:16px system-ui;max-width:1150px;margin:32px auto;padding:0 20px}h1,h2{color:#8be0d4}section{border-top:1px solid #384653;margin-top:32px}.shot{position:relative;display:block}.shot img{width:100%;display:block}.mark{position:absolute;border:3px solid #5effbb;box-sizing:border-box;pointer-events:none}.mark span{position:absolute;bottom:100%;left:0;background:#092d24;color:white;padding:3px 6px;white-space:nowrap;font-size:13px}.meta{color:#aab9c7;font-size:14px}figure{margin:22px 0}figcaption{margin:10px 0}.hand{font-size:14px;line-height:1.8}.play{margin:6px 0}a{color:#8be0d4}</style><h1>Artificial Creation — playtest</h1>");
        html.Append($"<p>Seed: <strong>{E(seed)}</strong>. Hand positions count from left to right. Green boxes mark Artificial Creation; they are report overlays, not changes to the game.</p>");
        foreach (var floor in entries.GroupBy(e => e.Floor))
        {
            html.Append($"<section><h2>Floor {floor.Key}</h2>");
            foreach (var e in floor)
            {
                string heading = $"#{e.Number} · {(e.Turn > 0 ? $"Turn {e.Turn} · " : "")}{e.Message}";
                if (e.Screenshot is null)
                    html.Append($"<p class='play'>{E(heading)}</p>");
                else
                {
                    html.Append($"<figure><figcaption>{E(heading)}</figcaption><a class='shot' href='{E(e.Screenshot)}'><img loading='lazy' src='{E(e.Screenshot)}' alt='{E(heading)}'>");
                    foreach (var box in e.Highlights ?? [])
                    {
                        string style = FormattableString.Invariant($"left:{box.X:F3}%;top:{box.Y:F3}%;width:{box.Width:F3}%;height:{box.Height:F3}%");
                        html.Append($"<div class='mark' style='{style}'><span>{E(box.Label)}</span></div>");
                    }
                    html.Append("</a></figure>");
                }
                if (e.Hand.Length > 0)
                    html.Append($"<p class='hand'>Hand: {string.Join(" · ", e.Hand.Select((c, i) => $"{i + 1}. {E(c)}"))}</p>");
                if (e.Status.Length > 0) html.Append($"<p class='meta'>{E(e.Status)}</p>");
            }
            html.Append("</section>");
        }
        return html.Append("</html>").ToString();
    }
}
