class Npc
{
    public string Nome { get; }
    public int X { get; }
    public int Y { get; }
    public string Messaggio { get; }
    public bool IsFriendly { get; }

    public Npc(string nome, int x, int y, string messaggio, bool isFriendly = false)
    {
        Nome = nome;
        X = x;
        Y = y;
        Messaggio = messaggio;
        IsFriendly = isFriendly;
    }
}