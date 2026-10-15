using System.Text.Json;

namespace Planer_App.Views;

/// <summary>Explicit alternate-data QA only. No logging during an ordinary planner session.</summary>
internal static class DragDiagnostics
{
    public static void Write(string action, object details)
    {
        if (App.DragTracePath is not { } path) return;
        try { File.AppendAllText(path, JsonSerializer.Serialize(new { Action = action, Details = details }) + Environment.NewLine); }
        catch (IOException) { }
    }
}
