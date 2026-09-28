using System;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

public partial class VerifyNoiseTerrainMap : Node
{
    private const string ScenePath = "res://tile_map/noise_terrain_map.tscn";
    private const string TestSavePath = "user://noise_terrain_map_csharp_verification.json";
    private int _failures;

    public override void _Ready()
    {
        CallDeferred(MethodName.Verify);
    }

    private void Verify()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        Check(packed != null, "Random map scene loads");
        if (packed == null) { GetTree().Quit(1); return; }

        var scene = packed.Instantiate<NoiseTerrainMap>();
        AddChild(scene);
        var data = scene.MapData;
        var layer = scene.GetNode<TileMapLayer>("Terrain");
        var overview = scene.GetNode<Sprite2D>("Overview");
        var camera = scene.GetNode<Camera2D>("Camera2D");
        Check(data.LandMask.Length == NoiseTerrainMapData.Size * NoiseTerrainMapData.Size,
            "Logic grid has 1024 x 1024 cells");
        Check(data.TerrainCells.Length == data.LandMask.Length, "Terrain grid matches logic");
        Check(overview.Visible && overview.Texture.GetSize() == new Vector2(1024, 1024),
            "Whole-map overview is visible");
        Check(layer.GetUsedCells().Count == 0, "Whole-map view does not allocate a million tiles");

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        float mapPixels = NoiseTerrainMapData.Size * 32;
        float screenLeft = viewportSize.X * 0.5f - camera.Position.X * camera.Zoom.X;
        float screenTop = viewportSize.Y * 0.5f - camera.Position.Y * camera.Zoom.Y;
        Check(screenLeft >= 260 && screenLeft + mapPixels * camera.Zoom.X <= viewportSize.X,
            "Whole map fits beside controls");
        Check(screenTop >= 0 && screenTop + mapPixels * camera.Zoom.Y <= viewportSize.Y,
            "Whole map fits vertically");
        var panel = scene.GetNode<PanelContainer>("Hud/Panel");
        Check(panel.GetGlobalRect().End.X <= screenLeft, "Controls do not cover the map");

        for (int y = 0; y < NoiseTerrainMapData.Size; y += 67)
            for (int x = 0; x < NoiseTerrainMapData.Size; x += 67)
                Check(scene.GetTerrainAt(new Vector2I(x, y)) is >= 0 and <= 2,
                    $"Valid terrain at ({x}, {y})");
        Check(scene.GetTerrainAt(new Vector2I(-1, 0)) == -1, "Out-of-bounds query is rejected");

        float oldZoom = camera.Zoom.X;
        var wheel = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.WheelUp,
            Pressed = true,
            Position = viewportSize * 0.5f,
        };
        scene._UnhandledInput(wheel);
        Check(camera.Zoom.X > oldZoom, "Mouse wheel zooms in");
        scene.FitCamera();
        scene.ZoomAt(viewportSize * 0.5f, 16f);
        Check(!overview.Visible && camera.Zoom.X >= scene.DetailZoom, "Zoom shows detailed tiles");
        var detailedCells = layer.GetUsedCells();
        Check(detailedCells.Count is > 0 and < 100000, "Only visible tiles are allocated");
        foreach (Vector2I cell in detailedCells)
            Check(layer.GetCellAtlasCoords(cell) == new Vector2I(data.GetTerrainAt(cell), 0),
                $"Detailed visual matches logic at {cell}");
        scene.FitCamera();
        Check(overview.Visible && layer.GetUsedCells().Count == 0, "Fit restores the whole map");

        var duplicate = new NoiseTerrainMapData
        {
            ContinentScale = data.ContinentScale,
            ContinentWeight = data.ContinentWeight,
            DetailScale = data.DetailScale,
            DetailWeight = data.DetailWeight,
            WarpScale = data.WarpScale,
            WarpStrength = data.WarpStrength,
            EdgeFalloff = data.EdgeFalloff,
        };
        duplicate.Generate(data.Seed, data.NoiseFrequency, data.WaterThreshold,
            data.CoastWidth, data.IterationStrength);
        Check(duplicate.TerrainCells.SequenceEqual(data.TerrainCells),
            "Same seed and settings reproduce terrain");

        string snapshot = data.ToJson();
        Check(NoiseTerrainMapData.TryFromJson(snapshot, out var restored)
            && restored!.TerrainCells.SequenceEqual(data.TerrainCells),
            "Save data survives JSON round trip");
        var invalid = JsonNode.Parse(snapshot)!.AsObject();
        invalid["land_mask_b64"] = "AAAA";
        Check(!NoiseTerrainMapData.TryFromJson(invalid.ToJsonString(), out _),
            "Corrupt terrain data is rejected");

        var previous = JsonNode.Parse(snapshot)!.AsObject();
        previous["version"] = 2;
        foreach (string key in new[] { "continent_scale", "continent_weight", "detail_scale",
            "detail_weight", "warp_scale", "warp_strength", "edge_falloff" })
            previous.Remove(key);
        Check(NoiseTerrainMapData.TryFromJson(previous.ToJsonString(), out var previousMap)
            && previousMap!.TerrainCells.SequenceEqual(data.TerrainCells),
            "Previous compact save format still loads");

        var legacy = JsonNode.Parse(snapshot)!.AsObject();
        legacy["version"] = 1;
        legacy.Remove("land_mask_b64");
        string allWater = new('0', NoiseTerrainMapData.Size * NoiseTerrainMapData.Size);
        legacy["land_mask"] = allWater;
        legacy["terrain_cells"] = allWater;
        Check(NoiseTerrainMapData.TryFromJson(legacy.ToJsonString(), out var oldMap)
            && oldMap!.GetTerrainAt(new Vector2I(512, 512)) == (int)NoiseTerrainMapData.Terrain.Water,
            "Previous text save format still loads");

        var isolated = new NoiseTerrainMapData();
        isolated.Generate(123, 0.003, 0, 16, 1);
        Array.Fill(isolated.LandMask, (byte)1);
        isolated.LandMask[512 * NoiseTerrainMapData.Size + 512] = 0;
        isolated.RebuildTerrain();
        Check(isolated.GetTerrainAt(new Vector2I(528, 512)) == (int)NoiseTerrainMapData.Terrain.Sand,
            "Wide coast includes requested distance");
        Check(isolated.GetTerrainAt(new Vector2I(529, 512)) == (int)NoiseTerrainMapData.Terrain.Grass,
            "Wide coast stops at requested distance");
        isolated.Iterate();
        Check(isolated.GetTerrainAt(new Vector2I(512, 512)) == (int)NoiseTerrainMapData.Terrain.Grass,
            "Iteration smooths an isolated water cell");

        byte[] originalMask = (byte[])data.LandMask.Clone();
        scene.EdgeFalloff = 0.4;
        scene.WarpStrength = 0;
        scene.GetNode<Button>("Hud/Panel/Rows/Actions/Apply").EmitSignal(Button.SignalName.Pressed);
        data = scene.MapData;
        Check(data.Seed == 12345 && data.EdgeFalloff == 0.4 && data.WarpStrength == 0,
            "Apply button uses parameters without changing seed");
        Check(!data.LandMask.SequenceEqual(originalMask), "New parameters change terrain");
        Check(NoiseTerrainMapData.TryFromJson(data.ToJson(), out var changedMap)
            && changedMap!.EdgeFalloff == 0.4 && changedMap.WarpStrength == 0,
            "New parameters survive save and load");

        scene.GetNode<Button>("Hud/Panel/Rows/Actions/Iterate").EmitSignal(Button.SignalName.Pressed);
        data = scene.MapData;
        Check(overview.Visible && overview.Texture.GetSize() == new Vector2(1024, 1024),
            "Iteration updates the overview");
        byte[] savedTerrain = (byte[])data.TerrainCells.Clone();
        Check(scene.SaveMap(TestSavePath), "Map saves to a temporary slot");
        int savedSeed = data.Seed;
        scene.GetNode<Button>("Hud/Panel/Rows/Actions/Regenerate").EmitSignal(Button.SignalName.Pressed);
        Check(scene.MapData.Seed != savedSeed, "Regenerate button changes the seed");
        scene.CoastWidth = 5;
        scene.EdgeFalloff = 0;
        scene.WarpStrength = 0.18;
        Check(scene.LoadMap(TestSavePath), "Map loads from temporary slot");
        Check(scene.MapData.Seed == savedSeed && scene.MapData.TerrainCells.SequenceEqual(savedTerrain),
            "Load restores exact iterated map");
        Check(scene.CoastWidth == scene.MapData.CoastWidth
            && scene.EdgeFalloff == 0.4 && scene.WarpStrength == 0,
            "Load restores inspector parameters");

        using (var file = FileAccess.Open(TestSavePath, FileAccess.ModeFlags.Write))
            file.StoreString("{broken");
        Check(!scene.LoadMap(TestSavePath), "Corrupt JSON is rejected");
        Check(scene.MapData.Seed == savedSeed && scene.MapData.TerrainCells.SequenceEqual(savedTerrain),
            "Failed load preserves current map");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(TestSavePath));

        scene.QueueFree();
        if (_failures == 0) GD.Print("C# noise terrain map verification passed");
        GetTree().Quit(_failures > 0 ? 1 : 0);
    }

    private void Check(bool condition, string message)
    {
        if (condition) return;
        _failures++;
        GD.PushError(message);
    }
}
