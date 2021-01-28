#nullable enable

using System;

namespace MazeGame.Server
{
    public class ChangeRoomException : Exception
    {
        public ChangeRoomException () : base("Player is already in another room.") { }
    }

    public class PlayerNotInRoom : Exception
    { }

    public enum PlayerState
    {
        Authorized,
        InGame
    }

    public class PlayerConnection
    {
        //ToDo : change to ip and port type
        public string Login { get; init; }
        public string SocketInfo { get; init; }
        public PlayerState State { get; private set; }
        public Guid? CurrentGameRoomGuid { get; private set; }

        public PlayerConnection (string login, string socketInfo)
        {
            Login = login;
            SocketInfo = socketInfo;
            State = PlayerState.Authorized;
            CurrentGameRoomGuid = null;
        }

        public void ConnectToRoom (Guid roomGuid)
        {
            switch (State)
            {
                case PlayerState.Authorized:
                    State = PlayerState.InGame;
                    CurrentGameRoomGuid = roomGuid;
                    break;
                case PlayerState.InGame or PlayerState.InGame:
                    throw new ChangeRoomException();
            }
        }

        public void Disconnect ()
        {
            switch (State)
            {
                case PlayerState.Authorized:
                    throw new PlayerNotInRoom();
                case PlayerState.InGame:
                    State = PlayerState.Authorized;
                    CurrentGameRoomGuid = null;
                    break;
            }
        }
    }
}
