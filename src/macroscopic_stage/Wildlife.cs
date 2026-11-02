using System;
using Godot;

/// <summary>
///   Other species living on the Oerbeest land (CC0 Quaternius monsters with their own animations). Prey roams in
///   herds and flees from the player (catching one leaves meat to eat), predators chase and bite the player, flyers
///   circle above the hills.
/// </summary>
public partial class Wildlife : Node3D
{
    private const string MonsterFolder = "res://assets/oerbeest/monsters/";

    private const float PreyNoticeDistance = 9;
    private const float CatchDistance = 1.9f;
    private const float PredatorNoticeDistance = 14;
    private const float BiteDistance = 2.2f;
    private const float BiteCooldown = 2.5f;

    private static readonly string[] Prey =
    {
        "Alien", "Crab", "Mushroom", "Chicken", "Pig", "Deer", "Panda", "Penguin", "Cactus", "Alien_Tall",
    };

    private static readonly string[] Predators = { "Cyclops", "GreenDemon", "Yeti", "Demon" };

    private static readonly string[] Flyers = { "Bat", "Bee", "YellowDragon" };

    private AnimationPlayer? animation;
    private string species = string.Empty;
    private Vector3 home;
    private Vector3 target;
    private float speed;
    private float pause;
    private bool flying;
    private bool predator;
    private float flyAngle;
    private float flyRadius;
    private float flyHeight;
    private float biteTimer;
    private float deadTimer = -1;
    private string currentAnimation = string.Empty;
    private RandomNumberGenerator random = new();

    /// <summary>
    ///   The player creature, set by the stage every frame
    /// </summary>
    public static MacroscopicCreature? Player { get; set; }

    public bool IsPrey => !predator && !flying && deadTimer < 0;

    public bool IsPredator => predator;

    /// <summary>
    ///   Shows a message to the player (set by the stage)
    /// </summary>
    public static Action<string>? ShowMessage { get; set; }

    public static void SpawnAll(Node parent)
    {
        var random = new RandomNumberGenerator { Seed = 777 };

        // Small herds of each prey species, the first few close to where the player arrives
        int index = 0;
        foreach (var name in Prey)
        {
            var herdCenter = index++ < 4 ? NearbyLandPoint(random, 14 + index * 4) : RandomLandPoint(random, 20);
            int herdSize = random.RandiRange(3, 5);
            float scale = random.RandfRange(0.7f, 1.3f);

            for (int i = 0; i < herdSize; ++i)
            {
                var position = herdCenter + new Vector3(random.RandfRange(-5, 5), 0, random.RandfRange(-5, 5));
                Spawn(parent, name, position, scale, random);
            }
        }

        // Lone predators, never right at the start
        foreach (var name in Predators)
            Spawn(parent, name, RandomLandPoint(random, 45), random.RandfRange(1.1f, 1.6f), random);

        foreach (var name in Flyers)
        {
            for (int i = 0; i < 2; ++i)
                Spawn(parent, name, RandomLandPoint(random, 10), random.RandfRange(0.8f, 1.4f), random);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (deadTimer >= 0)
        {
            deadTimer += dt;
            if (deadTimer > 6)
                Respawn();

            return;
        }

        if (flying)
        {
            Fly(dt);
            return;
        }

        biteTimer -= dt;

        var player = Player != null && IsInstanceValid(Player) && Player.MovementMode == MovementMode.Walking ?
            Player :
            null;

        var toPlayer = player != null ? (player.GlobalPosition - GlobalPosition) with { Y = 0 } : Vector3.Zero;
        float playerDistance = player != null ? toPlayer.Length() : float.MaxValue;

        if (predator && player != null && playerDistance < PredatorNoticeDistance)
        {
            Hunt(player, toPlayer, playerDistance, dt);
            return;
        }

        if (!predator && player != null && playerDistance < PreyNoticeDistance)
        {
            if (playerDistance < CatchDistance)
            {
                Die();
                return;
            }

            // Run away from the player
            MoveTowards(GlobalPosition - toPlayer.Normalized() * 5, speed * 2.2f, dt, "Walk", 1.8f);
            return;
        }

        Wander(dt);
    }

    private static Vector3 NearbyLandPoint(RandomNumberGenerator random, float distance)
    {
        for (int attempt = 0; attempt < 50; ++attempt)
        {
            float angle = random.RandfRange(0, MathF.Tau);
            float x = MathF.Cos(angle) * distance;
            float z = MathF.Sin(angle) * distance;

            if (OerbeestWorld.IsDryLand(x, z))
                return new Vector3(x, OerbeestWorld.HeightAt(x, z), z);
        }

        return RandomLandPoint(random, distance);
    }

    private static Vector3 RandomLandPoint(RandomNumberGenerator random, float keepClear)
    {
        for (int attempt = 0; attempt < 200; ++attempt)
        {
            float x = random.RandfRange(-OerbeestWorld.HalfSize + 15, OerbeestWorld.HalfSize - 15);
            float z = random.RandfRange(-OerbeestWorld.HalfSize + 15, OerbeestWorld.HalfSize - 15);

            if (x * x + z * z > keepClear * keepClear && OerbeestWorld.IsDryLand(x, z))
                return new Vector3(x, OerbeestWorld.HeightAt(x, z), z);
        }

        return new Vector3(30, OerbeestWorld.HeightAt(30, 30), 30);
    }

    private static void Spawn(Node parent, string name, Vector3 position, float scale, RandomNumberGenerator random)
    {
        var scene = GD.Load<PackedScene>($"{MonsterFolder}{name}.gltf");
        if (scene == null)
        {
            GD.PrintErr("Oerbeest: missing monster ", name);
            return;
        }

        bool flyer = Array.IndexOf(Flyers, name) >= 0;

        var animal = new Wildlife
        {
            Name = name,
            species = name,
            flying = flyer,
            predator = Array.IndexOf(Predators, name) >= 0,
            speed = flyer ? random.RandfRange(4, 7) : random.RandfRange(0.9f, 1.8f),
            flyRadius = random.RandfRange(12, 30),
            flyHeight = random.RandfRange(8, 16),
            flyAngle = random.RandfRange(0, MathF.Tau),
        };

        animal.random.Seed = random.Randi();

        var model = scene.Instantiate<Node3D>();
        model.Scale = Vector3.One * scale;
        animal.AddChild(model);
        parent.AddChild(animal);

        animal.home = position;
        animal.GlobalPosition = position;
        animal.animation = FindAnimationPlayer(model);

        if (flyer)
        {
            animal.Play("Flying");
        }
        else
        {
            animal.PickTarget();
        }
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer player)
            return player;

        foreach (var child in node.GetChildren())
        {
            var found = FindAnimationPlayer(child);
            if (found != null)
                return found;
        }

        return null;
    }

    private static string DutchName(string name)
    {
        return name switch
        {
            "Cyclops" => "de cycloop",
            "GreenDemon" => "de groene demon",
            "Demon" => "de demon",
            "Yeti" => "de yeti",
            _ => "een roofdier",
        };
    }

    private void Fly(float dt)
    {
        flyAngle += dt * speed / flyRadius;
        var offset = new Vector3(MathF.Cos(flyAngle), 0, MathF.Sin(flyAngle)) * flyRadius;
        var next = home + offset;
        next.Y = MathF.Max(OerbeestWorld.HeightAt(next.X, next.Z), OerbeestWorld.SeaLevel) + flyHeight +
            MathF.Sin(flyAngle * 3) * 1.5f;

        LookTowards(next, dt);
        GlobalPosition = next;
    }

    private void Hunt(MacroscopicCreature player, Vector3 toPlayer, float distance, float dt)
    {
        if (distance > BiteDistance)
        {
            MoveTowards(player.GlobalPosition, speed * 2.4f, dt, "Walk", 1.6f);
            return;
        }

        LookTowards(player.GlobalPosition, dt);

        if (biteTimer > 0)
            return;

        biteTimer = BiteCooldown;
        Play("Bite_InPlace");

        // Knock the player away and steal a bit of their progress
        player.ApplyCentralImpulse((toPlayer.Normalized() + Vector3.Up * 0.4f) * player.Mass * 6);

        if (player.LoseFood())
        {
            ShowMessage?.Invoke($"Au! {DutchName(species)} bijt je en je verliest eten. Ren weg!");
        }
        else
        {
            ShowMessage?.Invoke($"Au! {DutchName(species)} bijt je. Ren weg!");
        }
    }

    private void Wander(float dt)
    {
        if (pause > 0)
        {
            pause -= dt;
            if (pause <= 0)
                PickTarget();

            return;
        }

        if (((target - GlobalPosition) with { Y = 0 }).Length() < 0.5f)
        {
            pause = random.RandfRange(1.5f, 6f);
            Play(random.Randf() < 0.2f ? "Dance" : "Idle");
            return;
        }

        MoveTowards(target, speed, dt, "Walk", 1);
    }

    private void MoveTowards(Vector3 point, float moveSpeed, float dt, string animationName, float animationSpeed)
    {
        var position = GlobalPosition;
        var direction = (point - position) with { Y = 0 };
        if (direction.LengthSquared() < 0.0001f)
            return;

        var next = position + direction.Normalized() * moveSpeed * dt;

        // Stay on dry land and inside the world
        if (!OerbeestWorld.IsDryLand(next.X, next.Z) || MathF.Abs(next.X) > OerbeestWorld.HalfSize - 5 ||
            MathF.Abs(next.Z) > OerbeestWorld.HalfSize - 5)
        {
            PickTarget();
            return;
        }

        next.Y = OerbeestWorld.HeightAt(next.X, next.Z);

        LookTowards(next, dt);
        GlobalPosition = next;
        Play(animationName, animationSpeed);
    }

    private void Die()
    {
        deadTimer = 0;
        Play("Death");

        // Leave meat behind for the hunter
        var meatSpot = GlobalPosition;
        CreatureFood.SpawnMeatAt(GetParent(), meatSpot, 2);
        ShowMessage?.Invoke("Gevangen! Eet het vlees dat is achtergebleven.");
    }

    private void Respawn()
    {
        deadTimer = -1;

        // A new member of the herd arrives somewhere near its home
        var spot = home + new Vector3(random.RandfRange(-8, 8), 0, random.RandfRange(-8, 8));
        if (!OerbeestWorld.IsDryLand(spot.X, spot.Z))
            spot = home;

        spot.Y = OerbeestWorld.HeightAt(spot.X, spot.Z);
        GlobalPosition = spot;
        PickTarget();
    }

    private void PickTarget()
    {
        for (int attempt = 0; attempt < 10; ++attempt)
        {
            var candidate = home + new Vector3(random.RandfRange(-18, 18), 0, random.RandfRange(-18, 18));
            if (OerbeestWorld.IsDryLand(candidate.X, candidate.Z))
            {
                target = candidate;
                Play("Walk");
                return;
            }
        }

        pause = 2;
    }

    private void Play(string name, float playSpeed = 1)
    {
        if (animation == null || !animation.HasAnimation(name))
            return;

        animation.SpeedScale = playSpeed;

        if (currentAnimation == name && animation.IsPlaying())
            return;

        currentAnimation = name;

        // Death plays once, everything else loops
        bool once = name is "Death" or "Bite_InPlace";
        animation.GetAnimation(name).LoopMode = once ? Animation.LoopModeEnum.None : Animation.LoopModeEnum.Linear;
        animation.Play(name, 0.2);
    }

    private void LookTowards(Vector3 point, float delta)
    {
        var direction = (point - GlobalPosition) with { Y = 0 };
        if (direction.LengthSquared() < 0.0001f)
            return;

        // Quaternius models face +Z
        float wanted = MathF.Atan2(direction.X, direction.Z);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, wanted, Math.Clamp(delta * 6, 0, 1)), 0);
    }
}
