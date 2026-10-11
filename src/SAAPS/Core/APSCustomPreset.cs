using System.Text.Json;

namespace SprocketAdvancedAPS.Core;

internal sealed record APSCustomSettings
{
    public int Version { get; init; } = 1;
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Custom APS";
    public string BaseProfile { get; init; } = "trophy";
    public bool Enabled { get; init; } = true;
    public int Type { get; init; }
    public bool SpeedBased { get; init; } = true;
    public int Threats { get; init; } = 2047;
    public float DetectionRange { get; init; } = 100;
    public float DetectionHorizontal { get; init; } = 360;
    public float DetectionVertical { get; init; } = 90;
    public float RangeMin { get; init; } = 2;
    public float RangeMax { get; init; } = 30;
    public float Horizontal { get; init; } = 120;
    public float Vertical { get; init; } = 60;
    public float JamHorizontal { get; init; } = 30;
    public float JamVertical { get; init; } = 20;
    public float Reaction { get; init; } = .05f;
    public float MinSpeed { get; init; } = 50;
    public float MaxSpeed { get; init; } = 2000;
    public int Capacity { get; init; } = 4;
    public float Reload { get; init; }
    public float Cooldown { get; init; } = .5f;
    public int Concurrent { get; init; } = 2;
    public float[] Intercept { get; init; } = Enumerable.Repeat(.8f, 11).ToArray();
    public float[] Jam { get; init; } = Enumerable.Repeat(.6f, 11).ToArray();
    public string Sound { get; init; } = "shot";

    internal static APSCustomSettings Parse(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 16384) throw new InvalidDataException("Custom APS settings too large.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        return Parse(document.RootElement);
    }
    internal static APSCustomSettings Parse(JsonElement element)
    {
        APSCustomPreset.RequireFields(element, typeof(APSCustomSettings).GetProperties().Select(p => p.Name));
        var settings = element.Deserialize<APSCustomSettings>() ?? throw new InvalidDataException("Missing settings.");
        settings.Validate();
        return settings;
    }

    internal void Validate()
    {
        if (Version != 1 || !Guid.TryParseExact(Id, "N", out _) || string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || Name.Any(char.IsControl))
            throw new InvalidDataException("Invalid Custom APS version, ID or name.");
        if (!Profiles.Contains(BaseProfile) || Type is < 0 or > 2 || Threats < 0 || (Threats & ~2047) != 0 || !Sounds.Contains(Sound))
            throw new InvalidDataException("Unknown Custom APS profile, type, threat mask or sound.");
        Range(DetectionRange, .1f, 2000); Range(RangeMin, 0, 2000); Range(RangeMax, .1f, 2000);
        if (RangeMin > RangeMax || RangeMax > DetectionRange) throw new InvalidDataException("Require minimum range <= maximum range <= detection range.");
        foreach (float f in new[] { DetectionHorizontal, Horizontal, JamHorizontal }) Range(f, .1f, 360);
        foreach (float f in new[] { DetectionVertical, Vertical, JamVertical }) Range(f, .1f, 180);
        Range(Reaction, 0, 30); Range(MinSpeed, 0, 10000); Range(MaxSpeed, .1f, 10000);
        if (MinSpeed > MaxSpeed) throw new InvalidDataException("Minimum speed exceeds maximum speed.");
        Range(Reload, 0, 3600); Range(Cooldown, .01f, 3600);
        if (Capacity is < 1 or > 256 || Concurrent is < 1 or > 16) throw new InvalidDataException("Capacity must be 1..256; concurrency 1..16.");
        foreach (var probabilities in new[] { Intercept, Jam })
        {
            if (probabilities == null || probabilities.Length != 11) throw new InvalidDataException("Expected 11 threat probabilities.");
            foreach (float f in probabilities) Range(f, 0, 1);
        }
    }
    private static void Range(float value, float min, float max)
    {
        if (!float.IsFinite(value) || value < min || value > max) throw new InvalidDataException($"Value must be finite and within {min}..{max}.");
    }
    internal static readonly string[] Profiles = { "drozd1", "drozd2", "arena", "arenam", "afganit", "shtora1", "trophy", "ironfist", "amapads", "zaslin" };
    internal static readonly string[] Sounds = { "shot", "rocket", "launcher" };
}

internal sealed record APSCustomPreset(APSCustomSettings Settings, JsonElement Geometry)
{
    internal const int MaxBytes = 2 * 1024 * 1024;
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true, MaxDepth = 32 };

    internal string Serialize()
    {
        Validate();
        string json = JsonSerializer.Serialize(this, Json);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException("Preset exceeds 2 MiB.");
        return json;
    }
    internal static APSCustomPreset Parse(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException("Preset exceeds 2 MiB.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        RequireFields(document.RootElement, new[] { "Settings", "Geometry" });
        var preset = new APSCustomPreset(APSCustomSettings.Parse(document.RootElement.GetProperty("Settings")), document.RootElement.GetProperty("Geometry").Clone());
        preset.Validate();
        return preset;
    }
    internal static void RequireFields(JsonElement element, IEnumerable<string> fields)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected JSON object.");
        var remaining = new HashSet<string>(fields, StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!remaining.Remove(property.Name)) throw new InvalidDataException("Unknown or duplicate field: " + property.Name);
        if (remaining.Count != 0) throw new InvalidDataException("Missing fields: " + string.Join(", ", remaining));
    }
    internal void Validate()
    {
        if (Settings == null) throw new InvalidDataException("Missing APS settings.");
        Settings.Validate();
        ValidateGeometry(Geometry);
    }
    internal static void ValidateGeometry(JsonElement geometry)
    {
        try { CheckGeometry(geometry); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException("Malformed native plate geometry.", ex); }
    }
    private static void CheckGeometry(JsonElement geometry)
    {
        RejectDuplicateFields(geometry);
        if (geometry.ValueKind != JsonValueKind.Object || geometry.GetProperty("format").GetString() != "freeform")
            throw new InvalidDataException("Expected a native freeform plate mesh.");
        var mesh = geometry.GetProperty("mesh");
        var vertices = mesh.GetProperty("vertices");
        var faces = mesh.GetProperty("faces");
        int coordinates = vertices.GetArrayLength();
        int count = coordinates / 3;
        if (coordinates % 3 != 0 || count < 4 || count > 8192 || faces.GetArrayLength() is < 4 or > 8192) throw new InvalidDataException("Mesh count out of bounds.");
        foreach (var coordinate in vertices.EnumerateArray())
            if (!coordinate.TryGetSingle(out float value) || !float.IsFinite(value) || Math.Abs(value) > 20)
                throw new InvalidDataException("Vertex outside finite +/-20 m local bounds.");
        int triangles = 0;
        foreach (var face in faces.EnumerateArray())
        {
            var indices = face.GetProperty("v");
            int corners = indices.GetArrayLength();
            if (corners is < 3 or > 256) throw new InvalidDataException("Invalid face size.");
            var used = new HashSet<int>();
            foreach (var index in indices.EnumerateArray())
                if (!index.TryGetInt32(out int i) || i < 0 || i >= count || !used.Add(i)) throw new InvalidDataException("Invalid or repeated face index.");
            int[] cornersArray = indices.EnumerateArray().Select(i => i.GetInt32()).ToArray();
            for (int j = 2; j < corners; j++)
            {
                int a = 3 * cornersArray[0], b = 3 * cornersArray[j - 1], c = 3 * cornersArray[j];
                double ux = vertices[b].GetDouble() - vertices[a].GetDouble(), uy = vertices[b + 1].GetDouble() - vertices[a + 1].GetDouble(), uz = vertices[b + 2].GetDouble() - vertices[a + 2].GetDouble();
                double vx = vertices[c].GetDouble() - vertices[a].GetDouble(), vy = vertices[c + 1].GetDouble() - vertices[a + 1].GetDouble(), vz = vertices[c + 2].GetDouble() - vertices[a + 2].GetDouble();
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                if (nx * nx + ny * ny + nz * nz < 1e-20) throw new InvalidDataException("Degenerate plate triangle.");
            }
            var thickness = face.GetProperty("t");
            if (thickness.GetArrayLength() != corners) throw new InvalidDataException("Face thickness count mismatch.");
            foreach (var t in thickness.EnumerateArray())
                if (!t.TryGetInt32(out int value) || value < 1 || value > 1000) throw new InvalidDataException("Plate thickness must be 1..1000 mm.");
            triangles += corners - 2;
        }
        if (triangles > 8000) throw new InvalidDataException("Preset exceeds 8,000 triangles.");
        if (mesh.TryGetProperty("edges", out var edges))
        {
            if (edges.GetArrayLength() > 16384) throw new InvalidDataException("Too many edges.");
            if (edges.GetArrayLength() % 2 != 0) throw new InvalidDataException("Invalid edge pairs.");
            foreach (var index in edges.EnumerateArray())
                if (!index.TryGetInt32(out int i) || i < 0 || i >= count) throw new InvalidDataException("Invalid edge index.");
        }
        // ponytail: native rivet/packed face schema remains native-owned; bound input and reject faulted native blueprints before apply.
    }
    private static void RejectDuplicateFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate geometry field: " + property.Name);
                RejectDuplicateFields(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateFields(item);
    }
    internal static string PathFor(string folder, string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Invalid preset ID.");
        return Path.Combine(folder, id + ".json");
    }
    internal void Save(string folder, bool overwrite)
    {
        string json = Serialize();
        Directory.CreateDirectory(folder);
        string path = PathFor(folder, Settings.Id);
        if (File.Exists(path) && !overwrite) throw new IOException("Preset already exists; confirm overwrite or Save as new.");
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
                stream.Write(data); stream.Flush(true);
            }
            if (overwrite && File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    internal static APSCustomPreset Load(string path) => Parse(new System.Text.UTF8Encoding(false, true).GetString(Read(path)));

    internal static APSCustomSettings ReadSettings(string path)
    {
        var reader = new Utf8JsonReader(Read(path), new JsonReaderOptions { MaxDepth = 32 });
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) throw new InvalidDataException("Expected preset object.");
        APSCustomSettings? settings = null;
        bool geometry = false;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new InvalidDataException("Expected preset field.");
            string field = reader.GetString()!;
            if (!reader.Read()) throw new InvalidDataException("Missing preset value.");
            if (field == "Settings" && settings == null)
            {
                using var document = JsonDocument.ParseValue(ref reader);
                settings = APSCustomSettings.Parse(document.RootElement);
            }
            else if (field == "Geometry" && !geometry) { geometry = true; reader.Skip(); }
            else throw new InvalidDataException("Unknown or duplicate preset field: " + field);
        }
        if (settings == null || !geometry || reader.TokenType != JsonTokenType.EndObject || reader.Read())
            throw new InvalidDataException("Incomplete preset or trailing JSON.");
        return settings;
    }

    private static byte[] Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxBytes) throw new InvalidDataException("Preset exceeds 2 MiB.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        int offset = 0;
        while (offset < bytes.Length)
        {
            int read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) throw new EndOfStreamException("Truncated preset.");
            offset += read;
        }
        if (stream.ReadByte() != -1) throw new InvalidDataException("Preset changed while reading.");
        return bytes;
    }
}
