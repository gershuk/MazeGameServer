#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace MazeGame.Server
{
    public sealed record GameMap ([NotNull] string Name, uint MaxPlayerCount, Vector2Int Size, [NotNull] bool[,] Walls, [NotNull] Vector2Int[] Spawns, [NotNull] Vector2Int[] Exits, Guid Guid);
    public struct Vector2Int
    {
        public int X;
        public int Y;
        public Vector2Int (int x, int y) => (X, Y) = (x, y);
        public static bool operator == (Vector2Int a, Vector2Int b) => (a.X == b.X) && (a.Y == b.Y);
        public static bool operator != (Vector2Int a, Vector2Int b) => a == b;
        public static Vector2Int operator - (Vector2Int a, Vector2Int b) => new((a.X - b.X), (a.Y - b.Y));
        public static Vector2Int operator + (Vector2Int a, Vector2Int b) => new((a.X + b.X), (a.Y + b.Y));

        public override bool Equals (object? obj) => obj is Vector2Int @int && X == @int.X && Y == @int.Y;
        public override int GetHashCode () => HashCode.Combine(X, Y);
    }

    public static class MapReader
    {
        public static GameMap ReadGameMap (string path)
        {
            using var fileReader = new StreamReader(path);
            var name = fileReader.ReadLine();
            if (name == null || name == string.Empty)
                throw new UnplayableConfigException();

            var maxPlayerCount = Convert.ToUInt32(fileReader.ReadLine());

            var wh = fileReader.ReadLine().Split(' ');
            var w = Convert.ToInt32(wh[0]);
            var h = Convert.ToInt32(wh[1]);
            var walls = new bool[w, h];
            for (var i = 0; i < h; ++i)
            {
                var str = fileReader.ReadLine();
                if (str == null || str.Length != w)
                    throw new UnplayableConfigException();
                for (var j = 0; j < str.Length; ++j)
                {
                    walls[j, i] = str[j] != '.';
                }
            }

            var spawnCount = Convert.ToInt32(fileReader.ReadLine());
            if (spawnCount < maxPlayerCount)
                throw new UnplayableConfigException();
            var spawns = new Vector2Int[spawnCount];
            for (var i = 0; i < spawnCount; ++i)
            {
                var v2 = fileReader.ReadLine().Split(' ');
                spawns[i].X = Convert.ToInt32(v2[0]);
                spawns[i].Y = Convert.ToInt32(v2[1]);
                if (walls[spawns[i].X, spawns[i].Y])
                    throw new UnplayableConfigException();
            }

            var exitCount = Convert.ToInt32(fileReader.ReadLine());
            if (exitCount > w * h / 4)
                throw new UnplayableConfigException();
            var exits = new Vector2Int[exitCount];

            if (exitCount + spawnCount > w * h)
                throw new UnplayableConfigException();

            for (var i = 0; i < exitCount; ++i)
            {
                var v2 = fileReader.ReadLine().Split(' ');
                exits[i].X = Convert.ToInt32(v2[0]);
                exits[i].Y = Convert.ToInt32(v2[1]);
                if (walls[exits[i].X, exits[i].Y])
                    throw new UnplayableConfigException();
            }

            var guid = new Guid(fileReader.ReadLine());

            return new GameMap(name, maxPlayerCount, new Vector2Int() { X = w, Y = h }, walls, spawns, exits, guid);
        }
    }

    public class MapStorage
    {
        private readonly Dictionary<Guid, GameMap> _gameMaps;

        public MapStorage () => _gameMaps = new();

        public MapStorage ([NotNull] Dictionary<Guid, GameMap> gameMaps) => _gameMaps = gameMaps;

        public Guid AddMap (GameMap map)
        {
            if (_gameMaps.ContainsKey(map.Guid))
                map = map with { Guid = Guid.NewGuid() };
            _gameMaps[map.Guid] = map;
            return map.Guid;
        }

        public bool DeleteMap (Guid guid) => _gameMaps.Remove(guid);

        public bool TryGetMap (Guid guid, out GameMap? map) => _gameMaps.TryGetValue(guid, out map);

        public (string name, Guid guid, uint playerCount, Vector2Int size)[] GetShotMapsInfo () => _gameMaps.Select(m => (m.Value.Name, m.Value.Guid, m.Value.MaxPlayerCount, m.Value.Size)).ToArray();
    }
}
