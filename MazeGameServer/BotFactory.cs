#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace MazeGame.Server
{
    public abstract class Bot
    {
        public Vector2Int Position { get; private set; }
        public abstract void Init (GameMap map, Vector2Int position);
        public abstract bool Move ();
    }

    public class SimpleBot : Bot
    {
        public override void Init (GameMap map, Vector2Int position) => throw new NotImplementedException();
        public override bool Move () => throw new NotImplementedException();
    }

    public class BotFactory
    {
        private readonly Dictionary<string, Func<Bot>> _botCreators;

        public BotFactory (params (string name, Func<Bot> creator)[] botCreators)
        {
            _botCreators = new(botCreators.Length);
            AddCreators(botCreators);
        }

        public List<string> GetBotTypeNames () => _botCreators.Select((x) => x.Key).ToList();

        public void AddCreators (params (string name, Func<Bot> creator)[] botCreators)
        {
            foreach (var (name, creator) in botCreators)
            {
                _botCreators.Add(name, creator);
            }
        }

        public Bot CreatBot (string name) => _botCreators[name]();
    }
}
