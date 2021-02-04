#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;


using AsyncExtensions;

namespace MazeGame.Server
{
    public interface IGameServerModel
    {
        BotFactory BotFactory { get; init; }
        string DbConnectionString { get; }
        ConcurrentDictionary<Guid, GameRoom> GameRooms { get; }
        ConcurrentDictionary<string, Guid> LoginToGuid { get; }
        MapStorage MapStorage { get; }
        ConcurrentDictionary<Guid, PlayerConnection> PlayerConnections { get; }
        ConcurrentDictionary<Guid, GameRoom> Rooms { get; }

        Task<Guid> AuthorizePlayer (string login, string password, bool killLastConnection, string socketInfo);
        Task ClosePlayerConnection (Guid guid);
        Task<AsyncBuffer<LobbyInfo>> ConnectToRoom (Guid playerGuid, Guid roomGuid, string password);
        Task<Guid> CreateRoom (Guid playerGuid, string name, string description, Guid mapGuid, uint maxPlayerCount, string password, bool hasPassword, uint turnsCount, uint turnsDeley, params string[] botTypes);
        Task<bool> DeleteRoom (Guid playerGuid, Guid roomGuid);
        Task DisconnectFromRoom (Guid guid, bool isSelf);
        Task<(Guid? createdRoomGuid, Guid? connectToRoomGuid, PlayerState playerState)> GetPlayerState (Guid playerGuid);
        List<LobbyInfo> GetRoomList ();
        Task<AsyncBuffer<GameInfo>> GetVisualData (Guid playerGuid, Guid roomGuid, bool isReconnect);
        Task KickPlayer (Guid playerGuid, Guid room, string targetLogin);
        Task RegisterNewPlayer (string login, string password);
        Task SetPlayerDirection (Guid playerGuid, Guid roomGuid, Direction direction, uint turn);
        Task StartGame (Guid playerGuid, Guid roomGuid);
        Task StopSpectating (Guid playerGuid, Guid roomGuid);
    }

    public class GameServerModel : IGameServerModel
    {
        private readonly AsyncManualResetEvent _connectionsPoolLocker = new();
        private readonly AsyncManualResetEvent _roomTableLocker = new();
        private readonly Regex _loginCheckRegex = new(@"[^\w]+", RegexOptions.Compiled);

        public BotFactory BotFactory { get; init; }

        public string DbConnectionString { get; private set; }

        public ConcurrentDictionary<Guid, GameRoom> GameRooms { get; private set; }

        public ConcurrentDictionary<Guid, PlayerConnection> PlayerConnections { get; private set; }

        public ConcurrentDictionary<string, Guid> LoginToGuid { get; private set; }

        public ConcurrentDictionary<Guid, GameRoom> Rooms { get; private set; }

        public MapStorage MapStorage { get; private set; }

        //ToDo : load data from config
        public GameServerModel ()
        {
            MapStorage = new();

            var dirInfo = new DirectoryInfo(@"Maps");
            foreach (var file in dirInfo.GetFiles())
            {
                try
                {
                    MapStorage.AddMap(MapReader.ReadGameMap(file.FullName));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            Rooms = new();
            _connectionsPoolLocker.Set();
            _roomTableLocker.Set();
            DbConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=usersdb;Integrated Security=True";
            GameRooms = new();
            PlayerConnections = new();
            LoginToGuid = new();

            BotFactory = new BotFactory(("AStarBot", () => new AStrarBot()), ("RandomBot", () => new RandomBot()));
        }

        private bool IsConnectionGuidExist (Guid guid, out PlayerConnection? connection) => PlayerConnections.TryGetValue(guid, out connection);

        private static bool IfConnectionSame (string c1, string c2) => c1.Split(":")[0] == c2.Split(":")[0];

        public async Task<Guid> AuthorizePlayer (string login, string password, bool killLastConnection, string socketInfo)
        {
            var sqlExpression = $"SELECT * FROM UsersData WHERE Login = '{login}'";
            using SqlConnection connection = new(DbConnectionString);
            await connection.OpenAsync();
            using SqlCommand command = new(sqlExpression, connection);
            using var reader = await command.ExecuteReaderAsync();

            if (!reader.HasRows)
                throw new LoginNotExistException();

            await reader.ReadAsync();

            if (reader.GetString(0).TrimEnd() != login)
                throw new LoginNotExistException();

            var real_password = reader.GetString(1);

            if (real_password.TrimEnd() != password)
                throw new WrongPasswordException();

            await _connectionsPoolLocker.WaitAsync();

            var isExist = LoginToGuid.TryGetValue(login, out var guid);
            try
            {
                if (!(isExist && IfConnectionSame(PlayerConnections[LoginToGuid[login]].SocketInfo, socketInfo)))
                {
                    if (!killLastConnection && isExist)
                        throw new LastConnectionStillOpenException();
                    if (isExist)
                    {
                        if (!PlayerConnections.TryRemove(guid, out _))
                            throw new ConnectionPoolUndefinedException();
                    }

                    guid = Guid.NewGuid();
                    LoginToGuid[login] = guid;
                    PlayerConnections[guid] = new PlayerConnection(login, socketInfo);
                }
            }
            catch
            {
                await _connectionsPoolLocker.WaitAsync();
                _connectionsPoolLocker.Reset();

                guid = Guid.NewGuid();
                LoginToGuid[login] = guid;
                PlayerConnections[guid] = new PlayerConnection(login, socketInfo);

                _connectionsPoolLocker.Set();
            }

            return guid;
        }

        public async Task RegisterNewPlayer (string login, string password)
        {

            if (_loginCheckRegex.Matches(login).Count > 0)
                throw new BadRegistrationDataException();

            using SqlConnection connection = new(DbConnectionString);
            await connection.OpenAsync();
            using SqlCommand readCommand = new($"SELECT * FROM UsersData WHERE Login = '{login}'", connection);
            using var reader = await readCommand.ExecuteReaderAsync();
            if (reader.HasRows)
                throw new LoginAlreadyRegistredException();

            await reader.CloseAsync();
            using SqlCommand insertCommand = new($"INSERT INTO UsersData (Login, Password) VALUES ('{login}', '{password}')", connection);
            var count = await insertCommand.ExecuteNonQueryAsync();

            if (count != 1)
                throw new DbException();
        }

        public async Task ClosePlayerConnection (Guid guid)
        {
            await _connectionsPoolLocker.WaitAsync();

            if (!IsConnectionGuidExist(guid, out var connection))
                throw new PlayerGuidNotFoundException();

            if (connection!.CurrentGameRoomGuid.HasValue)
            {
                await Rooms[connection.CurrentGameRoomGuid.Value].DisconnectPLayer(guid);
            }

            foreach (var spectatingGuid in connection.GetSpectatingGuids())
            {
                if (Rooms.TryGetValue(spectatingGuid, out var gameRoom))
                {
                    gameRoom.StopSpectatingGame(guid);
                }
            }

            if (connection.CreatedRoomGuid.HasValue && Rooms[connection.CreatedRoomGuid!.Value].Status == RoomStatus.Lobby)
                await DeleteRoom(guid, connection.CreatedRoomGuid.Value);

            PlayerConnections.TryRemove(guid, out _);
            LoginToGuid.TryRemove(connection!.Login, out _);
        }

        public async Task<AsyncBuffer<LobbyInfo>> ConnectToRoom (Guid playerGuid, Guid roomGuid, string password)
        {
            await _connectionsPoolLocker.WaitAsync();
            if (!IsConnectionGuidExist(playerGuid, out var connection))
                throw new PlayerGuidNotFoundException();

            if (!Rooms.TryGetValue(roomGuid, out var gameRoom))
                throw new RoomNotExistException();

            var isReconnect = connection!.CurrentGameRoomGuid == roomGuid;

            var ans = await gameRoom.AddPlayer(playerGuid, password, isReconnect);

            if (!isReconnect)
                connection.ConnectToRoom(roomGuid);
            return ans;
        }

        public async Task DisconnectFromRoom (Guid guid, bool isSelf)
        {
            await _connectionsPoolLocker.WaitAsync();
            if (!IsConnectionGuidExist(guid, out var connection))
            {
                if (isSelf)
                    throw new PlayerGuidNotFoundException();
                else
                    throw new TargetGuidNotFoundException();
            }


            var gameRoomGuid = connection!.CurrentGameRoomGuid;
            connection.DisconnectFromRoom();

            if (gameRoomGuid.HasValue)
            {
                if (!Rooms.TryGetValue(gameRoomGuid.Value, out var gameRoom))
                    throw new RoomNotExistException();

                await gameRoom.DisconnectPLayer(guid);
            }
            else
            {
                throw new PlayerNotInRoomException();
            }
        }

        public async Task<Guid> CreateRoom (Guid playerGuid, string name, string description, Guid mapGuid, uint maxPlayerCount,
            string password, bool hasPassword, uint turnsCount, uint turnsDeley, params string[] botTypes)
        {
            await _connectionsPoolLocker.WaitAsync();
            if (!IsConnectionGuidExist(playerGuid, out var connection))
                throw new PlayerGuidNotFoundException();

            if (!connection!.CanCreateRoom())
                throw new RoomAlreadyCreatedException();

            if (!MapStorage.TryGetMap(mapGuid, out var map))
                throw new MapNotFoundException();

            if (map!.MaxPlayerCount < maxPlayerCount)
                throw new MaxPlayerMoreThenSpawnerException();

            if (maxPlayerCount == 0 && turnsDeley < 100)
                throw new UnplayableConfigException();

            if (maxPlayerCount < botTypes.Length)
                throw new ThereAreMoreBotsThanEmptySlots();

            List<(string name, Bot bot)> bots = new(botTypes.Length);

            try
            {
                foreach (var botType in botTypes)
                    bots.Add((botType, BotFactory.CreatBot(botType)));
            }
            catch
            {
                throw new WrongBotTypeException();
            }


            var roomGuid = Guid.NewGuid();
            GameRoom room = new(name, description, map, hasPassword ? password : null, maxPlayerCount, bots, playerGuid, (guid) => PlayerConnections[guid].Login, roomGuid, turnsCount, turnsDeley);

            Rooms[roomGuid] = room;
            connection.CreatedRoomGuid = roomGuid;
            return roomGuid;
        }

        public async Task<bool> DeleteRoom (Guid playerGuid, Guid roomGuid)
        {
            await _connectionsPoolLocker.WaitAsync();
            if (!IsConnectionGuidExist(playerGuid, out var connection))
                throw new PlayerGuidNotFoundException();

            if (!connection!.CanDestroyRoom(roomGuid))
                throw new NotYourRoomException();

            var isPerformed = false;

            if (Rooms.TryRemove(roomGuid, out var gameRoom))
            {
                await gameRoom.Destroy();
                isPerformed = true;
            }

            connection.CreatedRoomGuid = null;

            return isPerformed;
        }

        public Task<(Guid? createdRoomGuid, Guid? connectToRoomGuid, PlayerState playerState)> GetPlayerState (Guid playerGuid) =>
            IsConnectionGuidExist(playerGuid, out var connection)
                ? Task.FromResult((connection!.CreatedRoomGuid, connection!.CurrentGameRoomGuid, connection!.State))
                : Task.FromException<(Guid? createdRoomGuid, Guid? connectToRoomGuid, PlayerState playerState)>(new PlayerGuidNotFoundException());

        public List<LobbyInfo> GetRoomList () => Rooms.Select(r => (r.Value.GetInfo())).ToList();


        public async Task KickPlayer (Guid playerGuid, Guid roomGuid, string targetLogin)
        {
            await _connectionsPoolLocker.WaitAsync();
            if (!IsConnectionGuidExist(playerGuid, out var connection))
                throw new PlayerGuidNotFoundException();

            if (!connection!.CanKick(roomGuid))
                throw new NotYourRoomException();

            LoginToGuid.TryGetValue(targetLogin, out var target);

            await DisconnectFromRoom(target, false);
        }

        public async Task StartGame (Guid playerGuid, Guid roomGuid)
        {
            await _connectionsPoolLocker.WaitAsync();
            if (!IsConnectionGuidExist(playerGuid, out var connection))
                throw new PlayerGuidNotFoundException();

            if (!connection!.CanStartGame(roomGuid))
                throw new NotYourRoomException();

            if (!Rooms.TryGetValue(roomGuid, out var gameRoom))
                throw new RoomNotExistException();

            var task = gameRoom.StartGame(()=>DeleteRoom(playerGuid,roomGuid),guid=>DisconnectFromRoom(guid,false));
            if (task.Exception != null)
                throw task.Exception.InnerException;
        }

        public async Task SetPlayerDirection (Guid playerGuid, Guid roomGuid, Direction direction, uint turn)
        {
            await _connectionsPoolLocker.WaitAsync();
            if (!IsConnectionGuidExist(playerGuid, out var connection))
                throw new PlayerGuidNotFoundException();

            if (!Rooms.TryGetValue(roomGuid, out var gameRoom))
                throw new RoomNotExistException();

            gameRoom.SetPlayerDirection(playerGuid, direction, turn);
        }

        public async Task<AsyncBuffer<GameInfo>> GetVisualData (Guid playerGuid, Guid roomGuid, bool isReconnect)
        {
            await _connectionsPoolLocker.WaitAsync();

            if (!IsConnectionGuidExist(playerGuid, out _))
                throw new PlayerGuidNotFoundException();

            return Rooms.TryGetValue(roomGuid, out var gameRoom)
                ? await gameRoom.GetVisualData(playerGuid, isReconnect)
                : throw new RoomNotExistException();
        }

        public async Task StopSpectating (Guid playerGuid, Guid roomGuid)
        {
            await _connectionsPoolLocker.WaitAsync();

            if (!IsConnectionGuidExist(playerGuid, out _))
                throw new PlayerGuidNotFoundException();

            if (!Rooms.TryGetValue(roomGuid, out var gameRoom))
                throw new RoomNotExistException();

            gameRoom.StopSpectatingGame(playerGuid);
        }
    }
}
