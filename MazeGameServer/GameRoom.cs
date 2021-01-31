#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using AsyncExtensions;

namespace MazeGame.Server
{
    public enum RoomStatus
    {
        Lobby,
        GameStrated,
        GameEnded,
        Destroyed,
    }

    public record LobbyInfo (
        Guid Guid,
        RoomStatus RoomStatus,
        string RoomName,
        string Description,
        List<string> PlayerNames,
        List<string> BotTypes,
        bool HasPassword,
        uint MaxPlayerCount,
        string OwnerName,
        Guid MapGuid);

    public record GameInfo (List<(Vector2, int)> Blocks, List<(Vector2, string)> Players);

    public class GameRoom
    {
        private readonly Func<Guid, string> _getNameByGuid;
        private readonly ConcurrentDictionary<Guid, AsyncBuffer<LobbyInfo>> _lobbyInfoBuffers;
        private readonly ConcurrentDictionary<Guid, AsyncBuffer<GameInfo>> _gameInfoBuffers;

        public string Name { get; set; }
        public string Description { get; set; }
        public GameMap Map { get; set; }
        public string? Password { get; set; }
        public uint MaxPlayerCount { get; set; }

        public List<(string name, Bot bot)> Bots { get; set; }
        public Guid OwnerGuid { get; set; }
        public HashSet<Guid> PlayerGuids { get; private set; }
        public Guid Guid { get; init; }

        public RoomStatus Status { get; private set; }

        public async Task BroadcastLobbyInfo ()
        {
            foreach (var asyncBuffer in _lobbyInfoBuffers)
                await asyncBuffer.Value.AsyncWrite(GetInfo());
        }

        public GameRoom (string name,
                         string description,
                         GameMap map,
                         string? password,
                         uint maxPlayerCount,
                         List<(string name, Bot bot)> bots,
                         Guid ownerGuid,
                         Func<Guid, string> getNameByGuid,
                         Guid guid)
        {
            Name = name;
            Description = description;
            Map = map;
            Password = password;
            MaxPlayerCount = maxPlayerCount;
            Bots = bots;
            OwnerGuid = ownerGuid;
            Guid = guid;

            _lobbyInfoBuffers = new();
            _gameInfoBuffers = new();
            PlayerGuids = new();
            Status = RoomStatus.Lobby;
            _getNameByGuid = getNameByGuid;
        }

        public LobbyInfo GetInfo () => new LobbyInfo(Guid,
                                                     Status,
                                                     Name,
                                                     Description,
                                                     PlayerGuids.Select(p => _getNameByGuid(p)).ToList(),
                                                     Bots.Select(b => b.name).ToList(),
                                                     Password != null,
                                                     MaxPlayerCount,
                                                     _getNameByGuid(OwnerGuid),
                                                     Map.Guid);

        public async Task<AsyncBuffer<LobbyInfo>> AddPlayer (Guid playerGuid, string password)
        {
            if (PlayerGuids.Contains(playerGuid))
                throw new PlayerAlreadyConnectedToThisRoomException();

            if (Status != RoomStatus.Lobby)
                throw new CantConnectToStartedGameException();

            if (MaxPlayerCount == PlayerGuids.Count)
                throw new PlayerLimitException();

            if (Password != null && Password != password)
                throw new WrongPasswordException();

            PlayerGuids.Add(playerGuid);
            AsyncBuffer<LobbyInfo> asyncBuffer = new();
            _lobbyInfoBuffers[playerGuid] = asyncBuffer;
            await BroadcastLobbyInfo();
            return asyncBuffer;
        }

        public async Task DisconnectPLayer (Guid playerGuid)
        {
            if (!PlayerGuids.Contains(playerGuid))
                throw new PlayerNotFoundInRoomException();

            PlayerGuids.Remove(playerGuid);
            _lobbyInfoBuffers.Remove(playerGuid, out var asyncBuffer);
            await BroadcastLobbyInfo();
            asyncBuffer!.Dispose();
        }

        public AsyncBuffer<GameInfo> SpectateGame (Guid playerGuid)
        {
            if (_gameInfoBuffers.ContainsKey(playerGuid))
                throw new PlayerAlreadySpectedThisRoomException();

            _gameInfoBuffers[playerGuid] = new();
            return _gameInfoBuffers[playerGuid];
        }

        public void StopSpectatingGame (Guid playerGuid)
        {
            if (_gameInfoBuffers.ContainsKey(playerGuid))
                throw new SpectatingChannelNotFoundException();

            _gameInfoBuffers.Remove(playerGuid, out var asyncBuffer);
            asyncBuffer!.Dispose();
        }

        public async Task Destroy ()
        {
            await BroadcastLobbyInfo();
            Status = RoomStatus.Destroyed;
            foreach (var buffer in _lobbyInfoBuffers)
                buffer.Value.Dispose();
        }
    }
}
