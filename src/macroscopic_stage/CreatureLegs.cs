using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
///   Procedural walking: finds leg chains in the metaball layout, plants the feet on the ground and steps them
///   when the body moves away (alternating groups, like Spore). Solving is done by Godot's FABRIK modifier.
/// </summary>
public partial class CreatureLegs : Node3D
{
    private readonly List<Leg> legs = new();

    private RigidBody3D body = null!;
    private Node3D meshRoot = null!;
    private Fabrik3D? ik;
    private float legLength = 1;
    private int steppingGroup = -1;

    public bool Walking { get; set; } = true;

    public int LegCount => legs.Count;

    /// <summary>
    ///   Longer legs walk faster
    /// </summary>
    public float MaxSpeed => 1.5f + legLength * 2.0f;

    /// <summary>
    ///   Finds leg chains: a leaf metaball below the body and its ancestors down from the body
    /// </summary>
    public static List<List<MacroscopicMetaball>> FindLegChains(IReadOnlyList<MacroscopicMetaball> layout)
    {
        var result = new List<List<MacroscopicMetaball>>();

        var root = layout.FirstOrDefault(m => m.Parent == null);
        if (root == null)
            return result;

        var childCount = layout.Where(m => m.Parent != null)
            .GroupBy(m => m.Parent!).ToDictionary(g => g.Key, g => g.Count());

        // Body bottom: the lowest point of metaballs that are not part of a downward hanging chain
        float bodyY = root.Position.Y;

        foreach (var leaf in layout.Where(m => !childCount.ContainsKey(m) && m.Parent != null))
        {
            if (leaf.Position.Y > bodyY - leaf.Radius - 0.3f)
                continue;

            var chain = new List<MacroscopicMetaball> { leaf };
            var current = leaf;

            // Walk up while still hanging below the body and not branching
            while (current.Parent is MacroscopicMetaball parent && parent.Parent != null &&
                   childCount.GetValueOrDefault(parent) == 1 && parent.Position.Y < bodyY - 0.05f)
            {
                chain.Add(parent);
                current = parent;
            }

            if (chain.Count < 2)
                continue;

            chain.Reverse();
            result.Add(chain);
        }

        return result;
    }

    /// <summary>
    ///   Test helper: replaces the body with a simple quadruped (spine, neck, head, tail and four legs)
    /// </summary>
    public static void MakeTestQuadruped(MacroscopicSpecies species)
    {
        var layout = species.ModifiableBodyLayout;
        var cellType = species.ModifiableCellTypes.First();
        layout.Clear();

        MacroscopicMetaball Add(Metaball? parent, float size, Vector3 direction)
        {
            var metaball = new MacroscopicMetaball(cellType) { Size = size, ModifiableParent = parent };

            metaball.Position = parent == null ?
                Vector3.Zero :
                parent.Position + direction.Normalized() * (parent.Radius + metaball.Radius) * 0.9f;

            layout.Add(metaball);
            return metaball;
        }

        // Spine along -Z (forward)
        var spine = new List<MacroscopicMetaball> { Add(null, 1.3f, Vector3.Zero) };
        spine.Add(Add(spine[0], 1.2f, Vector3.Forward));
        spine.Add(Add(spine[0], 1.2f, Vector3.Back));

        var neck = Add(spine[1], 0.8f, new Vector3(0, 0.8f, -1));
        var head = Add(neck, 1.0f, new Vector3(0, 0.3f, -1));
        Add(head, 0.5f, new Vector3(0, -0.2f, -1));

        var tail = Add(spine[2], 0.7f, new Vector3(0, 0.2f, 1));
        tail = Add(tail, 0.5f, new Vector3(0, 0, 1));
        Add(tail, 0.35f, new Vector3(0, -0.2f, 1));

        foreach (var hip in new[] { spine[1], spine[2] })
        {
            foreach (var side in new[] { -1.0f, 1.0f })
            {
                Metaball parent = Add(hip, 0.6f, new Vector3(side, -0.6f, 0));
                parent = Add(parent, 0.5f, new Vector3(side * 0.2f, -1, 0));
                parent = Add(parent, 0.45f, new Vector3(0, -1, 0.1f));
                parent = Add(parent, 0.4f, new Vector3(0, -1, -0.1f));
                Add(parent, 0.45f, new Vector3(0, -1, -0.4f));
            }
        }

        species.OnEdited();
    }

    public void Setup(RigidBody3D creatureBody, Node3D mesh, Skeleton3D skeleton, List<MacroscopicMetaball> bones,
        IReadOnlyList<MacroscopicMetaball> layout)
    {
        body = creatureBody;
        meshRoot = mesh;

        foreach (var leg in legs)
            leg.Target.QueueFree();

        legs.Clear();
        ik?.QueueFree();
        ik = null;

        var chains = FindLegChains(layout);
        if (chains.Count == 0)
            return;

        ik = new Fabrik3D { Name = "LegIK" };
        ik.SetSettingCount(chains.Count);

        // Alternate the sides front to back so diagonal legs move together
        var ordered = chains.OrderBy(c => c[^1].Position.Z).ToList();
        int leftIndex = 0;
        int rightIndex = 0;

        for (int i = 0; i < ordered.Count; ++i)
        {
            var chain = ordered[i];
            var foot = chain[^1];
            bool left = foot.Position.X < 0;
            int sideIndex = left ? leftIndex++ : rightIndex++;

            var target = new Marker3D { Name = $"FootTarget{i}", TopLevel = true };
            AddChild(target);

            ik.SetRootBone(i, bones.IndexOf(chain[0]));
            ik.SetEndBone(i, bones.IndexOf(foot));

            float length = 0;
            for (int j = 1; j < chain.Count; ++j)
                length += chain[j].Position.DistanceTo(chain[j - 1].Position);

            legLength = MathF.Max(legLength, length);

            legs.Add(new Leg
            {
                Target = target,
                RestFootLocal = foot.Position with { Y = foot.Position.Y - foot.Radius },
                FootRadius = foot.Radius,
                Group = (sideIndex + (left ? 0 : 1)) % 2,
            });
        }

        skeleton.AddChild(ik);
        ik.GetParent().MoveChild(ik, -1);

        // Late wiring of the target paths once everything is in the tree
        for (int i = 0; i < legs.Count; ++i)
            ik.SetTargetNode(i, ik.GetPathTo(legs[i].Target));

        GD.Print("CreatureLegs: walking with ", legs.Count, " legs, leg length ", legLength);

        foreach (var leg in legs)
        {
            leg.Planted = GroundBelow(meshRoot.GlobalTransform * leg.RestFootLocal);
            leg.Target.GlobalPosition = leg.Planted + Vector3.Up * leg.FootRadius;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (ik == null || legs.Count == 0)
            return;

        ik.Active = Walking;
        if (!Walking)
            return;

        var velocity = body.LinearVelocity with { Y = 0 };
        float speed = velocity.Length();

        // Faster movement = quicker steps, the stride stays within what the legs can reach
        float stepDuration = speed < 0.1f ? 0.35f : Math.Clamp(legLength * 0.45f / speed, 0.1f, 0.35f);
        float stepThreshold = legLength * 0.3f;
        float maxReach = legLength * 0.9f;

        bool anyStepping = false;
        foreach (var leg in legs)
        {
            if (leg.StepProgress < 0)
                continue;

            anyStepping = true;
            leg.StepProgress += (float)delta / stepDuration;

            if (leg.StepProgress >= 1)
            {
                leg.StepProgress = -1;
                leg.Planted = leg.StepTo;
            }
        }

        if (!anyStepping)
            steppingGroup = -1;

        // When no group is stepping, the group that is furthest behind lifts its legs together (a trot)
        if (steppingGroup < 0)
        {
            float bestDistance = 0;

            foreach (var leg in legs)
            {
                var distance = DistanceFromHome(leg);
                if (distance >= stepThreshold && distance > bestDistance)
                {
                    bestDistance = distance;
                    steppingGroup = leg.Group;
                }
            }

            if (steppingGroup >= 0)
            {
                foreach (var leg in legs)
                {
                    if (leg.Group == steppingGroup && DistanceFromHome(leg) >= stepThreshold * 0.5f)
                        StartStep(leg, velocity, stepDuration);
                }
            }
        }

        // A foot that is left too far behind always steps, whatever its group
        foreach (var leg in legs)
        {
            if (leg.StepProgress < 0 && DistanceFromHome(leg) > maxReach)
                StartStep(leg, velocity, stepDuration);
        }

        foreach (var leg in legs)
        {
            Vector3 position;

            if (leg.StepProgress >= 0)
            {
                float t = leg.StepProgress;
                float eased = t * t * (3 - 2 * t);
                position = leg.StepFrom.Lerp(leg.StepTo, eased) +
                    Vector3.Up * MathF.Sin(t * MathF.PI) * legLength * 0.35f;
            }
            else
            {
                position = leg.Planted;
            }

            leg.Target.GlobalPosition = position + Vector3.Up * leg.FootRadius;
        }
    }

    private float DistanceFromHome(Leg leg)
    {
        var home = meshRoot.GlobalTransform * leg.RestFootLocal;
        return (home - leg.Planted).Length();
    }

    private void StartStep(Leg leg, Vector3 velocity, float stepDuration)
    {
        var home = meshRoot.GlobalTransform * leg.RestFootLocal;

        // Land ahead of the body so the foot does not need to step again immediately
        leg.StepFrom = leg.Planted;
        leg.StepTo = GroundBelow(home + velocity * stepDuration);
        leg.StepProgress = 0;
    }

    private Vector3 GroundBelow(Vector3 point)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return point;

        var query = PhysicsRayQueryParameters3D.Create(point + Vector3.Up * legLength * 2,
            point + Vector3.Down * legLength * 4);
        query.Exclude = new Godot.Collections.Array<Rid> { body.GetRid() };

        var hit = space.IntersectRay(query);
        if (hit.Count == 0)
            return point;

        return (Vector3)hit["position"];
    }

    private class Leg
    {
        public Marker3D Target = null!;
        public Vector3 RestFootLocal;
        public float FootRadius;
        public int Group;
        public Vector3 Planted;
        public Vector3 StepFrom;
        public Vector3 StepTo;
        public float StepProgress = -1;
    }
}
