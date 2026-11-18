"""
Writes Gildebloon's pile of money, as built by gildebloon-pile.py in the open Blender, to Bundles/gildebloon-pile.mesh.gz
for the mod (#25). Run inside Blender: python tools/blender/run-in-blender.py tools/blender/export-pile.py

The game can't load a model from an asset bundle (it crashes, 57.4), so the mod builds the mesh itself from this file
(Visuals/GildebloonPileMesh.cs). The toon look is baked: each corner gets a UV into a ramp texture the mod makes, with
x how lit it is by the light below and y the material's row. The outlines are the shells of the Solidify modifiers.

Format, little-endian, gzipped: "GPM1", then per mesh (Pile, Sparkle) its name (byte length + UTF-8), vertex count,
positions (3 floats, Unity's axes: x, z, y of Blender's), UVs (2 floats), index count, indices (int32, clockwise like
Unity's front faces); then the sparkle count and per sparkle its position (3 floats) and size (float).
"""
import gzip, math, os, struct
import bpy
from mathutils import Vector

# Rows of the ramp texture, in the order of GildebloonPileMesh.Rows
ROWS = ["Gilde_Gold", "Gilde_GoldFace", "Gilde_GoldInner", "Gilde_Sack", "Gilde_Rope", "Gilde_Dollar", "Gilde_Outline",
        "Gilde_Sparkle"]
# The light the bands are baked for: from the top left of the game's screen, towards the camera (Blender's axes)
LIGHT = Vector((-0.55, -0.35, 0.75)).normalized()
AMBIENT = 0.06

ROOT_NAME = "GildebloonMoneyPile"
# REPO is set by run-in-blender.py
OUT = os.path.join(REPO, "Bundles", "gildebloon-pile.mesh.gz")


def unity(v):
    return (v.x, v.z, v.y)


def collect(objects, root):
    dg = bpy.context.evaluated_depsgraph_get()
    to_root = root.matrix_world.inverted()
    verts, uvs, index, tris = [], [], {}, []
    flips = [0, 0]
    for obj in objects:
        ev = obj.evaluated_get(dg)
        me = ev.to_mesh()
        m = to_root @ obj.matrix_world
        nm = m.to_3x3().inverted().transposed()
        me.calc_loop_triangles()
        normals = me.corner_normals
        for tri in me.loop_triangles:
            mat = me.materials[tri.material_index] if me.materials else None
            name = mat.name.split(".")[0] if mat else "Gilde_Gold"
            row = ROWS.index(name) if name in ROWS else 0
            ids = []
            for loop in tri.loops:
                co = m @ me.vertices[me.loops[loop].vertex_index].co
                n = (nm @ normals[loop].vector).normalized()
                lit = min(0.999, max(0.001, AMBIENT + max(0.0, n.dot(LIGHT))))
                if name in ("Gilde_Outline", "Gilde_Sparkle"):
                    lit = 0.5
                u = (round(lit, 3), (row + 0.5) / len(ROWS))
                p = unity(co)
                key = (round(p[0], 5), round(p[1], 5), round(p[2], 5), u)
                if key not in index:
                    index[key] = len(verts)
                    verts.append(p)
                    uvs.append(u)
                ids.append(index[key])
            # Unity's front faces turn clockwise: check against the face's own normal, in Unity's axes
            a, b, c = (Vector(verts[i]) for i in ids)
            face = Vector(unity((nm @ tri.normal).normalized()))
            if (b - a).cross(c - a).dot(face) > 0:
                flips[0] += 1
            else:
                flips[1] += 1
            tris.append(ids)
        ev.to_mesh_clear()
    return verts, uvs, tris, flips


def write_mesh(f, name, verts, uvs, tris, flip):
    raw = name.encode()
    f.write(struct.pack("<B", len(raw)) + raw)
    f.write(struct.pack("<i", len(verts)))
    for p in verts:
        f.write(struct.pack("<3f", *p))
    for u in uvs:
        f.write(struct.pack("<2f", *u))
    f.write(struct.pack("<i", len(tris) * 3))
    for a, b, c in tris:
        f.write(struct.pack("<3i", *((a, c, b) if flip else (a, b, c))))


root = bpy.data.objects[ROOT_NAME]
parts = [o for o in root.children_recursive if o.type == "MESH" and not o.name.startswith("Sparkle")]
sparkles = [o for o in root.children_recursive if o.name.startswith("Sparkle")]

pile = collect(parts, root)
# A face is a front face in Unity when cross(b - a, c - a) points out of it; flip the order if most aren't
flip = pile[3][1] > pile[3][0]

spark_me = sparkles[0].data
spark_verts = [unity(v.co) for v in spark_me.vertices]
spark_uvs = [(0.5, (ROWS.index("Gilde_Sparkle") + 0.5) / len(ROWS))] * len(spark_verts)
spark_tris = [tuple(p.vertices) for p in spark_me.polygons]

out = OUT
with gzip.open(out, "wb", compresslevel=9) as f:
    f.write(b"GPM1")
    write_mesh(f, "Pile", *pile[:3], flip)
    # The sparkle lies in Blender's XY plane, facing +Z; in Unity it faces +Y, and the mod turns it to the camera
    write_mesh(f, "Sparkle", spark_verts, spark_uvs, spark_tris, flip)
    f.write(struct.pack("<i", len(sparkles)))
    for s in sparkles:
        f.write(struct.pack("<4f", *unity(s.location), s.scale.x))

result = {"file": out, "vertices": len(pile[0]), "triangles": len(pile[2]), "winding": pile[3], "flipped": flip,
          "bytes": os.path.getsize(out), "sparkles": len(sparkles)}
