using System;
using Godot;

/// <summary>
///   Other species living on the Oerbeest land (CC0 Quaternius monsters with their own animations). Walkers roam
///   around a home spot and pause now and then, flyers circle above the hills.
/// </summary>
public partial class Wildlife : Node3D
{
    private const string MonsterFolder = "res://assets/oerbeest/monsters/";

    private static readonly string[] Walkers =
    {
        "Alien", "Cyclops", "Crab", "Mushroom", "Chicken", "Pig", "Deer", "Panda", "Penguin", "GreenDemon",
        "Cactus", "Yeti", "Alien_Tall",
    };

    private static readonly string[] Flyers = { "Bat", "Bee", "YellowDragon" };

    private AnimationPlayer? animation;
    private Vector3 home;
    private Vector3 target;
    private float speed;
    private float pause;
    private bool flying;
    private float flyAngle;
    private float flyRadius;
    private float flyHeight;
    private RandomNumberGenerator random = new();

    public static void SpawnAll(Node parent)
    {
        var random = new RandomNumberGenerator { Seed = 777 };

        // Small herds of each walking species, the first few close to where the player arrives
        int index = 0;
        foreach (var species in Walkers)
        {
            var herdCenter = index++ < 4 ?
                NearbyLandPoint(random, 14 + index * 4) :
                RandomLandPoint(random, 20);
            int herdSize = random.RandiRange(2, 4);
            float scale = random.RandfRange(0.7f, 1.4f);

            for (int i = 0; i < herdSize; ++i)
            {
                var position = herdCenter + new Vector3(random.RandfRange(-5, 5), 0, random.RandfRange(-5, 5));
                Spawn(parent, species, position, scale, false, random);
            }
        }

        foreach (var species in Flyers)
        {
            for (int i = 0; i < 2; ++i)
                Spawn(parent, species, RandomLandPoint(random, 10), random.RandfRange(0.8f, 1.4f), true, random);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (flying)
        {
            flyAngle += dt * speed / flyRadius;
            var offset = new Vector3(MathF.Cos(flyAngle), 0, MathF.Sin(flyAngle)) * flyRadius;
            var next = home + offset;
            next.Y = MathF.Max(OerbeestWorld.HeightAt(next.X, next.Z), OerbeestWorld.SeaLevel) + flyHeight +
                MathF.Sin(flyAngle * 3) * 1.5f;

            LookTowards(next, dt);
            GlobalPosition = next;
            return;
        }

        if (pause > 0)
        {
            pause -= dt;
            if (pause <= 0)
                PickTarget();

            return;
        }

        var position = GlobalPosition;
        var toTarget = (target - position) with { Y = 0 };

        if (toTarget.Length() < 0.5f)
        {
            pause = random.RandfRange(1.5f, 6f);
            Play(random.Randf() < 0.2f ? "Dance" : "Idle");
            return;
        }

        var step = toTarget.Normalized() * speed * dt;
        var newPosition = position + step;
        newPosition.Y = OerbeestWorld.HeightAt(newPosition.X, newPosition.Z);

        LookTowards(newPosition, dt);
        GlobalPosition = newPosition;
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

    private static void Spawn(Node parent, string species, Vector3 position, float scale, bool flyer,
        RandomNumberGenerator random)
    {
        var scene = GD.Load<PackedScene>($"{MonsterFolder}{species}.gltf");
        if (scene == null)
        {
            GD.PrintErr("Oerbeest: missing monster ", species);
            return;
        }

        var animal = new Wildlife
        {
            Name = species,
            flying = flyer,
            speed = flyer ? random.RandfRange(4, 7) : random.RandfRange(0.8f, 2.2f),
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

    private void Play(string name)
    {
        if (animation == null)
            return;

        if (!animation.HasAnimation(name))
            return;

        animation.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        animation.Play(name, 0.25);
    }

    private void LookTowards(Vector3 point, float delta)
    {
        var direction = (point - GlobalPosition) with { Y = 0 };
        if (direction.LengthSquared() < 0.0001f)
            return;

        // Quaternius models face +Z
        float wanted = MathF.Atan2(direction.X, direction.Z);
        Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, wanted, Math.Clamp(delta * 5, 0, 1)), 0);
    }
}
