using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// The simple shapes Gildebloon's look is made of (#25), made in the game: the same as in UnityProject's
/// BuildGildebloon, which describes them in Bundles/gildebloon.layout.json. Meshes can't come from a bundle, the game
/// crashes loading them (57.4). Keep the two in step.
/// </summary>
public static class GildebloonMeshes
{
    /// <summary>A mesh of the shape <paramref name="kind"/> with the layout's arguments</summary>
    public static Mesh Make(string kind, float[] args) => kind switch
    {
        "sphere" => Sphere((int) Arg(args, 0, 16), (int) Arg(args, 1, 12)),
        "cylinder" => Cylinder((int) Arg(args, 0, 16), Arg(args, 1, 1), Arg(args, 2, 1)),
        "cone" => Cone((int) Arg(args, 0, 10)),
        _ => Box()
    };

    private static float Arg(float[] args, int index, float fallback) => index < args.Length ? args[index] : fallback;

    public static Mesh Sphere(int segments, int rings)
    {
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        for (var ring = 0; ring <= rings; ring++)
        {
            var v = ring / (float) rings;
            var phi = v * Mathf.PI;
            for (var segment = 0; segment <= segments; segment++)
            {
                var u = segment / (float) segments;
                var theta = u * Mathf.PI * 2;
                vertices.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)));
                uvs.Add(new Vector2(u, 1 - v));
            }
        }

        for (var ring = 0; ring < rings; ring++)
        {
            for (var segment = 0; segment < segments; segment++)
            {
                var a = ring * (segments + 1) + segment;
                var b = a + segments + 1;
                triangles.AddRange(new[] {a, a + 1, b, b, a + 1, b + 1});
            }
        }

        return Finish(vertices, uvs, triangles);
    }

    public static Mesh Cylinder(int segments, float radius, float height)
    {
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        var half = height / 2;

        for (var segment = 0; segment <= segments; segment++)
        {
            var u = segment / (float) segments;
            var angle = u * Mathf.PI * 2;
            var x = Mathf.Cos(angle) * radius;
            var z = Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, -half, z));
            vertices.Add(new Vector3(x, half, z));
            uvs.Add(new Vector2(u, 0));
            uvs.Add(new Vector2(u, 0.15f));
        }

        for (var segment = 0; segment < segments; segment++)
        {
            var a = segment * 2;
            triangles.AddRange(new[] {a, a + 1, a + 2, a + 2, a + 1, a + 3});
        }

        foreach (var y in new[] {half, -half})
        {
            var center = vertices.Count;
            vertices.Add(new Vector3(0, y, 0));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (var segment = 0; segment <= segments; segment++)
            {
                var angle = segment / (float) segments * Mathf.PI * 2;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }

            for (var segment = 0; segment < segments; segment++)
            {
                var a = center + 1 + segment;
                triangles.AddRange(y > 0 ? new[] {center, a + 1, a} : new[] {center, a, a + 1});
            }
        }

        return Finish(vertices, uvs, triangles);
    }

    public static Mesh Cone(int segments)
    {
        var vertices = new List<Vector3> {new(0, 1, 0)};
        var uvs = new List<Vector2> {new(0.5f, 1)};
        var triangles = new List<int>();
        for (var segment = 0; segment <= segments; segment++)
        {
            var angle = segment / (float) segments * Mathf.PI * 2;
            vertices.Add(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)));
            uvs.Add(new Vector2(segment / (float) segments, 0));
        }

        for (var segment = 0; segment < segments; segment++) triangles.AddRange(new[] {0, segment + 2, segment + 1});
        return Finish(vertices, uvs, triangles);
    }

    public static Mesh Box()
    {
        var corners = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        var faces = new[] {Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back};
        foreach (var normal in faces)
        {
            var side = Vector3.Cross(normal, normal == Vector3.up || normal == Vector3.down ? Vector3.forward : Vector3.up);
            var up = Vector3.Cross(side, normal);
            var start = corners.Count;
            corners.Add((normal - side - up) * 0.5f);
            corners.Add((normal + side - up) * 0.5f);
            corners.Add((normal + side + up) * 0.5f);
            corners.Add((normal - side + up) * 0.5f);
            uvs.AddRange(new[] {new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)});
            triangles.AddRange(new[] {start, start + 2, start + 1, start, start + 3, start + 2});
        }

        return Finish(corners, uvs, triangles);
    }

    private static Mesh Finish(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
    {
        var mesh = new Mesh
        {
            vertices = new Il2CppStructArray<Vector3>(vertices.ToArray()),
            uv = new Il2CppStructArray<Vector2>(uvs.ToArray()),
            triangles = new Il2CppStructArray<int>(triangles.ToArray())
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return mesh;
    }
}
