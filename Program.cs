using System;
using System.Collections.Generic;

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
    Torre
}

class Program
{
    static Player player = new Player { X = 4, Y = 4 };
    static MapData currentMap;
    static MapId currentMapId = MapId.Overworld;
    static readonly List<Npc> npcs = new List<Npc>();
    static Enemy enemy;
    static string ultimoMessaggio = "Barra spaziatrice per parlare con NPC o porte.";
    static int lastDx = 0, lastDy = 0;
    const int EnemyCollisionRange = 2;
    const int EnemyGreetRange = 5;
    const int NpcGreetRange = 4;
    const int NpcCollisionRange = 2;

    static void Main()
    {
        Console.CursorVisible = false;
        CaricaMappa(MapId.Overworld, 4, 4);

        while (true)
        {
            Disegna();
            Muovi();
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
            enemy = new Enemy("Nemico", 90, 40, "Ehi, non avvicinarti troppo!");
        }
        else
        {
            enemy = null;
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
            _ => (120, 40)
        };

    static (int X, int Y) GetInteriorSpawn(MapId id)
    {
        var size = GetMapSize(id);
        return (size.Width / 2, Math.Max(1, size.Height - 3));
    }

    static MapData BuildMap(MapId id)
    {
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
        int roofY = startY;
        int wallTop = startY + 1;
        int wallBottom = startY + height - 1;
        int wallLeft = startX;
        int wallRight = startX + width - 1;
        int doorX = wallLeft + width / 2;
        int doorY = wallBottom;

        // Tetto e cornice
        for (int x = wallLeft; x <= wallRight && x < map.Width - 1; x++)
        {
            char ch = '^';
            if (x == wallLeft) ch = '/';
            else if (x == wallRight) ch = '\\';
            mappa[roofY, x] = ch;
        }

        int tetto2 = roofY + 1;
        if (tetto2 < map.Height - 1)
            for (int x = wallLeft; x <= wallRight && x < map.Width - 1; x++)
            {
                char ch = (x == wallLeft) ? '/' : (x == wallRight ? '\\' : '~');
                mappa[tetto2, x] = ch;
            }

        // Muri esterni pieni
        for (int y = tetto2 + 1; y <= wallBottom && y < map.Height - 1; y++)
            for (int x = wallLeft; x <= wallRight && x < map.Width - 1; x++)
            {
                bool bordo = y == tetto2 + 1 || y == wallBottom || x == wallLeft || x == wallRight;
                mappa[y, x] = bordo ? '|' : '#'; // interno non visibile
            }

        // Finestre multiple su due file
        void DisegnaFinestra(int fx, int fy)
        {
            if (fx >= wallLeft + 1 && fx + 1 < wallRight && fy < map.Height && fy > roofY + 1)
            {
                mappa[fy, fx] = '[';
                mappa[fy, fx + 1] = ']';
            }
        }

        int finestraY1 = tetto2 + 2;
        int finestraY2 = tetto2 + 4;
        if (finestraY1 < wallBottom)
        {
            for (int fx = wallLeft + 2; fx < wallRight - 1; fx += 3)
                DisegnaFinestra(fx, finestraY1);
        }
        if (finestraY2 < wallBottom)
        {
            for (int fx = wallLeft + 1; fx < wallRight - 1; fx += 3)
                DisegnaFinestra(fx, finestraY2);
        }

        // Porta centrale
        if (doorY < map.Height && doorX < map.Width)
        {
            mappa[doorY, doorX] = 'H';
            if (doorY - 1 >= wallTop) mappa[doorY - 1, doorX] = 'H';
            map.Doors.Add(new Door
            {
                X = doorX,
                Y = doorY,
                TargetMap = targetMap,
                TargetX = GetInteriorSpawn(targetMap).X,
                TargetY = GetInteriorSpawn(targetMap).Y
            });
        }

        // Insegna
        if (!string.IsNullOrEmpty(label))
        {
            int textStart = Math.Max(wallLeft + 1, Math.Min(wallRight - label.Length, wallLeft + 1));
            if (textStart < wallRight && wallTop + 1 < map.Height - 1)
                for (int i = 0; i < label.Length && textStart + i < wallRight; i++)
                    mappa[wallTop + 1, textStart + i] = label[i];
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

        Console.SetCursorPosition(0, 0);

        for (int y = 0; y < viewHeight; y++)
        {
            int mapY = startY + y;

            for (int x = 0; x < viewWidth; x++)
            {
                int mapX = startX + x;

                if (enemy != null && enemy.X == mapX && enemy.Y == mapY)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write('E');
                    Console.ResetColor();
                }
                else if (TryGetDoorAt(mapX, mapY, out _))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write('+');
                    Console.ResetColor();
                }
                else
                {
                    Console.Write(mappa[mapY, mapX]);
                }
            }

            if (y < viewHeight - 1)
                Console.WriteLine();
        }

        DisegnaSprite(startX, startY, viewWidth, viewHeight);

        Console.SetCursorPosition(0, viewHeight);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"Pos {player.X},{player.Y}  Mappa {mapWidth}x{mapHeight}  WASD/Frecce movimento  SPAZIO interagisci");
        if (Console.CursorLeft < Console.WindowWidth)
            Console.Write(new string(' ', Console.WindowWidth - Console.CursorLeft));
        Console.ResetColor();

        Console.SetCursorPosition(0, viewHeight + 1);
        Console.ForegroundColor = ConsoleColor.Gray;
        string msg = ultimoMessaggio ?? string.Empty;
        if (msg.Length > Console.WindowWidth) msg = msg.Substring(0, Console.WindowWidth - 1);
        Console.Write(msg);
        if (Console.CursorLeft < Console.WindowWidth)
            Console.Write(new string(' ', Console.WindowWidth - Console.CursorLeft));
        Console.ResetColor();
    }

    static void Muovi()
    {
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

                char tile = currentMap.Tiles[y, x];
                if (tile == '#' || tile == '^' || tile == 'T' || tile == '/' || tile == '\\' || tile == '~')
                    return true;
                if (IsNpcBlocking(x, y))
                    return true;
                if (enemy != null && Math.Abs(enemy.X - x) + Math.Abs(enemy.Y - y) <= EnemyCollisionRange)
                    return true;
            }
        }

        return false;
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

    static void DisegnaSprite(int viewStartX, int viewStartY, int viewWidth, int viewHeight)
    {
        int px = player.X - viewStartX;
        int py = player.Y - viewStartY;
        if (px < 0 || px >= viewWidth || py < 0 || py >= viewHeight) return;

        string[] sprite = GetPlayerSpriteLines();
        for (int i = 0; i < sprite.Length; i++)
        {
            int drawY = py - 1 + i; // linea centrale sul tile del player
            if (drawY < 0 || drawY >= viewHeight) continue;

            int drawX = px - 1; // sprite largo 3 caratteri
            for (int c = 0; c < sprite[i].Length; c++)
            {
                int sx = drawX + c;
                if (sx < 0 || sx >= viewWidth) continue;

                Console.SetCursorPosition(sx, drawY);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(sprite[i][c]);
                Console.ResetColor();
            }
        }

        // NPC sprite (verde) + balloon se vicino
        foreach (var npc in npcs)
        {
            int nx = npc.X - viewStartX;
            int ny = npc.Y - viewStartY;
            if (nx >= 0 && nx < viewWidth && ny >= 0 && ny < viewHeight)
            {
                DrawSpriteAt(nx, ny, sprite, ConsoleColor.Green, viewWidth, viewHeight);

                int dist = Math.Abs(npc.X - player.X) + Math.Abs(npc.Y - player.Y);
                if (dist <= NpcGreetRange)
                    DrawBalloon(nx, ny, npc.Messaggio, ConsoleColor.Green, viewWidth, viewHeight);
            }
        }

        // Enemy sprite (rosso) se in viewport
        if (enemy != null)
        {
            int ex = enemy.X - viewStartX;
            int ey = enemy.Y - viewStartY;
            if (ex >= 0 && ex < viewWidth && ey >= 0 && ey < viewHeight)
            {
                DrawSpriteAt(ex, ey, sprite, ConsoleColor.Red, viewWidth, viewHeight);

                // Balloon se il player è vicino
                int dist = Math.Abs(enemy.X - player.X) + Math.Abs(enemy.Y - player.Y);
                if (dist <= EnemyGreetRange)
                {
                    DrawBalloon(ex, ey, enemy.Messaggio, ConsoleColor.Red, viewWidth, viewHeight);
                }
            }
        }

        // Player sprite (bianco) sopra tutti
        DrawSpriteAt(px, py, sprite, ConsoleColor.White, viewWidth, viewHeight);
    }

    static void DrawSpriteAt(int centerX, int centerY, string[] sprite, ConsoleColor color, int viewWidth, int viewHeight)
    {
        for (int i = 0; i < sprite.Length; i++)
        {
            int drawY = centerY - 1 + i;
            if (drawY < 0 || drawY >= viewHeight) continue;

            int drawX = centerX - 1;
            for (int c = 0; c < sprite[i].Length; c++)
            {
                int sx = drawX + c;
                if (sx < 0 || sx >= viewWidth) continue;

                Console.SetCursorPosition(sx, drawY);
                Console.ForegroundColor = color;
                Console.Write(sprite[i][c]);
                Console.ResetColor();
            }
        }
    }

    static void DrawBalloon(int centerX, int centerY, string message, ConsoleColor color, int viewWidth, int viewHeight)
    {
        if (string.IsNullOrEmpty(message)) return;
        int maxWidth = Math.Min(viewWidth, 30);
        var lines = WrapText(message, maxWidth);
        int topY = centerY - GetPlayerSpriteLines().Length - lines.Count;
        if (topY < 0) return;

        for (int li = 0; li < lines.Count; li++)
        {
            string line = $"({lines[li]})";
            int bx = centerX - line.Length / 2;
            int by = topY + li;
            if (by < 0 || by >= viewHeight) continue;

            for (int i = 0; i < line.Length; i++)
            {
                int sx = bx + i;
                if (sx < 0 || sx >= viewWidth) continue;
                Console.SetCursorPosition(sx, by);
                Console.ForegroundColor = color;
                Console.Write(line[i]);
                Console.ResetColor();
            }
        }
    }

    static List<string> WrapText(string text, int maxWidth)
    {
        var lines = new List<string>();
        string[] words = text.Split(' ');
        var current = new System.Text.StringBuilder();
        foreach (var word in words)
        {
            if (current.Length + word.Length + (current.Length > 0 ? 1 : 0) > maxWidth)
            {
                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                if (word.Length > maxWidth)
                {
                    for (int i = 0; i < word.Length; i += maxWidth)
                        lines.Add(word.Substring(i, Math.Min(maxWidth, word.Length - i)));
                }
                else
                {
                    current.Append(word);
                }
            }
            else
            {
                if (current.Length > 0) current.Append(' ');
                current.Append(word);
            }
        }
        if (current.Length > 0) lines.Add(current.ToString());
        return lines;
    }

    static string[] GetPlayerSpriteLines() => new[] { " O ", "/|\\", "/ \\" };

    static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}