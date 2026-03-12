class Enemy
{
    public string Nome { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Messaggio { get; }
    public int Hp { get; }
    public int MaxHp { get; }

    public Enemy(string nome, int x, int y, string messaggio, int maxHp)
    {
        Nome = nome;
        X = x;
        Y = y;
        Messaggio = messaggio;
        MaxHp = maxHp;
        Hp = maxHp;
    }
}
