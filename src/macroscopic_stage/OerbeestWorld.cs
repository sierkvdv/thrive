using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   The Oerbeest land: rolling hills with a coast and sea, covered with scanned (CC0) plants, trees and rocks.
///   Height is a deterministic function so food, wildlife and feet can all ask for the ground height.
/// </summary>
public partial class OerbeestWorld : Node3D
{
    public const float SeaLevel = -1.5f;
    public const float HalfSize = 160;

    private const int Resolution = 160;
    private const string NatureFolder = "res://assets/oerbeest/natuur/";

    private static readonly Godot.FastNoiseLite HillNoise = new()
    {
        Seed = 1977,
        NoiseType = Godot.FastNoiseLite.NoiseTypeEnum.Perlin,
        Frequency = 0.012f,
        FractalOctaves = 4,
    };

    /// <summary>
    ///   Ground height at a world position: a flat meadow at the start, hills around it and the sea towards +X
    /// </summary>
    public static float HeightAt(float x, float z)
    {
        float hills = HillNoise.GetNoise2D(x, z) * 9 + 1.5f;

        // Flat, safe meadow where the creature arrives
        float distance = MathF.Sqrt(x * x + z * z);
        float meadow = Math.Clamp((distance - 12) / 25, 0, 1);
        float height = Mathf.Lerp(0.3f, hills, meadow * meadow);

        // Coast: the land slopes into the sea on the +X side
        height -= MathF.Max(0, x - 45) * 0.12f;

        return height;
    }

    public static bool IsDryLand(float x, float z)
    {
        return HeightAt(x, z) > SeaLevel + 0.5f;
    }

    public void Build()
    {
        BuildTerrain();
        BuildSea();
        ScatterNature();
        Wildlife.SpawnAll(this);
    }

    public const float SeabedLevel = -9;

    /// <summary>
    ///   The sea floor for the water part: sand, boulders and big seaweed (scanned ferns and grass, tinted by the
    ///   water colour)
    /// </summary>
    public void BuildSeabed()
    {
        var floor = new MeshInstance3D
        {
            Name = "SeaFloor",
            Mesh = new PlaneMesh { Size = new Vector2(500, 500), SubdivideDepth = 1, SubdivideWidth = 1 },
            Position = new Vector3(0, SeabedLevel, 0),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = GD.Load<Texture2D>("res://assets/oerbeest/textures/aerial_beach_01_diff_2k.jpg"),
                NormalEnabled = true,
                NormalTexture = GD.Load<Texture2D>("res://assets/oerbeest/textures/aerial_beach_01_nor_gl_2k.jpg"),
                Uv1Scale = new Vector3(40, 40, 40),
                AlbedoColor = new Color(0.8f, 0.9f, 0.85f),
            },
        };

        AddChild(floor);

        var random = new RandomNumberGenerator { Seed = 99 };

        var props = new (string Name, int Count, float MinScale, float MaxScale)[]
        {
            ("boulder_01", 40, 1.0f, 4.0f),
            ("rock_07", 50, 1.5f, 4.5f),
            ("rock_moss_set_01", 40, 1.5f, 3.5f),
            ("fern_02", 160, 2.5f, 5.0f),
            ("grass_medium_01", 300, 3.0f, 6.0f),
            ("shrub_03", 40, 1.5f, 3.0f),
        };

        foreach (var prop in props)
        {
            var scene = LoadNature(prop.Name);
            if (scene == null)
                continue;

            var transforms = new List<Transform3D>();
            for (int i = 0; i < prop.Count; ++i)
            {
                float x = random.RandfRange(-150, 150);
                float z = random.RandfRange(-150, 150);

                if (x * x + z * z < 36)
                    continue;

                var basis = Basis.FromEuler(new Vector3(0, random.RandfRange(0, MathF.Tau), 0))
                    .Scaled(Vector3.One * random.RandfRange(prop.MinScale, prop.MaxScale));
                transforms.Add(new Transform3D(basis, new Vector3(x, SeabedLevel - 0.1f, z)));
            }

            AddMultiMeshes(scene, transforms);
        }
    }

    private static PackedScene? LoadNature(string name)
    {
        return GD.Load<PackedScene>($"{NatureFolder}{name}/{name}_2k.gltf");
    }

    private void BuildTerrain()
    {
        float cell = HalfSize * 2 / Resolution;
        int width = Resolution + 1;

        var heights = new float[width * width];
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);

        for (int z = 0; z < width; ++z)
        {
            for (int x = 0; x < width; ++x)
            {
                float worldX = -HalfSize + x * cell;
                float worldZ = -HalfSize + z * cell;
                heights[z * width + x] = HeightAt(worldX, worldZ);
            }
        }

        Vector3 Vertex(int x, int z)
        {
            return new Vector3(-HalfSize + x * cell, heights[z * width + x], -HalfSize + z * cell);
        }

        for (int z = 0; z < Resolution; ++z)
        {
            for (int x = 0; x < Resolution; ++x)
            {
                var a = Vertex(x, z);
                var b = Vertex(x + 1, z);
                var c = Vertex(x, z + 1);
                var d = Vertex(x + 1, z + 1);

                tool.AddVertex(a);
                tool.AddVertex(b);
                tool.AddVertex(c);
                tool.AddVertex(b);
                tool.AddVertex(d);
                tool.AddVertex(c);
            }
        }

        tool.GenerateNormals();

        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/oerbeest/OerbeestTerrain.gdshader") };
        string textures = "res://assets/oerbeest/textures/";
        material.SetShaderParameter("grass_albedo", GD.Load<Texture2D>(textures + "aerial_grass_rock_diff_2k.jpg"));
        material.SetShaderParameter("grass_normal", GD.Load<Texture2D>(textures + "aerial_grass_rock_nor_gl_2k.jpg"));
        material.SetShaderParameter("beach_albedo", GD.Load<Texture2D>(textures + "aerial_beach_01_diff_2k.jpg"));
        material.SetShaderParameter("beach_normal", GD.Load<Texture2D>(textures + "aerial_beach_01_nor_gl_2k.jpg"));
        material.SetShaderParameter("cliff_albedo", GD.Load<Texture2D>(textures + "cliff_side_diff_2k.jpg"));
        material.SetShaderParameter("cliff_normal", GD.Load<Texture2D>(textures + "cliff_side_nor_gl_2k.jpg"));
        material.SetShaderParameter("sea_level", SeaLevel);
        material.SetShaderParameter("beach_height", SeaLevel + 1.1f);

        var mesh = tool.Commit();
        mesh.SurfaceSetMaterial(0, material);

        var body = new StaticBody3D
        {
            Name = "OerbeestGround",
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = 1, Rough = true, Absorbent = true },
        };

        body.AddChild(new MeshInstance3D { Mesh = mesh });

        var shape = new HeightMapShape3D { MapWidth = width, MapDepth = width, MapData = heights };
        body.AddChild(new CollisionShape3D { Shape = shape, Scale = new Vector3(cell, 1, cell) });

        AddChild(body);
    }

    private void BuildSea()
    {
        var water = new MeshInstance3D
        {
            Name = "Sea",
            Mesh = new PlaneMesh { Size = new Vector2(HalfSize * 6, HalfSize * 6) },
            Position = new Vector3(0, SeaLevel, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        water.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.36f, 0.45f, 0.82f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.08f,
            Metallic = 0.2f,
        };

        AddChild(water);
    }

    private void ScatterNature()
    {
        var random = new RandomNumberGenerator { Seed = 4242 };

        // (model, count, min scale, max scale, keep clear radius around the start)
        var props = new (string Name, int Count, float MinScale, float MaxScale, float Clear)[]
        {
            ("island_tree_01", 22, 0.8f, 1.3f, 16),
            ("island_tree_02", 18, 0.8f, 1.3f, 16),
            ("tree_stump_01", 10, 0.8f, 1.2f, 8),
            ("boulder_01", 20, 0.6f, 2.0f, 8),
            ("rock_07", 30, 0.8f, 2.5f, 6),
            ("rock_moss_set_01", 18, 0.8f, 1.6f, 6),
            ("shrub_02", 60, 0.8f, 1.6f, 5),
            ("shrub_03", 60, 0.8f, 1.6f, 5),
            ("fern_02", 260, 0.9f, 1.8f, 3),
            ("grass_medium_01", 2200, 1.4f, 2.6f, 1.5f),
            ("grass_medium_02", 2200, 1.4f, 2.6f, 1.5f),
            ("dandelion_01", 400, 1.2f, 2.0f, 2),
            ("flower_gazania", 400, 1.2f, 2.0f, 2),
        };

        foreach (var prop in props)
        {
            var scene = LoadNature(prop.Name);
            if (scene == null)
            {
                GD.PrintErr("Oerbeest: missing nature model ", prop.Name);
                continue;
            }

            var transforms = new List<Transform3D>();
            int attempts = 0;

            while (transforms.Count < prop.Count && attempts++ < prop.Count * 20)
            {
                float x = random.RandfRange(-HalfSize + 5, HalfSize - 5);
                float z = random.RandfRange(-HalfSize + 5, HalfSize - 5);

                if (x * x + z * z < prop.Clear * prop.Clear || !IsDryLand(x, z))
                    continue;

                var basis = Basis.FromEuler(new Vector3(0, random.RandfRange(0, MathF.Tau), 0))
                    .Scaled(Vector3.One * random.RandfRange(prop.MinScale, prop.MaxScale));

                transforms.Add(new Transform3D(basis, new Vector3(x, HeightAt(x, z) - 0.05f, z)));
            }

            AddMultiMeshes(scene, transforms);
        }
    }

    /// <summary>
    ///   Draws many copies of a model cheaply: one MultiMesh per mesh part of the model
    /// </summary>
    private void AddMultiMeshes(PackedScene scene, List<Transform3D> transforms)
    {
        var instance = scene.Instantiate<Node3D>();

        foreach (var part in FindMeshes(instance, Transform3D.Identity))
        {
            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = part.Mesh,
                InstanceCount = transforms.Count,
            };

            for (int i = 0; i < transforms.Count; ++i)
                multiMesh.SetInstanceTransform(i, transforms[i] * part.Local);

            AddChild(new MultiMeshInstance3D { Multimesh = multiMesh });
        }

        instance.Free();
    }

    private static IEnumerable<(Mesh Mesh, Transform3D Local)> FindMeshes(Node node, Transform3D parentTransform)
    {
        var transform = node is Node3D node3D ? parentTransform * node3D.Transform : parentTransform;

        if (node is MeshInstance3D { Mesh: not null } meshInstance)
            yield return (meshInstance.Mesh, transform);

        foreach (var child in node.GetChildren())
        {
            foreach (var found in FindMeshes(child, transform))
                yield return found;
        }
    }
}
