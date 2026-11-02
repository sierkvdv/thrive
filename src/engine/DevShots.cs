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
    private bool foodDone;
    private int editorLegsPressed;
    private bool editorFinished;
    private bool menuCalled;

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
        var hookArgs = ParseArgs();

        // dev-editorlegs=SECONDS,COUNT presses "add leg pair" in the macroscopic editor COUNT times
        if (editorLegsPressed == 0 && hookArgs.TryGetValue("dev-editorlegs", out var legHook))
        {
            var parts = legHook.Split(',');
            if (elapsed >= ParseFloat(parts[0]))
            {
                var editor = FindNode<MetaballBodyEditorComponent>(GetTree().Root);
                if (editor != null)
                {
                    int count = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 1;
                    for (int i = 0; i < count; ++i)
                        editor.Call("OnAddLegPairPressed");

                    editorLegsPressed = count;

                    // Show the body editor tab so the screenshots show the result
                    FindNode<MacroscopicEditor>(GetTree().Root)?.Call("SetEditorTab", (int)EditorTab.CellEditor);
                }
            }
        }

        // dev-menucall=SECONDS,Method calls a main menu method (like pressing its button)
        if (!menuCalled && hookArgs.TryGetValue("dev-menucall", out var menuCall))
        {
            var parts = menuCall.Split(',');
            if (elapsed >= ParseFloat(parts[0]) && GetTree().CurrentScene is MainMenu menu)
            {
                menuCalled = true;
                menu.Call(parts[1]);
            }
        }

        // dev-editorfinish=SECONDS confirms the editor (like pressing Confirm)
        if (!editorFinished && hookArgs.TryGetValue("dev-editorfinish", out var finishAt) &&
            elapsed >= ParseFloat(finishAt))
        {
            var macroEditor = FindNode<MacroscopicEditor>(GetTree().Root);
            if (macroEditor != null)
            {
                editorFinished = true;
                GD.Print("DevShots finishing editor: ", macroEditor.OnFinishEditing());
            }
        }

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

        if (!foodDone && args.TryGetValue("dev-foodahead", out var foodAt) && elapsed >= ParseFloat(foodAt))
        {
            foodDone = true;
            int i = 0;
            foreach (var food in GetTree().GetNodesInGroup(CreatureFood.GroupName))
            {
                if (i >= 8)
                    break;

                ((Node3D)food).GlobalPosition = stage.Player.GlobalPosition + new Vector3(0, 0.5f, -4 - 3 * i++);
            }
        }

        // dev-walk=START[,END] walks forward, dev-gotopartner=SECONDS then walks to the partner
        if (args.TryGetValue("dev-walk", out var walk))
        {
            var walkTimes = walk.Split(',');
            bool walking = elapsed >= ParseFloat(walkTimes[0]) &&
                (walkTimes.Length < 2 || elapsed < ParseFloat(walkTimes[1]));

            if (walking)
            {
                stage.Player.MovementDirection = new Vector3(0, 0, -1);
                stage.Player.LookVector = new Vector3(0, 0, -1);
            }
        }

        if (args.TryGetValue("dev-gotopartner", out var goTo) && elapsed >= ParseFloat(goTo))
        {
            var partner = stage.DevPartnerPosition;
            if (partner != null)
            {
                var direction = (partner.Value - stage.Player.GlobalPosition) with { Y = 0 };
                if (direction.Length() > 2)
                {
                    stage.Player.MovementDirection = direction.Normalized();
                    stage.Player.LookVector = direction.Normalized();
                }
            }
        }
    }

    private static T? FindNode<T>(Node node)
        where T : Node
    {
        if (node is T found)
            return found;

        foreach (var child in node.GetChildren())
        {
            var result = FindNode<T>(child);
            if (result != null)
                return result;
        }

        return null;
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value, CultureInfo.InvariantCulture);
    }
}
