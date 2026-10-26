using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

/// <summary>
///   Unattended test helper: after "--" on the command line pass
///   devshot-dir=PATH devshot-times=4,8,12 [devshot-hold=g_move_forward] [devshot-hold-from=6]
///   to save viewport images at those seconds and quit after the last one. Forces windowed mode.
/// </summary>
[GodotAutoload]
public partial class DevShots : Node
{
    private readonly List<float> times = new();
    private string dir = string.Empty;
    private string? holdAction;
    private float holdFrom;
    private float elapsed;
    private int shotIndex;
    private bool landDone;
    private bool cameraDone;

    private static bool? isActive;

    public static bool IsActive => isActive ??= ParseArgs().ContainsKey("devshot-dir");

    public static Dictionary<string, string> ParseArgs()
    {
        var result = new Dictionary<string, string>();

        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            var split = arg.TrimStart('-').Split('=', 2);
            result[split[0]] = split.Length > 1 ? split[1] : string.Empty;
        }

        return result;
    }

    public override void _EnterTree()
    {
        var args = ParseArgs();

        if (!args.TryGetValue("devshot-dir", out var value))
        {
            // Without screenshots only the dev-* stage hooks can run (e.g. from Start-Landbeest.cmd)
            SetProcess(args.Keys.Any(k => k.StartsWith("dev-", StringComparison.Ordinal)));
            return;
        }

        dir = value;
        DirAccess.MakeDirRecursiveAbsolute(dir);

        if (args.TryGetValue("devshot-times", out var timeList))
        {
            times.AddRange(timeList.Split(',').Select(t => float.Parse(t, CultureInfo.InvariantCulture)));
        }
        else
        {
            times.Add(5);
        }

        args.TryGetValue("devshot-hold", out holdAction);

        if (args.TryGetValue("devshot-hold-from", out var from))
            holdFrom = float.Parse(from, CultureInfo.InvariantCulture);

        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        elapsed += (float)delta;

        if (!string.IsNullOrEmpty(holdAction) && elapsed >= holdFrom)
        {
            foreach (var action in holdAction.Split('+'))
                Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });

            holdAction = null;
        }

        RunStageHooks();

        if (dir.Length == 0 || shotIndex >= times.Count || elapsed < times[shotIndex])
            return;

        var image = GetViewport().GetTexture().GetImage();
        var path = $"{dir}/shot{shotIndex:D2}_{times[shotIndex]:0}s.png";
        image.SavePng(path);
        GD.Print("DevShots saved ", path);

        if (GetTree().CurrentScene is MacroscopicStage { Player: { } player })
            GD.Print("DevShots player speed ", player.LinearVelocity.Length(), " at ", player.GlobalPosition);

        ++shotIndex;

        if (shotIndex >= times.Count)
            GetTree().Quit();
    }

    /// <summary>
    ///   dev-land=SECONDS teleports the macroscopic player to land, dev-camyaw=RADIANS turns the camera once,
    ///   dev-walk=SECONDS makes the player walk forward
    /// </summary>
    private void RunStageHooks()
    {
        var stage = GetTree().CurrentScene as MacroscopicStage;
        if (stage?.Player == null)
            return;

        var args = ParseArgs();

        if (!landDone && args.TryGetValue("dev-land", out var land) && elapsed >= ParseFloat(land))
        {
            landDone = true;
            stage.TeleportToLand();
        }

        if (!cameraDone && args.TryGetValue("dev-camyaw", out var yaw))
        {
            cameraDone = true;
            stage.RotateCamera(ParseFloat(yaw), args.TryGetValue("dev-campitch", out var p) ? ParseFloat(p) : 0);
        }

        if (args.TryGetValue("dev-walk", out var walk) && elapsed >= ParseFloat(walk))
        {
            stage.Player.MovementDirection = new Vector3(0, 0, -1);
            stage.Player.LookVector = new Vector3(0, 0, -1);
        }
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value, CultureInfo.InvariantCulture);
    }
}
