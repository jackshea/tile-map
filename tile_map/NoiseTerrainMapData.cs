using System;
using System.Text.Json;
using Godot;

public sealed class NoiseTerrainMapData
{
    public const int Size = 1024;
    public const int SaveVersion = 3;

    public enum Terrain : byte { Water, Sand, Grass }

    public int Seed { get; private set; }
    public double NoiseFrequency { get; private set; } = 0.003;
    public double WaterThreshold { get; private set; }
    public int CoastWidth { get; set; } = 2;
    public int IterationStrength { get; private set; } = 1;
    public double ContinentScale { get; set; } = 0.45;
    public double ContinentWeight { get; set; } = 0.62;
    public double DetailScale { get; set; } = 2.5;
    public double DetailWeight { get; set; } = 0.09;
    public double WarpScale { get; set; } = 0.7;
    public double WarpStrength { get; set; } = 0.18;
    public double EdgeFalloff { get; set; }

    // Water/land is the editable logic layer. Sand is derived from its coastline.
    public byte[] LandMask { get; private set; } = Array.Empty<byte>();
    public byte[] TerrainCells { get; private set; } = Array.Empty<byte>();

    public void Generate(int seed, double frequency, double threshold, int beachWidth, int smoothingSteps)
    {
        Seed = seed;
        NoiseFrequency = frequency;
        WaterThreshold = threshold;
        CoastWidth = beachWidth;
        IterationStrength = smoothingSteps;
        LandMask = new byte[Size * Size];

        var continentNoise = MakeNoise(seed, frequency * ContinentScale);
        var coastNoise = MakeNoise(OffsetSeed(seed, 101), frequency);
        var detailNoise = MakeNoise(OffsetSeed(seed, 202), frequency * DetailScale);
        var warpNoise = MakeNoise(OffsetSeed(seed, 303), frequency * WarpScale);
        double warpDistance = WarpStrength / frequency;
        double coastWeight = 1.0 - ContinentWeight - DetailWeight;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                double sampleX = x;
                double sampleY = y;
                if (WarpStrength > 0.0)
                {
                    sampleX += warpNoise.GetNoise2D(x, y) * warpDistance;
                    sampleY += warpNoise.GetNoise2D(x + 4096, y - 4096) * warpDistance;
                }

                double elevation =
                    continentNoise.GetNoise2D((float)sampleX, (float)sampleY) * ContinentWeight
                    + coastNoise.GetNoise2D((float)sampleX, (float)sampleY) * coastWeight
                    + detailNoise.GetNoise2D((float)sampleX, (float)sampleY) * DetailWeight;
                if (EdgeFalloff > 0.0)
                {
                    double nx = (x / (double)(Size - 1) - 0.5) * 2.0;
                    double ny = (y / (double)(Size - 1) - 0.5) * 2.0;
                    elevation -= EdgeFalloff * (nx * nx + ny * ny) * 0.5;
                }
                LandMask[y * Size + x] = (byte)(elevation < threshold ? 0 : 1);
            }
        }
        RebuildTerrain();
    }

    public void Iterate()
    {
        for (int step = 0; step < IterationStrength; step++)
        {
            var next = (byte[])LandMask.Clone();
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int waterVotes = 0;
                    int landVotes = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if (!InBounds(nx, ny)) continue;
                            if (LandMask[ny * Size + nx] == 0) waterVotes++;
                            else landVotes++;
                        }
                    }
                    if (waterVotes > landVotes) next[y * Size + x] = 0;
                    else if (landVotes > waterVotes) next[y * Size + x] = 1;
                }
            }
            LandMask = next;
        }
        RebuildTerrain();
    }

    public int GetTerrainAt(Vector2I cell) =>
        InBounds(cell.X, cell.Y) ? TerrainCells[cell.Y * Size + cell.X] : -1;

    public string ToJson() => JsonSerializer.Serialize(new
    {
        version = SaveVersion,
        size = Size,
        seed = Seed,
        noise_frequency = NoiseFrequency,
        water_threshold = WaterThreshold,
        coast_width = CoastWidth,
        iteration_strength = IterationStrength,
        continent_scale = ContinentScale,
        continent_weight = ContinentWeight,
        detail_scale = DetailScale,
        detail_weight = DetailWeight,
        warp_scale = WarpScale,
        warp_strength = WarpStrength,
        edge_falloff = EdgeFalloff,
        land_mask_b64 = Convert.ToBase64String(LandMask),
    });

    public static bool TryFromJson(string text, out NoiseTerrainMapData? map)
    {
        map = null;
        try
        {
            using var document = JsonDocument.Parse(text);
            var record = document.RootElement;
            if (record.ValueKind != JsonValueKind.Object
                || !TryInt(record, "version", out int version)
                || !TryInt(record, "size", out int size)
                || size != Size || version < 1 || version > SaveVersion
                || !TryInt(record, "seed", out int seed)
                || !TryDouble(record, "noise_frequency", out double frequency)
                || !TryDouble(record, "water_threshold", out double threshold)
                || !TryInt(record, "coast_width", out int coastWidth)
                || !TryInt(record, "iteration_strength", out int iterationStrength))
                return false;

            var loaded = new NoiseTerrainMapData
            {
                Seed = seed,
                NoiseFrequency = frequency,
                WaterThreshold = threshold,
                CoastWidth = coastWidth,
                IterationStrength = iterationStrength,
            };
            if (version == SaveVersion)
            {
                if (!TryDouble(record, "continent_scale", out double continentScale)
                    || !TryDouble(record, "continent_weight", out double continentWeight)
                    || !TryDouble(record, "detail_scale", out double detailScale)
                    || !TryDouble(record, "detail_weight", out double detailWeight)
                    || !TryDouble(record, "warp_scale", out double warpScale)
                    || !TryDouble(record, "warp_strength", out double warpStrength)
                    || !TryDouble(record, "edge_falloff", out double edgeFalloff))
                    return false;
                loaded.ContinentScale = continentScale;
                loaded.ContinentWeight = continentWeight;
                loaded.DetailScale = detailScale;
                loaded.DetailWeight = detailWeight;
                loaded.WarpScale = warpScale;
                loaded.WarpStrength = warpStrength;
                loaded.EdgeFalloff = edgeFalloff;
            }
            if (!loaded.HasValidSettings()) return false;

            if (version >= 2)
            {
                if (!TryString(record, "land_mask_b64", out string? encoded)) return false;
                loaded.LandMask = Convert.FromBase64String(encoded!);
            }
            else
            {
                if (!TryString(record, "land_mask", out string? maskText)
                    || !TryString(record, "terrain_cells", out string? terrainText)
                    || maskText!.Length != Size * Size || terrainText!.Length != Size * Size)
                    return false;
                loaded.LandMask = new byte[Size * Size];
                for (int i = 0; i < loaded.LandMask.Length; i++)
                {
                    char digit = maskText[i];
                    if (digit != '0' && digit != '1') return false;
                    loaded.LandMask[i] = (byte)(digit - '0');
                }
            }
            if (loaded.LandMask.Length != Size * Size) return false;
            foreach (byte value in loaded.LandMask)
                if (value > 1) return false;

            loaded.RebuildTerrain();
            if (version == 1)
            {
                string terrainText = record.GetProperty("terrain_cells").GetString()!;
                for (int i = 0; i < loaded.TerrainCells.Length; i++)
                    if (terrainText[i] != (char)('0' + loaded.TerrainCells[i])) return false;
            }
            map = loaded;
            return true;
        }
        catch (JsonException) { return false; }
        catch (FormatException) { return false; }
    }

    public void RebuildTerrain()
    {
        int count = Size * Size;
        var distances = new int[count];
        int beyondCoast = CoastWidth + 1;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int index = y * Size + x;
                if (LandMask[index] == 0) continue;
                int distance = beyondCoast;
                if (x > 0) distance = Math.Min(distance, distances[index - 1] + 1);
                if (y > 0) distance = Math.Min(distance, distances[index - Size] + 1);
                distances[index] = distance;
            }
        }

        TerrainCells = new byte[count];
        for (int y = Size - 1; y >= 0; y--)
        {
            for (int x = Size - 1; x >= 0; x--)
            {
                int index = y * Size + x;
                if (LandMask[index] == 0)
                {
                    TerrainCells[index] = (byte)Terrain.Water;
                    continue;
                }
                int distance = distances[index];
                if (x < Size - 1) distance = Math.Min(distance, distances[index + 1] + 1);
                if (y < Size - 1) distance = Math.Min(distance, distances[index + Size] + 1);
                distances[index] = distance;
                TerrainCells[index] = (byte)(distance <= CoastWidth ? Terrain.Sand : Terrain.Grass);
            }
        }
    }

    private bool HasValidSettings() =>
        Seed >= 0
        && NoiseFrequency >= 0.0005 && NoiseFrequency <= 0.1
        && WaterThreshold >= -1.0 && WaterThreshold <= 1.0
        && CoastWidth >= 1 && CoastWidth <= 32
        && IterationStrength >= 1 && IterationStrength <= 4
        && ContinentScale >= 0.2 && ContinentScale <= 1.0
        && ContinentWeight >= 0.3 && ContinentWeight <= 0.85
        && DetailScale >= 1.0 && DetailScale <= 6.0
        && DetailWeight >= 0.0 && DetailWeight <= 0.15
        && WarpScale >= 0.2 && WarpScale <= 2.0
        && WarpStrength >= 0.0 && WarpStrength <= 0.5
        && EdgeFalloff >= 0.0 && EdgeFalloff <= 0.7;

    private static FastNoiseLite MakeNoise(int seed, double frequency) => new()
    {
        NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
        Seed = seed,
        Frequency = (float)frequency,
    };

    private static int OffsetSeed(int seed, int offset) => (int)(((long)seed + offset) % int.MaxValue);
    private static bool InBounds(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;

    private static bool TryInt(JsonElement record, string name, out int value)
    {
        value = 0;
        return record.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value);
    }

    private static bool TryDouble(JsonElement record, string name, out double value)
    {
        value = 0;
        return record.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetDouble(out value)
            && double.IsFinite(value);
    }

    private static bool TryString(JsonElement record, string name, out string? value)
    {
        value = null;
        if (!record.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString();
        return value != null;
    }
}
