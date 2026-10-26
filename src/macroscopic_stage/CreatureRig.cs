using System;
using System.Collections.Generic;
using Godot;

/// <summary>
///   Turns a metaball creature mesh into a skinned mesh: one bone per metaball (parented like the metaballs), vertex
///   weights from the distance to the metaballs. Lets the legs be animated with Godot's built-in IK.
/// </summary>
public static class CreatureRig
{
    private const int MaxInfluences = 4;

    /// <summary>
    ///   Builds the skeleton under the displayer and adds bone weights to its mesh
    /// </summary>
    /// <returns>The skeleton and the metaball for each bone index</returns>
    public static (Skeleton3D Skeleton, List<MacroscopicMetaball> Bones)? Rig(MeshInstance3D displayer,
        IReadOnlyList<MacroscopicMetaball> layout)
    {
        if (displayer.Mesh is not ArrayMesh mesh || mesh.GetSurfaceCount() < 1 || layout.Count < 2)
            return null;

        var bones = new List<MacroscopicMetaball>(layout);
        var indexOf = new Dictionary<IReadOnlyMetaball, int>();
        for (int i = 0; i < bones.Count; ++i)
            indexOf[bones[i]] = i;

        var skeleton = displayer.GetNodeOrNull<Skeleton3D>("CreatureSkeleton");
        skeleton?.Free();

        skeleton = new Skeleton3D { Name = "CreatureSkeleton" };

        for (int i = 0; i < bones.Count; ++i)
            skeleton.AddBone($"b{i}");

        for (int i = 0; i < bones.Count; ++i)
        {
            var metaball = bones[i];
            var localPosition = metaball.Position;

            if (metaball.Parent != null && indexOf.TryGetValue(metaball.Parent, out var parentIndex))
            {
                skeleton.SetBoneParent(i, parentIndex);
                localPosition -= metaball.Parent.Position;
            }

            var rest = new Transform3D(Basis.Identity, localPosition);
            skeleton.SetBoneRest(i, rest);
            skeleton.SetBonePosePosition(i, localPosition);
        }

        displayer.AddChild(skeleton);

        // Re-create the surface with bone data
        var material = mesh.SurfaceGetMaterial(0);
        var arrays = mesh.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();

        var boneIndices = new int[vertices.Length * MaxInfluences];
        var weights = new float[vertices.Length * MaxInfluences];
        var scores = new float[bones.Count];
        Span<int> best = stackalloc int[MaxInfluences];

        for (int v = 0; v < vertices.Length; ++v)
        {
            var vertex = vertices[v];

            for (int b = 0; b < bones.Count; ++b)
            {
                var distance = MathF.Max(vertex.DistanceTo(bones[b].Position) - bones[b].Radius, 0.02f);
                scores[b] = 1.0f / (distance * distance * distance * distance);
            }

            float total = 0;
            for (int k = 0; k < MaxInfluences; ++k)
            {
                int bestIndex = -1;
                for (int b = 0; b < bones.Count; ++b)
                {
                    if (scores[b] < 0)
                        continue;

                    if (bestIndex < 0 || scores[b] > scores[bestIndex])
                        bestIndex = b;
                }

                best[k] = bestIndex;
                if (bestIndex >= 0)
                {
                    total += scores[bestIndex];
                    weights[v * MaxInfluences + k] = scores[bestIndex];
                    scores[bestIndex] = -1;
                }
            }

            for (int k = 0; k < MaxInfluences; ++k)
            {
                boneIndices[v * MaxInfluences + k] = best[k] < 0 ? 0 : best[k];
                weights[v * MaxInfluences + k] = best[k] < 0 ? 0 : weights[v * MaxInfluences + k] / total;
            }
        }

        arrays[(int)Mesh.ArrayType.Bones] = boneIndices;
        arrays[(int)Mesh.ArrayType.Weights] = weights;

        mesh.ClearSurfaces();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, material);

        displayer.Skin = skeleton.CreateSkinFromRestTransforms();
        displayer.Skeleton = displayer.GetPathTo(skeleton);

        return (skeleton, bones);
    }
}
