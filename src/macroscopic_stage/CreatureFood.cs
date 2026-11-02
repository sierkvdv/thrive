using System;
using Godot;

/// <summary>
///   A glowing piece of food the player creature eats by touching it. Eaten food reappears somewhere else near the
///   eater, so there is always something to find. Eating enough lets the species evolve in the editor.
/// </summary>
public partial class CreatureFood : Area3D
{
    private const float SpawnMinDistance = 8;
    private const float SpawnMaxDistance = 45;

    private static readonly Color WaterFoodColour = new(0.95f, 0.75f, 0.25f);
    private static readonly Color LandFoodColour = new(0.85f, 0.2f, 0.3f);

    public const string GroupName = "creature_food";

    private MeshInstance3D visual = null!;
    private bool onLand;
    private float bobTime;
    private float baseY;

    /// <summary>
    ///   Spawns food scattered around a point
    /// </summary>
    public static void SpawnAround(Node parent, Vector3 center, int count, bool land)
    {
        for (int i = 0; i < count; ++i)
        {
            var food = new CreatureFood();
            food.Setup(land);
            parent.AddChild(food);
            food.MoveNear(center);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        bobTime += (float)delta;
        visual.Position = new Vector3(0, MathF.Sin(bobTime * 2) * 0.15f, 0);
        visual.RotateY((float)delta);
    }

    private void Setup(bool land)
    {
        onLand = land;
        AddToGroup(GroupName);
        bobTime = GD.Randf() * 10;

        // Creatures are on collision layer 2
        CollisionLayer = 0;
        CollisionMask = 2;
        Monitoring = true;

        AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.9f } });

        var colour = land ? LandFoodColour : WaterFoodColour;

        visual = new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = 0.35f,
                Height = 0.7f,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = colour,
                    EmissionEnabled = true,
                    Emission = colour,
                    EmissionEnergyMultiplier = 0.8f,
                },
            },
        };

        AddChild(visual);

        BodyEntered += OnBodyEntered;
    }

    private void MoveNear(Vector3 center)
    {
        float angle = GD.Randf() * MathF.Tau;
        float distance = (float)GD.RandRange(SpawnMinDistance, SpawnMaxDistance);

        baseY = onLand ? 0.5f : (float)GD.RandRange(-1.0, 2.5);
        Position = new Vector3(center.X + MathF.Cos(angle) * distance, baseY, center.Z + MathF.Sin(angle) * distance);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not MacroscopicCreature { IsPlayerCreature: true } creature)
            return;

        creature.Eat();

        // Not allowed to move physics objects while the physics callback is running
        var eaterPosition = creature.GlobalPosition;
        Callable.From(() => MoveNear(eaterPosition)).CallDeferred();
    }
}
