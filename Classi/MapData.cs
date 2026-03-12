class MapData
{
    public int Width { get; }
    public int Height { get; }
    public char[,] Tiles { get; }
    public List<Door> Doors { get; } = new List<Door>();

    public MapData(int width, int height)
    {
        Width = width;
        Height = height;
        Tiles = new char[height, width];
    }
}