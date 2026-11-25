using System;
using System.Collections;
using System.IO;
using BTD_Mod_Helper.Extensions;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Data.Boss;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Tests;

/// <summary>
/// The website shows the bosses' portraits (#206), the same ones the Boss Creator shows
/// (<see cref="BossEditorSection.Portrait"/>). They are Ninja Kiwi's art, taken from the game by this test.
/// <br/>
/// Started with the environment variable <c>CBC_PORTRAITS=&lt;folder&gt;</c>, this saves each boss's normal and Elite
/// portrait there as <c>&lt;Boss&gt;.png</c> and <c>&lt;Boss&gt;Elite.png</c>; <c>web/scripts/portraits.py</c> then makes
/// the website's small .webp files from them (see web/public/bosses/README.md). Run it windowed: the pictures are
/// copied on the graphics card. Otherwise it checks that every portrait still loads, so a game update that renames
/// one is noticed.
/// </summary>
public class BossPortraitsTest : CustomBossTest
{
    public const string EnvironmentVariable = "CBC_PORTRAITS";

    private const float PortraitTimeout = 15;

    protected override IEnumerator Run()
    {
        var folder = System.Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        // Under a canvas like the Boss Creator's buttons: an image elsewhere only gets sprites that are loaded already
        var canvas = AssertNotNull(Object.FindObjectOfType<Canvas>(), "No canvas on the main menu")!;
        var holder = new GameObject("CBC portraits test", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        holder.transform.SetParent(canvas.transform, false);
        try
        {
            foreach (var boss in Enum.GetValues<BossType>())
            {
                foreach (var elite in new[] {false, true})
                {
                    var name = boss + (elite ? "Elite" : "");
                    var image = new GameObject(name, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>()).AddComponent<Image>();
                    image.transform.SetParent(holder.transform, false);
                    image.SetSprite(BossEditorSection.Portrait(boss, elite));
                    var start = Time.realtimeSinceStartup;
                    while (image.sprite == null && Time.realtimeSinceStartup - start < PortraitTimeout) yield return null;
                    var sprite = AssertNotNull(image.sprite, $"{name}'s portrait didn't load")!;
                    Assert(sprite.rect.width > 0 && sprite.rect.height > 0, $"{name}'s portrait is empty");

                    if (!string.IsNullOrEmpty(folder))
                    {
                        var path = Path.Combine(folder, name + ".png");
                        File.WriteAllBytes(path, ImageConversion.EncodeToPNG(Copy(sprite)));
                        Log($"wrote {path} ({sprite.rect.width:0}x{sprite.rect.height:0})");
                    }
                }
            }
        }
        finally
        {
            Object.Destroy(holder);
        }
    }

    /// <summary>
    /// A readable copy of a sprite's picture. Sprites sit in atlases on the graphics card that can't be read, so its
    /// part of the atlas is drawn into a render texture and read back from there.
    /// </summary>
    private static Texture2D Copy(Sprite sprite)
    {
        var texture = sprite.texture;
        var rect = sprite.textureRect;
        var width = Mathf.RoundToInt(rect.width);
        var height = Mathf.RoundToInt(rect.height);
        var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var active = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, target,
                new Vector2(rect.width / texture.width, rect.height / texture.height),
                new Vector2(rect.x / texture.width, rect.y / texture.height));
            RenderTexture.active = target;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            copy.Apply();
            return copy;
        }
        finally
        {
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
