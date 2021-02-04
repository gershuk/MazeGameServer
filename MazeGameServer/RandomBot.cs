using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeGame.Server
{
    class RandomBot : Bot
    {
        private GameMap _map;
        private Vector2Int _endPoint;
        private Random _rand;

        public override void Init (GameMap map, Vector2Int startPosition, Vector2Int endPoint)
        {
            Position = startPosition;
            _rand = new();
            _endPoint = endPoint;
            _map = map;
        }

        public override bool Move ()
        {
            List<Vector2Int> vector2Ints = new() { new(-1, 0), new(1, 0), new(0, 1), new(0, -1) };
            List<Vector2Int> newCoord = new(4);
            foreach (var vector in vector2Ints)
            {
                var pos = Position + vector;
                if (!(pos.X < 0 || pos.X > _map.Size.X - 1 || pos.Y < 0 || pos.Y > _map.Size.Y - 1))
                {
                    if (!_map.Walls[pos.X, pos.Y])
                    newCoord.Add(pos);
                }
            }

            var index = _rand.Next();
            index %= newCoord.Count;
            Position = newCoord[index];

            return Position == _endPoint;
        }
    }
}
