using System;

namespace MazeGame.Server
{
    public class LoginNotExistException : Exception { }

    public class LastConnectionStillOpenException : Exception { }

    public class PlayerGuidNotFoundException : Exception { }

    public class WrongGuidForLogin : Exception { }

    public class ActivePlayerNotFoundException : Exception { }

    public class BadRegistrationDataException : Exception { }

    public class LoginAlreadyRegistredException : Exception { }

    public class DbException : Exception { }

    public class ConnectionPoolUndefinedException : Exception { }

    public class MapNotFoundException : Exception { }

    public class MaxPlayerMoreThenSpawnerException : Exception { }

    public class UnplayableConfigException : Exception { }

    public class ThereAreMoreBotsThanEmptySlots : Exception { }

    public class NotYourRoomException : Exception { }

    public class RoomNotExistException : Exception { }

    public class PlayerAlreadyConnectedToThisRoomException : Exception { }
    public class PlayerAlreadySpectedThisRoomException : Exception { }
    public class PlayerNotFoundInRoomException : Exception { }
    public class SpectatingChannelNotFoundException : Exception { }
    public class CantConnectToStartedGameException : Exception { }
    public class PlayerLimitException : Exception { }
    public class WrongPasswordException : Exception { }
    public class ChangeRoomException : Exception { }

    public class PlayerNotInRoomException : Exception { }

    public class RoomAlreadyCreatedException : Exception { }
    public class PlayerNotFoundInGameExeception : Exception { };
    public class GameNotStartedException : Exception { };
    public class WrongTurnException : Exception { };
    public class TargetGuidNotFoundException : Exception { };
    public class GameAlreadyStartedException : Exception { };
    public class ThereIsNoPlayersOrBotsInRoomException : Exception { };
    public class WrongBotTypeException : Exception { };
}
