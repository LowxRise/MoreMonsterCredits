using System.IO;
using System.Reflection;
using UnityEngine;

namespace MoreMonsterCredits
{
    internal static class SettingsIcon
    {
        internal static Sprite Load(string resourceName)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;
                var data = new byte[stream.Length];
                int offset = 0;
                while (offset < data.Length)
                {
                    int read = stream.Read(data, offset, data.Length - offset);
                    if (read == 0)
                        throw new EndOfStreamException(resourceName);
                    offset += read;
                }
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(data))
                    return null;
                texture.name = "MoreMonsterCredits settings icon";
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
    }
}
