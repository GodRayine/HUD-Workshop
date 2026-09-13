using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace HudWorkshop;

public sealed record SharedElement(string Id, float X, float Y, float? Scale, bool Enabled, int? Shape, bool? Simple);
public sealed record SharedLayout(int Version, int Width, int Height, SharedElement[] Elements);

public static class LayoutCode
{
    public const int MaxText = 65536;
    private const int MaxJson = 131072;
    public static void Validate(SharedLayout layout)
    {
        if (layout.Version != 1) throw new FormatException("Эта версия строки не поддерживается.");
        if (layout.Width is < 320 or > 32767 || layout.Height is < 200 or > 32767
            || layout.Elements == null || layout.Elements.Length is < 1 or > 256)
            throw new FormatException("Некорректные размеры или список элементов.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in layout.Elements)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Id) || e.Id.Length > 80 || !ids.Add(e.Id)
                || !float.IsFinite(e.X) || !float.IsFinite(e.Y) || e.X < short.MinValue || e.X > short.MaxValue
                || e.Y < short.MinValue || e.Y > short.MaxValue
                || (e.Scale is {} scale && (!float.IsFinite(scale) || scale < .6f || scale > 2f))
                || (e.Shape is {} shape && (shape < 0 || shape > 5)) || (e.Shape != null && e.Simple != null))
                throw new FormatException("Строка содержит некорректный или повторяющийся элемент.");
        }
    }
    public static string Encode(SharedLayout layout)
    {
        Validate(layout);
        var json = JsonSerializer.SerializeToUtf8Bytes(layout);
        if (json.Length > MaxJson) throw new FormatException("Раскладка слишком большая.");
        using var buffer = new MemoryStream();
        using (var zip = new GZipStream(buffer, CompressionLevel.SmallestSize, true)) zip.Write(json);
        var data = buffer.ToArray();
        var result = "HUDW1:" + Convert.ToBase64String(data) + ":" + Convert.ToHexString(SHA256.HashData(data));
        if (result.Length > MaxText) throw new FormatException("Раскладка слишком большая.");
        return result;
    }
    public static SharedLayout Decode(string text)
    {
        if (text.Length > MaxText) throw new FormatException("Строка слишком длинная.");
        var parts = text.Trim().Split(':');
        if (parts.Length != 3 || parts[0] != "HUDW1") throw new FormatException("Нужна строка HUDW1:… из HUD Workshop.");
        var data = Convert.FromBase64String(parts[1]);
        var hash = Convert.FromHexString(parts[2]);
        if (!CryptographicOperations.FixedTimeEquals(hash, SHA256.HashData(data))) throw new FormatException("Строка повреждена: контрольная сумма не совпадает.");
        using var input = new MemoryStream(data);
        using var zip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = zip.Read(chunk)) > 0)
        {
            if (output.Length + read > MaxJson) throw new FormatException("Распакованная раскладка слишком большая.");
            output.Write(chunk, 0, read);
        }
        var layout = JsonSerializer.Deserialize<SharedLayout>(output.ToArray(), new JsonSerializerOptions { MaxDepth = 16 })
            ?? throw new FormatException("Пустая раскладка.");
        Validate(layout);
        return layout;
    }
}
