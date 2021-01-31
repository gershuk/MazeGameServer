﻿#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace MazeGame.Server
{
    public abstract class Bot
    {

    }

    public class SimpleBot : Bot { }

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
