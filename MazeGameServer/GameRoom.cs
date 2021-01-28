#nullable enable

using System;
using System.Collections.Generic;

namespace MazeGame.Server
{
    public enum RoomStatus
    {
        Looby,
        GameStrated,
        GameEnded
    }

    public class GameRoom
    {
        private string Name { get; set; }
        private string Description { get; set; }
        //ToDo : change to reference
        public string Map { get; set; }
        public string Password { get; set; }
        public uint MaxPlayerCount { get; set; }
        //ToDo : change factory id's
        //private repeated BotType botTypes = 8;
        public Guid OwnerGuid { get; set; }
        public HashSet<Guid> PlayerGuids { get; private set; }

        public RoomStatus Status { get; private set; }
    }
}
