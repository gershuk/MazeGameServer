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
        Guid LobbyGuid,
        RoomStatus RoomStatus,
        string RoomName,
        string Description,
        List<string> PlayerNames,
        List<string> BotTypes,
        bool HasPassword,
        uint MaxPlayerCount,
        string OwnerName,
        Guid MapGuid,
        uint TurnsCount,
        uint TurnDeley);

    public class GameRoom
    {
        private readonly Func<Guid, string> _getNameByGuid;
        private readonly ConcurrentDictionary<Guid, AsyncBuffer<LobbyInfo>> _lobbyInfoBuffers;
        private MazeGameModel? _mazeGame;

        public string Name { get; set; }
        public string Description { get; set; }
        public GameMap Map { get; set; }
        public string? Password { get; set; }
        public uint MaxPlayerCount { get; set; }
        public uint TurnsCount { get; set; }
        public uint TurnsDeley { get; set; }

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
                         Guid guid,
                         uint turnsCount,
                         uint turnDeley)
        {
            Name = name;
            Description = description;
            Map = map;
            Password = password;
            MaxPlayerCount = maxPlayerCount;
            Bots = bots;
            OwnerGuid = ownerGuid;
            Guid = guid;
            TurnsCount = turnsCount;

            _lobbyInfoBuffers = new();
            PlayerGuids = new();
            Status = RoomStatus.Lobby;
            _getNameByGuid = getNameByGuid;
            TurnsDeley = turnDeley;
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
                                                     Map.Guid,
                                                     TurnsCount,
                                                     TurnsDeley);

        public async Task<AsyncBuffer<LobbyInfo>> AddPlayer (Guid playerGuid, string password, bool isReconnect)
        {
            if (Password != null && Password != password)
                throw new WrongPasswordException();

            if (!isReconnect)
            {
                if (PlayerGuids.Contains(playerGuid))
                    throw new PlayerAlreadyConnectedToThisRoomException();

                if (Status != RoomStatus.Lobby)
                    throw new CantConnectToStartedGameException();

                if (MaxPlayerCount == PlayerGuids.Count + Bots.Count)
                    throw new PlayerLimitException();

                PlayerGuids.Add(playerGuid);
            }

            AsyncBuffer<LobbyInfo> asyncBuffer = new();
            if (isReconnect)
                _lobbyInfoBuffers[playerGuid]?.Dispose();
            _lobbyInfoBuffers[playerGuid] = asyncBuffer;
            await BroadcastLobbyInfo();
            return asyncBuffer;
        }

        public void SetPlayerDirection (Guid guid, Direction direction, uint turn)
        {
            if (Status != RoomStatus.GameStrated)
                throw new GameNotStartedException();

            _mazeGame.SetPlayerDirection(guid, direction, turn);
        }

        public async Task DisconnectPLayer (Guid playerGuid)
        {
            if (!PlayerGuids.Contains(playerGuid))
                throw new PlayerNotFoundInRoomException();

            PlayerGuids.Remove(playerGuid);
            _lobbyInfoBuffers.Remove(playerGuid, out var asyncBuffer);
            try
            {
                _mazeGame?.DeletePlayer(playerGuid);
            }
            finally
            {
                await BroadcastLobbyInfo();
                asyncBuffer!.Dispose();
            }
        }

        public void StopSpectatingGame (Guid playerGuid)
        {
            if (Status != RoomStatus.GameStrated)
                throw new GameNotStartedException();

            _mazeGame.DeleteSpectator(playerGuid);
        }

        public async Task Destroy ()
        {
            _mazeGame?.Dispose();
            Status = RoomStatus.GameEnded;
            await BroadcastLobbyInfo();
            foreach (var buffer in _lobbyInfoBuffers)
                buffer.Value.Dispose();
            Status = RoomStatus.Destroyed;
        }

        public async Task StartGame (Action deleteRoom, Action<Guid> deletePlayer)
        {
            if (Status != RoomStatus.Lobby)
                throw new GameAlreadyStartedException();
            if (PlayerGuids.Count + Bots.Count == 0)
                throw new ThereIsNoPlayersOrBotsInRoomException();

            Status = RoomStatus.GameStrated;
            _mazeGame = new MazeGameModel(deleteRoom, deletePlayer, Map, PlayerGuids.Select(p => (p, _getNameByGuid(p))).ToList(), Bots, TurnsCount, TurnsDeley);
            await BroadcastLobbyInfo();
            await _mazeGame.StartGame();
        }

        public async Task<AsyncBuffer<GameInfo>> GetVisualData (Guid playerGuid, bool isReconnect)
        {
            return Status == RoomStatus.GameStrated
                ? PlayerGuids.Contains(playerGuid)
                ? await _mazeGame.GetPlayerData(playerGuid)
                : await _mazeGame.AddSpectator(playerGuid, isReconnect)
                : throw new GameNotStartedException();
        }
    }
}
