#nullable enable

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace MazeGame.Server
{
    public class LoginNotExistException : Exception { }

    public class WrongPasswordException : Exception { }

    public class LastConnectionStillOpenException : Exception { }

    public class PlayerGuidNotFoundException : Exception { }

    public class WrongGuidForLogin : Exception { }

    public class ActivePlayerNotFoundException : Exception { }

    public class BadRegistrationDataException : Exception { }

    public class LoginAlreadyRegistredException : Exception { }

    public class DbException : Exception { }

    public interface IGameServerModel
    {
        public string DbConnectionString { get; }
        public Dictionary<Guid, GameRoom> GameRooms { get; }
        public Dictionary<string, Guid> LoginToGuid { get; }
        public Dictionary<Guid, PlayerConnection> PlayerConnections { get; }

        public Task<Guid> AuthorizePlayer (string login, string password, bool killLastConnection, string socketInfo);
        public Task ClosePlayerConnection (Guid guid);
        public Task RegisterNewPlayer (string login, string password);
    }

    public class GameServer : IGameServerModel
    {
        private readonly ManualResetEvent _connectionsPoolLocker;

        public string DbConnectionString { get; private set; }

        public Dictionary<Guid, GameRoom> GameRooms { get; private set; }

        public Dictionary<Guid, PlayerConnection> PlayerConnections { get; private set; }

        public Dictionary<string, Guid> LoginToGuid { get; private set; }

        public GameServer ()
        {
            _connectionsPoolLocker = new(true);
            DbConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=usersdb;Integrated Security=True";
            GameRooms = new();
            PlayerConnections = new();
            LoginToGuid = new();
        }

        public GameServer (int roomsPoolCapacity, int connectionsPoolCapacity, string dbConnectionString)
        {
            _connectionsPoolLocker = new(true);
            DbConnectionString = dbConnectionString;
            GameRooms = new(roomsPoolCapacity);
            PlayerConnections = new(connectionsPoolCapacity);
            LoginToGuid = new(connectionsPoolCapacity);
        }

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
            var real_password = reader.GetString(1);

            if (real_password.TrimEnd() != password)
                throw new WrongPasswordException();

            await Task.Run(() => _connectionsPoolLocker.WaitOne());

            var isExist = LoginToGuid.TryGetValue(login, out var guid);
            if (!(isExist && PlayerConnections[LoginToGuid[login]].SocketInfo == socketInfo))
            {
                if (!killLastConnection && isExist)
                    throw new LastConnectionStillOpenException();
                if (isExist)
                    PlayerConnections.Remove(guid);

                guid = Guid.NewGuid();
                LoginToGuid[login] = guid;
                PlayerConnections[guid] = new PlayerConnection(login, socketInfo);
            }

            return guid;
        }

        public async Task RegisterNewPlayer (string login, string password)
        {
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
            await Task.Run(() => _connectionsPoolLocker.WaitOne());

            if (!PlayerConnections.TryGetValue(guid, out var connection))
                throw new PlayerGuidNotFoundException();

            LoginToGuid.Remove(connection.Login);
            PlayerConnections.Remove(guid);
        }
    }
}
