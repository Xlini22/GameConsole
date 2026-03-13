using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

enum MapId
{
    Overworld,
    Municipio,
    Bottega,
    Magazzino,
    Locanda,
    Officina,
    CasaNord,
    CasaSud,
    Torre,
    Ospedale
}

class Program
{
    static Player player = new Player { X = 4, Y = 4, MaxHp = 100, Hp = 100 };
    static MapData currentMap;
    static MapId currentMapId = MapId.Overworld;
    static readonly List<Npc> npcs = new List<Npc>();
    static Enemy enemy;
    // Celle solide degli edifici (indipendenti dal carattere usato per le strade)
    static readonly HashSet<(int x, int y)> buildingBlocks = new HashSet<(int, int)>();
    static string ultimoMessaggio = "Barra spaziatrice per parlare con NPC o porte.";
    static int lastDx = 0, lastDy = 0;
    static int tickCounter = 0;
    static bool enemyAggro = false;
    static DateTime lastEnemyHit = DateTime.MinValue;
    const int EnemyCollisionRange = 2;
    const int EnemyGreetRange = 15;
    const int EnemyAggroStart = 5;
    const int EnemyAggroLose = 20;
    const int EnemyMoveInterval = 4; // move every 4 ticks (più lento)
    const int FrameDelayMs = 16; // ~60fps target
    const int EnemyTouchDamage = 5;
    const int EnemyDamageCooldownMs = 500;
    const int NpcGreetRange = 4;
    const int NpcCollisionRange = 2;

    static void Main()
    {
        Console.CursorVisible = false;
        CaricaMappa(MapId.Overworld, 4, 4);

        while (true)
        {
            var frameStart = DateTime.UtcNow;

            AggiornaNemico();
            Disegna();
            GestisciInput();

            int elapsed = (int)(DateTime.UtcNow - frameStart).TotalMilliseconds;
            if (elapsed < FrameDelayMs)
                Thread.Sleep(FrameDelayMs - elapsed);
        }
    }

    static void CaricaMappa(MapId id, int spawnX, int spawnY)
    {
        currentMapId = id;
        currentMap = BuildMap(id);
        Console.Clear();
        player.X = spawnX;
        player.Y = spawnY;
        ultimoMessaggio = $"Sei in {id}";

        npcs.Clear();
        if (id == MapId.Overworld)
        {
            InizializzaNpcs();
            InizializzaNpcsInterni(null); // nessun interno per overworld, pulizia
            enemy = new Enemy("Nemico", 90, 40, "Ehi, non avvicinarti troppo!", 60);
            enemyAggro = false;
        }
        else
        {
            enemy = null;
            enemyAggro = false;
            InizializzaNpcsInterni(id);
        }
    }

    static (int Width, int Height) GetMapSize(MapId id) =>
        id switch
        {
            MapId.Overworld => (220, 90),
            MapId.Municipio => (40, 20),
            MapId.Bottega => (32, 18),
            MapId.Magazzino => (42, 20),
            MapId.Locanda => (38, 20),
            MapId.Officina => (40, 22),
            MapId.CasaNord => (32, 18),
            MapId.CasaSud => (32, 18),
            MapId.Torre => (36, 20),
            MapId.Ospedale => (48, 24),
            _ => (120, 40)
        };

    static (int X, int Y) GetInteriorSpawn(MapId id)
    {
        var size = GetMapSize(id);
        return (size.Width / 2, Math.Max(1, size.Height - 3));
    }

    static MapData BuildMap(MapId id)
    {
        buildingBlocks.Clear();
        return id switch
        {
            MapId.Overworld => BuildOverworld(),
            MapId.Municipio => BuildHouse("Municipio", id, 17, 20),
            MapId.Bottega => BuildHouse("Bottega", id, 17, 20),
            MapId.Magazzino => BuildHouse("Magazzino", id, 101, 20),
            MapId.Locanda => BuildHouse("Locanda", id, 139, 27),
            MapId.Officina => BuildHouse("Officina", id, 180, 49),
            MapId.CasaNord => BuildHouse("Casa Nord", id, 39, 67),
            MapId.CasaSud => BuildHouse("Casa Sud", id, 118, 77),
            MapId.Torre => BuildHouse("Torre", id, 199, 84),
            MapId.Ospedale => BuildHouse("Ospedale", id, 75, 84),
            _ => BuildOverworld()
        };
    }

    static MapData BuildOverworld()
    {
        var map = new MapData(220, 90);
        var tiles = map.Tiles;

        // Base: bordo di muri e pavimento vuoto
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                tiles[y, x] = (y == 0 || y == map.Height - 1 || x == 0 || x == map.Width - 1) ? '#' : ' ';

        // Strade principali con corsie e linea tratteggiata
        DisegnaStradaOrizzontale(map, 30, 2, map.Width - 3);
        DisegnaStradaOrizzontale(map, 60, 5, map.Width - 6);
        DisegnaStradaVerticale(map, 70, 2, map.Height - 3);
        DisegnaStradaVerticale(map, 150, 5, map.Height - 6);

        // Edifici con porte registrate
        RegistraEdificio(map, 8, 8, 18, 12, "Municipio", MapId.Municipio);
        RegistraEdificio(map, 40, 10, 16, 10, "Bottega", MapId.Bottega);
        RegistraEdificio(map, 90, 6, 22, 14, "Magazzino", MapId.Magazzino);
        RegistraEdificio(map, 130, 15, 18, 12, "Locanda", MapId.Locanda);
        RegistraEdificio(map, 170, 35, 20, 14, "Officina", MapId.Officina);
        RegistraEdificio(map, 30, 55, 18, 12, "Casa Nord", MapId.CasaNord);
        RegistraEdificio(map, 110, 65, 16, 12, "Casa Sud", MapId.CasaSud);
        RegistraEdificio(map, 190, 70, 18, 14, "Torre", MapId.Torre);
        RegistraOspedale(map, 60, 74, MapId.Ospedale);

        // Parchi/boschi
        AggiungiRettangolo(map, 15, 40, 25, 10, 'T');
        AggiungiRettangolo(map, 75, 45, 18, 8, 'T');
        AggiungiRettangolo(map, 145, 55, 24, 10, 'T');

        // Piazza centrale
        AggiungiRettangolo(map, 60, 25, 20, 8, '.');

        return map;
    }

    static MapData BuildHouse(string label, MapId id, int exitToX, int exitToY)
    {
        var (width, height) = GetMapSize(id);
        var map = new MapData(width, height);
        var t = map.Tiles;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                t[y, x] = (y == 0 || y == height - 1 || x == 0 || x == width - 1) ? '#' : ' ';

        // semplice arredo
        if (width > 10 && height > 8)
        {
            for (int x = 2; x < width - 2; x++) t[3, x] = '='; // mensola
            for (int y = 5; y < height - 3; y++) t[y, 5] = '|'; // pilastro
            for (int x = 8; x < width - 6; x++) t[height - 4, x] = '-'; // tappeto
        }

        // porta di uscita verso overworld
        int doorX = width / 2;
        int doorY = height - 1;
        t[doorY, doorX] = '+';
        t[doorY - 1, doorX] = '+';

        map.Doors.Add(new Door
        {
            X = doorX,
            Y = doorY,
            TargetMap = MapId.Overworld,
            TargetX = exitToX,
            TargetY = Math.Min(exitToY + 1, GetMapSize(MapId.Overworld).Height - 2)
        });

        // insegna interna
        string labelShort = label.Length > width - 4 ? label.Substring(0, width - 4) : label;
        int textStart = Math.Max(2, (width - labelShort.Length) / 2);
        for (int i = 0; i < labelShort.Length && textStart + i < width - 2; i++)
            t[1, textStart + i] = labelShort[i];

        return map;
    }

    static void AggiungiRettangolo(MapData map, int startX, int startY, int width, int height, char tile)
    {
        var mappa = map.Tiles;
        for (int y = startY; y < startY + height && y < map.Height - 1; y++)
            for (int x = startX; x < startX + width && x < map.Width - 1; x++)
                mappa[y, x] = tile;
    }

    static void DisegnaStradaOrizzontale(MapData map, int centerY, int startX, int endX)
    {
        int yTop = centerY - 1;
        int yMid = centerY;
        int yBot = centerY + 1;
        if (yTop < 0 || yBot >= map.Height) return;

        for (int x = startX; x <= endX && x < map.Width; x++)
        {
            if (x < 0) continue;
            map.Tiles[yTop, x] = '-';
            map.Tiles[yBot, x] = '-';
            map.Tiles[yMid, x] = ((x - startX) % 2 == 0) ? '-' : ' ';
        }
    }

    static void DisegnaStradaVerticale(MapData map, int centerX, int startY, int endY)
    {
        int half = 4;
        int left = centerX - half;
        int right = centerX + half;
        if (left < 0 || right >= map.Width) return;

        for (int y = startY; y <= endY && y < map.Height; y++)
        {
            if (y < 0) continue;
            for (int x = left; x <= right; x++)
            {
                if (x < 0 || x >= map.Width) continue;
                if (x == left || x == right) map.Tiles[y, x] = '|';
                else if (x == centerX) map.Tiles[y, x] = '¦';
                else map.Tiles[y, x] = ' ';
            }
        }
    }

    static void RegistraEdificio(MapData map, int startX, int startY, int width, int height, string label, MapId targetMap)
    {
        var mappa = map.Tiles;
        int tplMinWidth = 24; // larghezza minima per il template ASCII
        int effWidth = Math.Max(width, tplMinWidth);
        int effHeight = Math.Max(height, 12);

        int roofY = startY;
        int wallTop = startY + 1;
        int wallBottom = Math.Min(map.Height - 2, startY + effHeight - 1);
        int wallLeft = startX;
        int wallRight = Math.Min(map.Width - 2, startX + effWidth - 1);

        string labelLine = $"[{label}]";
        string[] template = new[]
        {
            "",
            "",
            "            ^    ___",
            "          /   \\  | |",
            "        /       \\| |",
            "      /    ___    \\|",
            "    /      |_|      \\",
            "  / |               | \\",
            "    |         ____  |",
            "    |  ____   |  |  |",
            "    |  |  |  _|__|_ |",
            "    |  |  |         |",
            "    |__|^^|_________|"
        };

        int tplH = template.Length;
        int tplW = 0;
        foreach (var line in template) tplW = Math.Max(tplW, line.Length);

        int destTop = roofY;
        int destLeft = wallLeft + Math.Max(0, (effWidth - tplW) / 2);

        for (int ty = 0; ty < tplH; ty++)
        {
            int y = destTop + ty;
            if (y >= map.Height - 1) break;
            var line = template[ty];
            for (int tx = 0; tx < line.Length; tx++)
            {
                int x = destLeft + tx;
                if (x >= map.Width - 1 || x > wallRight) break;
                char ch = line[tx];
                if (ch == '^' && tx + 1 < line.Length && line[tx + 1] == '^')
                {
                    int left = x;
                    int right = x + 1;
                    mappa[y, left] = '^';
                    mappa[y, right] = '^';
                    map.Doors.Add(new Door
                    {
                        X = left,
                        Y = y,
                        TargetMap = targetMap,
                        TargetX = GetInteriorSpawn(targetMap).X,
                        TargetY = GetInteriorSpawn(targetMap).Y
                    });
                    map.Doors.Add(new Door
                    {
                        X = right,
                        Y = y,
                        TargetMap = targetMap,
                        TargetX = GetInteriorSpawn(targetMap).X,
                        TargetY = GetInteriorSpawn(targetMap).Y
                    });
                    tx++; // skip second ^
                    continue;
                }
                mappa[y, x] = ch;
                // Tutto ciò che non è spazio diventa solido per le collisioni, eccetto la porta
                if (ch != ' ')
                    buildingBlocks.Add((x, y));
            }
        }

        // centra il nome sull'apice del tetto (^)
        int caretLocal = template[2].IndexOf('^');
        if (caretLocal >= 0)
        {
            int caretGlobal = destLeft + caretLocal;
            int labelStart = caretGlobal - labelLine.Length / 2;
            int y = destTop; // prima riga
            for (int i = 0; i < labelLine.Length; i++)
            {
                int x = labelStart + i;
                if (x < wallLeft || x > wallRight || x >= map.Width - 1 || x < 0) continue;
                mappa[y, x] = labelLine[i];
            }
        }

        // Nessuna insegna extra: solo il nome nel template
    }

    static void RegistraOspedale(MapData map, int startX, int startY, MapId targetMap)
    {
        var mappa = map.Tiles;
        string[] tpl =
        {
            "            [Ospedale] ",
            "         _________________",
            "---------|      ┌─┐      |---------",
            "|        |    ┌─┘ └─┐    |        |",
            "| []  [] |    └─┐ ┌─┘    | []  [] |",
            "| []  [] |      └─┘      | []  [] |",
            "|        |  [] [] [] []  |        |",
            "| []  [] |               | []  [] |",
            "| []  [] |   _________   | []  [] |",
            "|        |   |   |   |   |        |",
            "|________|___|_^^|_^^|___|________|"
        };

        int tplH = tpl.Length;
        int maxW = 0; foreach (var l in tpl) maxW = Math.Max(maxW, l.Length);

        for (int ty = 0; ty < tplH; ty++)
        {
            int y = startY + ty;
            if (y >= map.Height - 1) break;
            var line = tpl[ty];
            for (int tx = 0; tx < line.Length; tx++)
            {
                int x = startX + tx;
                if (x >= map.Width - 1) break;
                char ch = line[tx];
                if (ch == '^' && tx + 1 < line.Length && line[tx + 1] == '^')
                {
                    mappa[y, x] = '^';
                    mappa[y, x + 1] = '^';
                    map.Doors.Add(new Door { X = x, Y = y, TargetMap = targetMap, TargetX = GetInteriorSpawn(targetMap).X, TargetY = GetInteriorSpawn(targetMap).Y });
                    map.Doors.Add(new Door { X = x + 1, Y = y, TargetMap = targetMap, TargetX = GetInteriorSpawn(targetMap).X, TargetY = GetInteriorSpawn(targetMap).Y });
                    tx++;
                    continue;
                }
                mappa[y, x] = ch;
                if (ch != ' ')
                    buildingBlocks.Add((x, y));
            }
        }
    }

    static void InizializzaNpcs()
    {
        // NPC posizionati fuori dagli edifici
        npcs.Add(new Npc("Guardia", 12, 20, "Tieniti alla larga dai tetti!"));
        npcs.Add(new Npc("Bottaio", 44, 20, "Puoi entrare se trovi la porta."));
        npcs.Add(new Npc("Locandiere", 136, 27, "La locanda è piena, ma c'è spazio per te."));
        npcs.Add(new Npc("Meccanico", 176, 49, "Sto riparando un motore, non toccare."));
        npcs.Add(new Npc("Erborista", 62, 46, "Ho rimedi naturali per tutti.", true));
        npcs.Add(new Npc("Bambino", 152, 32, "Sto correndo verso la piazza!", true));
    }

    static void InizializzaNpcsInterni(MapId? id)
    {
        if (id == null) return;
        var (w, h) = GetMapSize(id.Value);
        int cx = w / 2;
        int cy = h / 2;
        switch (id)
        {
            case MapId.Locanda:
                npcs.Add(new Npc("Ospite", cx - 4, cy, "Bella locanda, vero?", true));
                npcs.Add(new Npc("Cuoco", cx + 3, cy + 2, "Sto preparando una zuppa.", true));
                break;
            case MapId.Municipio:
                npcs.Add(new Npc("Segretaria", cx, cy, "Hai un appuntamento?", true));
                break;
            case MapId.Bottega:
                npcs.Add(new Npc("Commerciante", cx, cy, "Guarda le mie merci.", true));
                break;
            case MapId.Officina:
                npcs.Add(new Npc("Apprendista", cx, cy, "Sto studiando i motori.", true));
                break;
            case MapId.CasaNord:
            case MapId.CasaSud:
                npcs.Add(new Npc("Inquilino", cx, cy, "Benvenuto a casa.", true));
                break;
            case MapId.Torre:
                npcs.Add(new Npc("Studioso", cx, cy, "Sto leggendo antichi manoscritti.", true));
                break;
            case MapId.Magazzino:
                npcs.Add(new Npc("Magazziniere", cx, cy, "Attento alle casse.", true));
                break;
            case MapId.Ospedale:
                npcs.Add(new Npc("Dottore", cx, cy, "Seguimi in ambulatorio.", true));
                break;
        }
    }

    static void Disegna()
    {
        int mapWidth = currentMap.Width;
        int mapHeight = currentMap.Height;
        var mappa = currentMap.Tiles;

        int viewWidth = Math.Max(1, Math.Min(Console.WindowWidth, mapWidth));
        int viewHeight = Math.Max(1, Math.Min(Console.WindowHeight - 2, mapHeight));

        int startX = Clamp(player.X - viewWidth / 2, 0, Math.Max(0, mapWidth - viewWidth));
        int startY = Clamp(player.Y - viewHeight / 2, 0, Math.Max(0, mapHeight - viewHeight));

        // buffer
        char[,] buf = new char[viewHeight, viewWidth];
        for (int y = 0; y < viewHeight; y++)
        {
            int my = startY + y;
            for (int x = 0; x < viewWidth; x++)
            {
                int mx = startX + x;
                buf[y, x] = mappa[my, mx];
            }
        }

        // NPC
        foreach (var npc in npcs)
            BlitSprite(buf, viewWidth, viewHeight, npc.X - startX, npc.Y - startY, 'N');

        // Enemy
        if (enemy != null)
            BlitSprite(buf, viewWidth, viewHeight, enemy.X - startX, enemy.Y - startY, 'E');

        // Player
        BlitSprite(buf, viewWidth, viewHeight, player.X - startX, player.Y - startY, '@');

        // balloons
        foreach (var npc in npcs)
        {
            int dist = Math.Abs(npc.X - player.X) + Math.Abs(npc.Y - player.Y);
            if (dist <= NpcGreetRange)
                BlitBalloon(buf, viewWidth, viewHeight, npc.X - startX, npc.Y - startY, npc.Messaggio);
        }
        if (enemy != null && !enemyAggro)
        {
            int dist = Math.Abs(enemy.X - player.X) + Math.Abs(enemy.Y - player.Y);
            if (dist <= EnemyGreetRange)
                BlitBalloon(buf, viewWidth, viewHeight, enemy.X - startX, enemy.Y - startY, enemy.Messaggio);
        }

        // write buffer once
        Console.SetCursorPosition(0, 0);
        var sb = new StringBuilder(viewWidth * (viewHeight + 2));
        for (int y = 0; y < viewHeight; y++)
        {
            for (int x = 0; x < viewWidth; x++) sb.Append(buf[y, x]);
            if (y < viewHeight - 1) sb.Append('\n');
        }
        Console.Write(sb.ToString());

        // disegna sprite colorati (3 linee) sopra il buffer
        foreach (var npc in npcs)
            DrawColoredSprite(npc.X - startX, npc.Y - startY, StickSprite, ConsoleColor.Green, viewWidth, viewHeight);

        if (enemy != null)
            DrawColoredSprite(enemy.X - startX, enemy.Y - startY, StickSprite, ConsoleColor.Red, viewWidth, viewHeight);

        int screenPX = player.X - startX;
        int screenPY = player.Y - startY;
        DrawColoredSprite(screenPX, screenPY, StickSprite, ConsoleColor.White, viewWidth, viewHeight);
        DrawHealthBarAt(screenPX, screenPY + 2, player.Hp, player.MaxHp, viewWidth);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.SetCursorPosition(0, viewHeight);
        string hud = $"Pos {player.X},{player.Y}  HP {player.Hp}/{player.MaxHp}  Mappa {mapWidth}x{mapHeight}  Aggro {(enemyAggro ? "ON" : "OFF")}";
        if (hud.Length > viewWidth) hud = hud.Substring(0, viewWidth);
        Console.Write(hud.PadRight(viewWidth));
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.SetCursorPosition(0, viewHeight + 1);
        string msg = ultimoMessaggio ?? string.Empty;
        if (msg.Length > viewWidth) msg = msg.Substring(0, viewWidth);
        Console.Write(msg.PadRight(viewWidth));
        Console.ResetColor();
    }

    static void GestisciInput()
    {
        if (!Console.KeyAvailable) return;

        ConsoleKey key = Console.ReadKey(true).Key;

        int dx = 0, dy = 0;

        switch (key)
        {
            case ConsoleKey.W:
            case ConsoleKey.UpArrow:
                dy = -1; break;
            case ConsoleKey.S:
            case ConsoleKey.DownArrow:
                dy = 1; break;
            case ConsoleKey.A:
            case ConsoleKey.LeftArrow:
                dx = -1; break;
            case ConsoleKey.D:
            case ConsoleKey.RightArrow:
                dx = 1; break;
            case ConsoleKey.Spacebar:
                Interagisci();
                return;
            default:
                return;
        }

        int newX = player.X + dx;
        int newY = player.Y + dy;
        lastDx = dx; lastDy = dy;

        if (Collides(newX, newY))
            return;

        player.X = newX;
        player.Y = newY;

        TriggerDoorIfOnTile();

        AggiornaNemico();
    }

    static void Interagisci()
    {
        // Porta (non usata per entrare ora)

        // NPC
        foreach (var npc in npcs)
        {
            int dist = Math.Abs(npc.X - player.X) + Math.Abs(npc.Y - player.Y);
            if (dist <= NpcGreetRange)
            {
                ultimoMessaggio = $"{npc.Nome}: {npc.Messaggio}";
                return;
            }
        }

        // Enemy greet if close
        if (enemy != null)
        {
            int distX = Math.Abs(enemy.X - player.X);
            int distY = Math.Abs(enemy.Y - player.Y);
            if (distX + distY <= EnemyGreetRange)
            {
                ultimoMessaggio = $"{enemy.Nome}: {enemy.Messaggio}";
                return;
            }
        }

        ultimoMessaggio = "Nessuna interazione disponibile.";
    }

    static bool TryGetNpcAt(int x, int y, out Npc npc)
    {
        foreach (var n in npcs)
        {
            if (n.X == x && n.Y == y)
            {
                npc = n;
                return true;
            }
        }

        npc = null;
        return false;
    }

    static bool TryGetDoorAt(int x, int y, out Door door)
    {
        foreach (var d in currentMap.Doors)
        {
            if (d.X == x && d.Y == y)
            {
                door = d;
                return true;
            }
        }
        door = null;
        return false;
    }

    static bool IsNpcBlocking(int x, int y)
    {
        foreach (var npc in npcs)
        {
            int dist = Math.Abs(npc.X - x) + Math.Abs(npc.Y - y);
            if (dist <= NpcCollisionRange)
                return true;
        }
        return false;
    }

    static bool Collides(int centerX, int centerY)
    {
        // Hitbox a croce (Manhattan <= 1): 5 celle invece di 3x3 pieno
        int half = 1;

        // se il centro è una porta, consenti (anche ai bordi)
        if (TryGetDoorAt(centerX, centerY, out _))
            return false;

        // controlla bordi mappa
        if (centerX - half < 0 || centerY - half < 0 || centerX + half >= currentMap.Width || centerY + half >= currentMap.Height)
            return true;

        for (int y = centerY - half; y <= centerY + half; y++)
        {
            for (int x = centerX - half; x <= centerX + half; x++)
            {
                int manhattan = Math.Abs(x - centerX) + Math.Abs(y - centerY);
                if (manhattan > 1) continue;

                if (TryGetDoorAt(x, y, out _))
                    continue;

                if (buildingBlocks.Contains((x, y)))
                    return true;

                char tile = currentMap.Tiles[y, x];
                if (tile == '#' || tile == '^' || tile == 'T' || tile == '/' || tile == '\\' || tile == '~')
                    return true;
                if (IsNpcBlocking(x, y))
                    return true;
            }
        }

        return false;
    }

    static bool CanEnemyMoveTo(int centerX, int centerY)
    {
        int half = 1;
        if (centerX - half < 0 || centerY - half < 0 || centerX + half >= currentMap.Width || centerY + half >= currentMap.Height)
            return false;

        // evitare sovrapposizione diretta al player
        if (centerX == player.X && centerY == player.Y) return false;

        for (int y = centerY - half; y <= centerY + half; y++)
        {
            for (int x = centerX - half; x <= centerX + half; x++)
            {
                int manhattan = Math.Abs(x - centerX) + Math.Abs(y - centerY);
                if (manhattan > 1) continue;

                if (buildingBlocks.Contains((x, y)))
                    return false;

                char tile = currentMap.Tiles[y, x];
                if (tile == '#' || tile == '^' || tile == 'T' || tile == '/' || tile == '\\' || tile == '~')
                    return false;

                // evita NPC hitbox
                foreach (var npc in npcs)
                    if (Math.Abs(npc.X - x) + Math.Abs(npc.Y - y) <= NpcCollisionRange)
                        return false;
            }
        }

        return true;
    }

    static void ProvaDannoNemico()
    {
        var now = DateTime.UtcNow;
        if ((now - lastEnemyHit).TotalMilliseconds < EnemyDamageCooldownMs)
            return;

        player.Hp = Math.Max(0, player.Hp - EnemyTouchDamage);
        lastEnemyHit = now;
        ultimoMessaggio = $"Sei stato colpito! HP: {player.Hp}/{player.MaxHp}";

        if (player.Hp <= 0)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Sei morto. Game Over.");
            Console.ResetColor();
            Environment.Exit(0);
        }
    }

    static void TriggerDoorIfOnTile()
    {
        if (lastDy == 0) return;
        if (TryGetDoorAt(player.X, player.Y, out var door))
        {
            bool ok = (currentMapId == MapId.Overworld && lastDy == -1) ||
                      (currentMapId != MapId.Overworld && lastDy == 1);
            if (ok)
            {
                CaricaMappa(door.TargetMap, door.TargetX, door.TargetY);
                ultimoMessaggio = $"Entrato in {door.TargetMap}";
            }
        }
    }

    static void AggiornaNemico()
    {
        if (enemy == null) return;

        int dist = Math.Abs(enemy.X - player.X) + Math.Abs(enemy.Y - player.Y);
        if (!enemyAggro && dist <= EnemyAggroStart) enemyAggro = true;
        if (enemyAggro && dist > EnemyAggroLose) { enemyAggro = false; return; }
        if (!enemyAggro) return;

        // danno a contatto
        if (dist <= 1)
        {
            ProvaDannoNemico();
            return; // non muove se è già in contatto
        }

        // rallenta il passo: muove solo ogni EnemyMoveInterval frame di input
        tickCounter = (tickCounter + 1) % EnemyMoveInterval;
        if (tickCounter != 0) return;

        int dx = Math.Sign(player.X - enemy.X);
        int dy = Math.Sign(player.Y - enemy.Y);

        // prova asse con distanza maggiore prima
        var moves = new List<(int dx, int dy)>();
        if (Math.Abs(player.X - enemy.X) >= Math.Abs(player.Y - enemy.Y))
        {
            moves.Add((dx, 0));
            moves.Add((0, dy));
        }
        else
        {
            moves.Add((0, dy));
            moves.Add((dx, 0));
        }

        foreach (var m in moves)
        {
            int nx = enemy.X + m.dx;
            int ny = enemy.Y + m.dy;
            if (CanEnemyMoveTo(nx, ny))
            {
                enemy.X = nx;
                enemy.Y = ny;
                break;
            }
        }
    }

    static void BlitBalloon(char[,] buf, int vw, int vh, int cx, int cy, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var lines = WrapText(text, Math.Min(30, vw));
        int topY = cy - 2 - lines.Count;
        if (topY < 0) return;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = $"({lines[i]})";
            int bx = cx - line.Length / 2;
            int by = topY + i;
            if (by < 0 || by >= vh) continue;
            for (int c = 0; c < line.Length; c++)
            {
                int x = bx + c;
                if (x < 0 || x >= vw) continue;
                buf[by, x] = line[c];
            }
        }
    }

    static List<string> WrapText(string text, int maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var current = new StringBuilder();
        foreach (var w in words)
        {
            if (current.Length + w.Length + (current.Length > 0 ? 1 : 0) > maxWidth)
            {
                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }
                if (w.Length > maxWidth)
                {
                    for (int i = 0; i < w.Length; i += maxWidth)
                        lines.Add(w.Substring(i, Math.Min(maxWidth, w.Length - i)));
                }
                else current.Append(w);
            }
            else
            {
                if (current.Length > 0) current.Append(' ');
                current.Append(w);
            }
        }
        if (current.Length > 0) lines.Add(current.ToString());
        return lines;
    }

    static readonly string[] StickSprite = { " O ", "/|\\", "/ \\" };

    static void BlitSprite(char[,] buf, int vw, int vh, int cx, int cy, char glyph)
    {
        if (cx < 0 || cy < 0 || cx >= vw || cy >= vh) return;
        buf[cy, cx] = glyph;
    }

    static void DrawColoredSprite(int cx, int cy, string[] sprite, ConsoleColor color, int vw, int vh)
    {
        for (int i = 0; i < sprite.Length; i++)
        {
            int sy = cy - 1 + i;
            if (sy < 0 || sy >= vh) continue;
            for (int c = 0; c < sprite[i].Length; c++)
            {
                int sx = cx - 1 + c;
                if (sx < 0 || sx >= vw) continue;
                Console.SetCursorPosition(sx, sy);
                Console.ForegroundColor = color;
                Console.Write(sprite[i][c]);
            }
        }
        Console.ResetColor();
    }

    static void DrawHealthBarAt(int cx, int y, int hp, int maxHp, int vw)
    {
        if (y < 0 || y >= Console.WindowHeight) return;
        int width = Math.Min(12, vw);
        hp = Math.Max(0, Math.Min(hp, maxHp));
        int filled = (int)Math.Round((double)hp / Math.Max(1, maxHp) * width);
        int startX = Math.Max(0, cx - width / 2);
        if (startX + width > vw) startX = Math.Max(0, vw - width);
        Console.SetCursorPosition(startX, y);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(new string('█', filled));
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(new string('░', width - filled));
        Console.ResetColor();
    }

    static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
