#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace MazeGame.Server
{
    public enum PlayerState
    {
        Authorized,
        InLobby,
        InGame,
    }

    public class PlayerConnection
    {
        private Guid? _createdRoomGuid;
        private readonly HashSet<Guid> _spectatingGuids;

        public string Login { get; init; }
        public string SocketInfo { get; init; }
        public PlayerState State { get; private set; }
        public Guid? CurrentGameRoomGuid { get; private set; }
        public Guid? CreatedRoomGuid
        {
            get => _createdRoomGuid;
            set => _createdRoomGuid = (_createdRoomGuid == null)
                    ? value
                    : (value == null) ?
                        (Guid?) null :
                        throw new RoomAlreadyCreatedException();
        }

        public PlayerConnection (string login, string socketInfo)
        {
            Login = login;
            SocketInfo = socketInfo;
            State = PlayerState.Authorized;
            CurrentGameRoomGuid = null;
            CreatedRoomGuid = null;
            _spectatingGuids = new();
        }

        public void ConnectToRoom (Guid roomGuid)
        {
            switch (State)
            {
                case PlayerState.Authorized:
                    State = PlayerState.InLobby;
                    CurrentGameRoomGuid = roomGuid;
                    break;
                case PlayerState.InLobby or PlayerState.InGame:
                    throw new ChangeRoomException();
            }
        }

        public void AddSpectatingGuid (Guid guid) => _spectatingGuids.Add(guid);

        public void DeleteSpectatingGuid (Guid guid) => _spectatingGuids.Remove(guid);

        public IImmutableList<Guid> GetSpectatingGuids () => _spectatingGuids.ToImmutableList();

        public void DisconnectFromRoom ()
        {
            switch (State)
            {
                case PlayerState.Authorized:
                    throw new PlayerNotInRoomException();
                case PlayerState.InLobby or PlayerState.InGame:
                    CurrentGameRoomGuid = null;
                    State = PlayerState.Authorized;
                    break;
            }
        }

        public bool CanCreateRoom () => CreatedRoomGuid == null;

        public bool CanDestroyRoom (Guid roomGuid) => CreatedRoomGuid.Equals(roomGuid);

        public bool CanKick (Guid roomGuid) => CreatedRoomGuid.Equals(roomGuid);

        public bool CanStartGame (Guid roomGuid) => CreatedRoomGuid.Equals(roomGuid);
    }
}