class Enemy
{
    public string Nome { get; }
    public int X { get; }
    public int Y { get; }
    public string Messaggio { get; }

    public Enemy(string nome, int x, int y, string messaggio)
    {
        Nome = nome;
        X = x;
        Y = y;
        Messaggio = messaggio;
    }
}