using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Makes Gildebloon's look (#25): ../Bundles/gildebloon.layout.json with its parts, and its textures and portraits
/// in ../Images. Everything is
/// made here in code, so it can be built again and its changes read in a diff:
/// Unity.exe -batchmode -quit -projectPath UnityProject -executeMethod BuildGildebloon.Build -logFile -
/// (without -nographics, which the portraits need).
/// <br/>
/// The prefab keeps to a contract the mod's Visuals/GildebloonLook.cs relies on: a root with the children Hull,
/// VaultDoor, Dial, BagLeft, BagRight and CoinRing, looked up by name. The game looks down on its bosses, so the door
/// with its dial is on top (+Y). The game can't run scripts from a bundle, so the mod moves the parts itself. A model
/// made by hand later (an FBX in Assets/Gildebloon/Model) can take the procedural one's place if it keeps the names.
/// </summary>
public static class BuildGildebloon
{
    public const string Folder = "Assets/Gildebloon";
    public static readonly string[] Parts = {"Hull", "VaultDoor", "Dial", "BagLeft", "BagRight", "CoinRing"};

    [MenuItem("Custom Boss Challenge/Build Gildebloon")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        BuildTierLook.UsePipelinePublic();

        var prefabs = new List<string>();
        foreach (var elite in new[] {false, true})
        {
            prefabs.Add(MakePrefab(elite));
        }

        // Only meshes and textures go in the bundle: loading a prefab (or a shader the game has itself) from a bundle
        // crashes the game, so the mod builds the parts from a layout file instead
        // Nothing from a bundle: loading a prefab, mesh or texture from one crashes the game (57.4), so the mod makes the
        // meshes itself from the layout and loads the textures from Images/ like its other pictures
        WriteLayout(prefabs);
        var images = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Images"));
        Directory.CreateDirectory(images);
        foreach (var texture in Textures)
        {
            File.Copy(TexturePath(texture), Path.Combine(images, $"Gildebloon{texture}Texture.png"), true);
        }

        foreach (var elite in new[] {false, true})
        {
            RenderPortrait(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(elite)), elite);
        }
    }

    /// <summary>
    /// Writes ../Bundles/gildebloon.layout.json: for each prefab its parts in order, with the index of their parent,
    /// the mesh they show (its name, which starts with its texture, and its shape) and their local position, rotation
    /// and scale. Parents come before children.
    /// </summary>
    private static void WriteLayout(List<string> prefabs)
    {
        var text = new System.Text.StringBuilder("{\n");
        for (var p = 0; p < prefabs.Count; p++)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[p]);
            var parts = root.GetComponentsInChildren<Transform>(true);
            var index = new Dictionary<Transform, int>();
            text.Append($"  \"{root.name}\": [\n");
            for (var i = 0; i < parts.Length; i++)
            {
                var t = parts[i];
                index[t] = i;
                var parent = t.parent != null && index.TryGetValue(t.parent, out var at) ? at : -1;
                var mesh = t.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                var shape = mesh != null && Shapes.TryGetValue(mesh.name, out var found) ? found : ("", new float[0]);
                var meshText = mesh == null ? ""
                    : $"\"mesh\": \"{mesh.name}\", \"kind\": \"{shape.Item1}\", \"args\": [{string.Join(", ", System.Array.ConvertAll(shape.Item2, F))}], ";
                var r = t.localRotation;
                text.Append($"    {{\"name\": \"{t.name}\", \"parent\": {parent}, {meshText}" +
                            $"\"p\": [{F(t.localPosition.x)}, {F(t.localPosition.y)}, {F(t.localPosition.z)}], " +
                            $"\"r\": [{F(r.x)}, {F(r.y)}, {F(r.z)}, {F(r.w)}], " +
                            $"\"s\": [{F(t.localScale.x)}, {F(t.localScale.y)}, {F(t.localScale.z)}]}}" +
                            (i < parts.Length - 1 ? ",\n" : "\n"));
            }

            text.Append(p < prefabs.Count - 1 ? "  ],\n" : "  ]\n");
        }

        text.Append("}\n");
        var target = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Bundles", "gildebloon.layout.json"));
        File.WriteAllText(target, text.ToString().Replace("\r\n", "\n"));
        Debug.Log($"BuildGildebloon: wrote {target}");
    }

    private static string F(float value) => value.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);

    private static string PrefabPath(bool elite) => $"{Folder}/{(elite ? "GildebloonElite" : "Gildebloon")}.prefab";

    // ---------- The model ----------

    private static string MakePrefab(bool elite)
    {
        GoldTexture(elite);
        IronTexture(elite);
        SackTexture();
        var gold = elite ? "GoldElite" : "Gold";
        var dark = elite ? "IronElite" : "Iron";
        const string sack = "Sack";

        var root = new GameObject(elite ? "GildebloonElite" : "Gildebloon");

        // The hull: a squat gold blimp with plate seams in its texture
        var hull = Part("Hull", root.transform, Mesh(gold, "Sphere", "sphere", 32, 20), gold);
        // Longer along the track (+Z, the way it moves) than across it, where the bags hang
        hull.transform.localScale = new Vector3(1f, 0.75f, 1.35f);

        // A ring of rivets around its waist
        var rivet = Mesh(dark, "Rivet", "sphere", 8, 6);
        for (var i = 0; i < 28; i++)
        {
            var angle = i * Mathf.PI * 2 / 28;
            var r = Part($"Rivet{i}", hull.transform, rivet, dark);
            r.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.99f, 0, Mathf.Sin(angle) * 0.99f);
            r.transform.localScale = Vector3.one * 0.05f;
        }

        // The vault door on top, with bolts around it and a dial that spins
        var door = Part("VaultDoor", root.transform, Mesh(dark, "Disc", "cylinder", 40, 1f, 0.12f), dark);
        door.transform.localPosition = new Vector3(0, 0.7f, 0);
        door.transform.localScale = new Vector3(0.62f, 1, 0.62f);
        var bolt = Mesh(gold, "Bolt", "cylinder", 10, 1f, 1f);
        for (var i = 0; i < 12; i++)
        {
            var angle = i * Mathf.PI * 2 / 12;
            var b = Part($"Bolt{i}", door.transform, bolt, gold);
            b.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.86f, 0.1f, Mathf.Sin(angle) * 0.86f);
            b.transform.localScale = new Vector3(0.09f, 0.08f, 0.09f);
        }

        var dial = Part("Dial", root.transform, Mesh(gold, "DialDisc", "cylinder", 24, 1f, 0.2f), gold);
        dial.transform.localPosition = new Vector3(0, 0.82f, 0);
        dial.transform.localScale = new Vector3(0.22f, 1, 0.22f);
        var spoke = Mesh(dark, "Spoke", "box");
        for (var i = 0; i < 3; i++)
        {
            var s = Part($"Spoke{i}", dial.transform, spoke, dark);
            s.transform.localPosition = new Vector3(0, 0.18f, 0);
            s.transform.localRotation = Quaternion.Euler(0, i * 60, 0);
            s.transform.localScale = new Vector3(2.6f, 0.12f, 0.22f);
        }

        // Money bags strapped to its sides, with a dollar patch and a tied neck
        foreach (var (name, side) in new[] {("BagLeft", -1f), ("BagRight", 1f)})
        {
            var bag = Part(name, root.transform, Mesh(sack, "Bag", "sphere", 20, 14), sack);
            bag.transform.localPosition = new Vector3(side * 1.45f, 0.2f, 0);
            bag.transform.localScale = new Vector3(0.42f, 0.5f, 0.42f);
            var neck = Part("Neck", bag.transform, Mesh(sack, "Neck", "cylinder", 12, 1f, 1f), sack);
            neck.transform.localPosition = new Vector3(0, 1f, 0);
            neck.transform.localScale = new Vector3(0.35f, 0.3f, 0.35f);
            var strap = Part("Strap", bag.transform, Mesh(dark, "Strap", "box"), dark);
            strap.transform.localPosition = new Vector3(-side * 0.6f, 0.2f, 0);
            strap.transform.localScale = new Vector3(1.2f, 0.12f, 0.5f);
        }

        // Coins circling it, tilted a little so they catch the light
        var ring = new GameObject("CoinRing");
        ring.transform.SetParent(root.transform, false);
        var coin = Mesh(gold, "Coin", "cylinder", 16, 1f, 0.15f);
        var coins = elite ? 16 : 12;
        for (var i = 0; i < coins; i++)
        {
            var angle = i * Mathf.PI * 2 / coins;
            var c = Part($"Coin{i}", ring.transform, coin, gold);
            c.transform.localPosition = new Vector3(Mathf.Cos(angle) * 1.75f, 0.15f * Mathf.Sin(angle * 3), Mathf.Sin(angle) * 1.75f);
            c.transform.localRotation = Quaternion.Euler(70, -angle * Mathf.Rad2Deg, 0);
            c.transform.localScale = new Vector3(0.16f, 1, 0.16f);
        }

        // Elite wears a crown of spikes around the door
        if (elite)
        {
            var spike = Mesh(gold, "Spike", "cone", 10);
            for (var i = 0; i < 8; i++)
            {
                var angle = (i + 0.5f) * Mathf.PI * 2 / 8;
                var s = Part($"Spike{i}", root.transform, spike, gold);
                s.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.8f, 0.55f, Mathf.Sin(angle) * 0.8f);
                s.transform.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(Mathf.Cos(angle), 1.4f, Mathf.Sin(angle)));
                s.transform.localScale = new Vector3(0.14f, 0.35f, 0.14f);
            }
        }

        var path = PrefabPath(elite);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return path;
    }

    /// <summary>
    /// A part without a material: a shader in the bundle crashes the game when the prefab is loaded (it has URP's
    /// shaders itself), so the mod gives each part a material in the game, with the texture its mesh's name starts
    /// with ("Gold_Coin" gets Gold.png)
    /// </summary>
    private static GameObject Part(string name, Transform parent, Mesh mesh, string texture)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = null;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return part;
    }

    /// <summary>The textures the mod puts on the parts, by the start of their mesh's name</summary>
    public static readonly string[] Textures = {"Gold", "GoldElite", "Iron", "IronElite", "Sack"};

    private static string TexturePath(string name) => $"{Folder}/{name}.png";

    // ---------- Meshes ----------

    /// <summary>The shape of each mesh by name, for the layout: the mod makes the same meshes in the game</summary>
    private static readonly Dictionary<string, (string Kind, float[] Args)> Shapes = new();

    private static Mesh Mesh(string texture, string part, string kind, params float[] args)
    {
        var name = $"{texture}_{part}";
        Shapes[name] = (kind, args);
        var path = $"{Folder}/Mesh{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        var mesh = kind switch
        {
            "sphere" => Sphere((int) args[0], (int) args[1]),
            "cylinder" => Cylinder((int) args[0], args[1], args[2]),
            "cone" => Cone((int) args[0]),
            _ => Box()
        };
        mesh.name = name;
        mesh.RecalculateBounds();
        if (existing != null)
        {
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Mesh Sphere(int segments, int rings)
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

        return Finish(vertices, uvs, triangles, true);
    }

    private static Mesh Cylinder(int segments, float radius, float height)
    {
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        var half = height / 2;

        // Side
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

        // Caps, with the texture laid flat on them
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

        return Finish(vertices, uvs, triangles, false);
    }

    private static Mesh Cone(int segments)
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
        return Finish(vertices, uvs, triangles, false);
    }

    private static Mesh Box()
    {
        var mesh = new Mesh();
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

        return Finish(corners, uvs, triangles, false);
    }

    private static Mesh Finish(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, bool smooth)
    {
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------- Textures ----------

    private static Texture2D GoldTexture(bool elite) => Texture(elite ? "GoldElite" : "Gold", (u, v) =>
    {
        // Plates: seams every eighth around and every quarter up, with a soft shine across each plate
        var seamU = Mathf.Abs(Mathf.Repeat(u * 8, 1) - 0.5f) > 0.47f;
        var seamV = Mathf.Abs(Mathf.Repeat(v * 4, 1) - 0.5f) > 0.46f;
        var shine = 0.75f + 0.25f * Mathf.Sin(Mathf.Repeat(u * 8, 1) * Mathf.PI) * Mathf.Sin(Mathf.Repeat(v * 4, 1) * Mathf.PI);
        var grain = 0.92f + 0.08f * Mathf.PerlinNoise(u * 40, v * 40);
        var baseColor = elite ? new Color(0.55f, 0.36f, 0.08f) : new Color(1f, 0.78f, 0.22f);
        var color = baseColor * shine * grain;
        if (seamU || seamV) color *= elite ? 0.25f : 0.55f;
        // A band of red along the waist for Elite
        if (elite && Mathf.Abs(v - 0.5f) < 0.04f) color = new Color(0.65f, 0.08f, 0.06f);
        color.a = 1;
        return color;
    });

    private static Texture2D IronTexture(bool elite) => Texture(elite ? "IronElite" : "Iron", (u, v) =>
    {
        var dx = u - 0.5f;
        var dy = v - 0.5f;
        var r = Mathf.Sqrt(dx * dx + dy * dy);
        // Rings like a vault door's face
        var ring = 0.8f + 0.2f * Mathf.Sign(Mathf.Sin(r * 60));
        var tone = elite ? new Color(0.16f, 0.14f, 0.16f) : new Color(0.35f, 0.33f, 0.36f);
        var color = tone * ring * (0.9f + 0.1f * Mathf.PerlinNoise(u * 30, v * 30));
        color.a = 1;
        return color;
    });

    private static Texture2D SackTexture() => Texture("Sack", (u, v) =>
    {
        var weave = 0.85f + 0.15f * Mathf.PerlinNoise(u * 120, v * 120);
        var color = new Color(0.62f, 0.5f, 0.3f) * weave;
        // A green dollar patch on the front
        var dx = Mathf.Repeat(u * 2, 1) - 0.5f;
        var dy = v - 0.45f;
        if (Mathf.Abs(dx) < 0.18f && Mathf.Abs(dy) < 0.12f)
        {
            color = new Color(0.18f, 0.55f, 0.22f);
            var s = Mathf.Abs(dx) < 0.025f || Mathf.Abs(Mathf.Abs(dy) - 0.06f) < 0.015f && Mathf.Abs(dx) < 0.08f;
            if (s) color = new Color(0.95f, 0.9f, 0.6f);
        }

        color.a = 1;
        return color;
    });

    private static Texture2D Texture(string name, System.Func<float, float, Color> pixel)
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++) texture.SetPixel(x, y, pixel((x + 0.5f) / size, (y + 0.5f) / size));
        }

        texture.Apply();
        var path = $"{Folder}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------- Portraits ----------

    private static void RenderPortrait(GameObject prefab, bool elite)
    {
        const int size = 256;
        var model = Object.Instantiate(prefab);
        var materials = new Dictionary<string, Material>();
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            var key = renderer.GetComponent<MeshFilter>().sharedMesh.name.Split('_')[0];
            if (!materials.TryGetValue(key, out var material))
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(key)));
                materials[key] = material;
            }

            renderer.sharedMaterial = material;
        }

        model.transform.rotation = Quaternion.Euler(-35, 25, 0);
        var lightObject = new GameObject("Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        var cameraObject = new GameObject("Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0, 0, 0, 0);
        camera.orthographic = true;
        camera.orthographicSize = 2.1f;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.transform.LookAt(Vector3.zero);

        var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        image.Apply();
        RenderTexture.active = null;

        var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Images"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, elite ? "GildebloonElite.png" : "Gildebloon.png");
        File.WriteAllBytes(file, image.EncodeToPNG());
        Debug.Log($"BuildGildebloon: wrote {file}");

        Object.DestroyImmediate(model);
        Object.DestroyImmediate(lightObject);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(target);
    }
}
