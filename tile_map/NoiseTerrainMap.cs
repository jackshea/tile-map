using System;
using Godot;

public partial class NoiseTerrainMap : Node2D
{
    private const string SavePath = "user://noise_terrain_map.json";
    private const int AtlasSourceId = 0;
    private const int TileSize = 32;
    private const int TileMargin = 12;
    private const float SidebarWidth = 260f;

    [ExportGroup("基础")]
    [Export(PropertyHint.Range, "0,2147483647,1")] public int InitialSeed { get; set; } = 12345;
    [Export(PropertyHint.Range, "0.0005,0.05,0.0005")] public double NoiseFrequency { get; set; } = 0.003;
    [Export(PropertyHint.Range, "-1,1,0.01")] public double WaterThreshold { get; set; }

    [ExportGroup("大陆与海岸")]
    [Export(PropertyHint.Range, "0.2,1,0.01")] public double ContinentScale { get; set; } = 0.45;
    [Export(PropertyHint.Range, "0.3,0.85,0.01")] public double ContinentWeight { get; set; } = 0.62;
    [Export(PropertyHint.Range, "1,32,1")] public int CoastWidth { get; set; } = 2;
    [Export(PropertyHint.Range, "0,0.7,0.01")] public double EdgeFalloff { get; set; }

    [ExportGroup("地形细节")]
    [Export(PropertyHint.Range, "1,6,0.1")] public double DetailScale { get; set; } = 2.5;
    [Export(PropertyHint.Range, "0,0.15,0.01")] public double DetailWeight { get; set; } = 0.09;
    [Export(PropertyHint.Range, "0.2,2,0.01")] public double WarpScale { get; set; } = 0.7;
    [Export(PropertyHint.Range, "0,0.5,0.01")] public double WarpStrength { get; set; } = 0.18;

    [ExportGroup("迭代")]
    [Export(PropertyHint.Range, "1,4,1")] public int IterationStrength { get; set; } = 1;

    [ExportGroup("视角")]
    [Export(PropertyHint.Range, "0.1,0.6,0.01")] public float DetailZoom { get; set; } = 0.22f;
    [Export(PropertyHint.Range, "1.1,2,0.05")] public float WheelZoomStep { get; set; } = 1.5f;
    [Export(PropertyHint.Range, "1,4,0.1")] public float MaxZoom { get; set; } = 2f;

    private TileMapLayer _terrainLayer = null!;
    private Sprite2D _overview = null!;
    private Camera2D _camera = null!;
    private Label _seedLabel = null!;
    private Label _statusLabel = null!;
    private float _fitZoom;
    private bool _dragging;
    private Rect2I _loadedRect;

    public NoiseTerrainMapData MapData { get; private set; } = null!;

    public override void _Ready()
    {
        _terrainLayer = GetNode<TileMapLayer>("Terrain");
        _overview = GetNode<Sprite2D>("Overview");
        _camera = GetNode<Camera2D>("Camera2D");
        _seedLabel = GetNode<Label>("Hud/Panel/Rows/SeedLabel");
        _statusLabel = GetNode<Label>("Hud/Panel/Rows/StatusLabel");

        GetNode<Button>("Hud/Panel/Rows/Actions/Regenerate").Pressed += Regenerate;
        GetNode<Button>("Hud/Panel/Rows/Actions/Apply").Pressed += ApplyParameters;
        GetNode<Button>("Hud/Panel/Rows/Actions/Iterate").Pressed += Iterate;
        GetNode<Button>("Hud/Panel/Rows/Actions/Save").Pressed += () => SaveMap();
        GetNode<Button>("Hud/Panel/Rows/Actions/Load").Pressed += () => LoadMap();
        GetNode<Button>("Hud/Panel/Rows/Actions/Fit").Pressed += FitCamera;
        GetViewport().SizeChanged += OnViewportResized;
        FitCamera();
        Generate(InitialSeed);
        SetStatus("地图已生成。");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouse)
        {
            if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomAt(mouse.Position, WheelZoomStep);
                GetViewport().SetInputAsHandled();
            }
            else if (mouse.Pressed && mouse.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomAt(mouse.Position, 1f / WheelZoomStep);
                GetViewport().SetInputAsHandled();
            }
            else if (mouse.ButtonIndex is MouseButton.Middle or MouseButton.Right)
            {
                _dragging = mouse.Pressed;
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseMotion motion && _dragging)
        {
            _camera.Position -= motion.Relative / _camera.Zoom.X;
            ClampCamera();
            RefreshVisibleTiles();
            GetViewport().SetInputAsHandled();
        }
    }

    public int GetTerrainAt(Vector2I cell) => MapData?.GetTerrainAt(cell) ?? -1;

    public void Generate(int seed)
    {
        InitialSeed = seed;
        MapData = new NoiseTerrainMapData
        {
            ContinentScale = ContinentScale,
            ContinentWeight = ContinentWeight,
            DetailScale = DetailScale,
            DetailWeight = DetailWeight,
            WarpScale = WarpScale,
            WarpStrength = WarpStrength,
            EdgeFalloff = EdgeFalloff,
        };
        MapData.Generate(seed, NoiseFrequency, WaterThreshold, CoastWidth, IterationStrength);
        DrawMap();
    }

    public void Regenerate()
    {
        int nextSeed;
        do nextSeed = Random.Shared.Next(int.MaxValue);
        while (nextSeed == MapData.Seed);
        Generate(nextSeed);
        SetStatus("已重新生成地图。");
    }

    public void ApplyParameters()
    {
        Generate(InitialSeed);
        SetStatus("已按当前参数重建地图。");
    }

    public void Iterate()
    {
        MapData.Iterate();
        DrawMap();
        SetStatus("已基于当前地形迭代。");
    }

    public bool SaveMap(string path = SavePath)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            SetStatus("保存失败：无法打开存档文件。");
            return false;
        }
        file.StoreString(MapData.ToJson());
        file.Flush();
        if (file.GetError() != Error.Ok)
        {
            SetStatus("保存失败：写入存档时出错。");
            return false;
        }
        SetStatus("地图已保存到用户目录。");
        return true;
    }

    public bool LoadMap(string path = SavePath)
    {
        if (!FileAccess.FileExists(path))
        {
            SetStatus("加载失败：还没有保存的地图。");
            return false;
        }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            SetStatus("加载失败：无法读取存档文件。");
            return false;
        }
        if (!NoiseTerrainMapData.TryFromJson(file.GetAsText(), out var loaded) || loaded == null)
        {
            SetStatus("加载失败：存档格式无效或已损坏。");
            return false;
        }

        MapData = loaded;
        InitialSeed = loaded.Seed;
        NoiseFrequency = loaded.NoiseFrequency;
        WaterThreshold = loaded.WaterThreshold;
        CoastWidth = loaded.CoastWidth;
        IterationStrength = loaded.IterationStrength;
        ContinentScale = loaded.ContinentScale;
        ContinentWeight = loaded.ContinentWeight;
        DetailScale = loaded.DetailScale;
        DetailWeight = loaded.DetailWeight;
        WarpScale = loaded.WarpScale;
        WarpStrength = loaded.WarpStrength;
        EdgeFalloff = loaded.EdgeFalloff;
        DrawMap();
        SetStatus("已加载保存的地图。");
        return true;
    }

    public void FitCamera()
    {
        _fitZoom = CalculateFitZoom();
        _camera.Zoom = Vector2.One * _fitZoom;
        _camera.Position = new Vector2(
            NoiseTerrainMapData.Size * TileSize * 0.5f - SidebarWidth / (2f * _fitZoom),
            NoiseTerrainMapData.Size * TileSize * 0.5f);
        RefreshVisibleTiles();
    }

    public void ZoomAt(Vector2 mousePosition, float factor)
    {
        float nextZoom = Mathf.Clamp(_camera.Zoom.X * factor, _fitZoom, MaxZoom);
        if (Mathf.IsEqualApprox(nextZoom, _camera.Zoom.X)) return;
        Vector2 cursorOffset = mousePosition - GetViewportRect().Size * 0.5f;
        Vector2 worldUnderCursor = _camera.Position + cursorOffset / _camera.Zoom.X;
        _camera.Zoom = Vector2.One * nextZoom;
        _camera.Position = worldUnderCursor - cursorOffset / nextZoom;
        ClampCamera();
        RefreshVisibleTiles();
    }

    private void DrawMap()
    {
        UpdateOverview();
        _loadedRect = new Rect2I();
        RefreshVisibleTiles();
        _seedLabel.Text = $"当前种子：{MapData.Seed}";
    }

    private void UpdateOverview()
    {
        byte[][] palette =
        [
            [49, 74, 122],
            [197, 170, 100],
            [78, 124, 55],
        ];
        var pixels = new byte[NoiseTerrainMapData.Size * NoiseTerrainMapData.Size * 3];
        for (int i = 0; i < MapData.TerrainCells.Length; i++)
        {
            byte[] color = palette[MapData.TerrainCells[i]];
            int pixelIndex = i * 3;
            pixels[pixelIndex] = color[0];
            pixels[pixelIndex + 1] = color[1];
            pixels[pixelIndex + 2] = color[2];
        }
        var image = Image.CreateFromData(NoiseTerrainMapData.Size, NoiseTerrainMapData.Size,
            false, Image.Format.Rgb8, pixels);
        _overview.Texture = ImageTexture.CreateFromImage(image);
    }

    private void RefreshVisibleTiles()
    {
        if (MapData == null) return;
        if (_camera.Zoom.X < DetailZoom)
        {
            _overview.Visible = true;
            if (_terrainLayer.Visible) _terrainLayer.Clear();
            _terrainLayer.Visible = false;
            _loadedRect = new Rect2I();
            return;
        }

        _overview.Visible = false;
        _terrainLayer.Visible = true;
        Vector2 halfWorld = GetViewportRect().Size / (_camera.Zoom.X * 2f);
        var first = new Vector2I(
            Math.Clamp((int)Math.Floor((_camera.Position.X - halfWorld.X) / TileSize), 0, NoiseTerrainMapData.Size),
            Math.Clamp((int)Math.Floor((_camera.Position.Y - halfWorld.Y) / TileSize), 0, NoiseTerrainMapData.Size));
        var last = new Vector2I(
            Math.Clamp((int)Math.Ceiling((_camera.Position.X + halfWorld.X) / TileSize), 0, NoiseTerrainMapData.Size),
            Math.Clamp((int)Math.Ceiling((_camera.Position.Y + halfWorld.Y) / TileSize), 0, NoiseTerrainMapData.Size));
        if (_loadedRect.HasPoint(first) && _loadedRect.HasPoint(last - Vector2I.One)) return;

        var paddedFirst = new Vector2I(Math.Max(0, first.X - TileMargin), Math.Max(0, first.Y - TileMargin));
        var paddedLast = new Vector2I(
            Math.Min(NoiseTerrainMapData.Size, last.X + TileMargin),
            Math.Min(NoiseTerrainMapData.Size, last.Y + TileMargin));
        _loadedRect = new Rect2I(paddedFirst, paddedLast - paddedFirst);
        _terrainLayer.Clear();
        for (int y = paddedFirst.Y; y < paddedLast.Y; y++)
            for (int x = paddedFirst.X; x < paddedLast.X; x++)
                _terrainLayer.SetCell(new Vector2I(x, y), AtlasSourceId,
                    new Vector2I(MapData.GetTerrainAt(new Vector2I(x, y)), 0));
    }

    private void OnViewportResized()
    {
        bool wasFitted = Mathf.IsEqualApprox(_camera.Zoom.X, _fitZoom);
        _fitZoom = CalculateFitZoom();
        if (wasFitted) FitCamera();
        else
        {
            _camera.Zoom = Vector2.One * Math.Max(_camera.Zoom.X, _fitZoom);
            ClampCamera();
            RefreshVisibleTiles();
        }
    }

    private float CalculateFitZoom()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        float contentWidth = Math.Max(1f, viewportSize.X - SidebarWidth);
        return Math.Min(contentWidth, viewportSize.Y) * 0.96f
            / (NoiseTerrainMapData.Size * TileSize);
    }

    private void ClampCamera()
    {
        float mapPixels = NoiseTerrainMapData.Size * TileSize;
        Vector2 viewportSize = GetViewportRect().Size;
        float contentWidth = Math.Max(1f, viewportSize.X - SidebarWidth);
        Vector2 halfWorld = viewportSize / (_camera.Zoom.X * 2f);
        var position = _camera.Position;
        if (contentWidth / _camera.Zoom.X >= mapPixels)
            position.X = mapPixels * 0.5f - SidebarWidth / (2f * _camera.Zoom.X);
        else
        {
            float leftLimit = (viewportSize.X * 0.5f - SidebarWidth) / _camera.Zoom.X;
            float rightLimit = mapPixels - halfWorld.X;
            position.X = Mathf.Clamp(position.X, leftLimit, rightLimit);
        }
        position.Y = halfWorld.Y * 2f >= mapPixels
            ? mapPixels * 0.5f
            : Mathf.Clamp(position.Y, halfWorld.Y, mapPixels - halfWorld.Y);
        _camera.Position = position;
    }

    private void SetStatus(string message) => _statusLabel.Text = message;
}
