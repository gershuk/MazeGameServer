#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

using Grpc.Core;

using MazeGame.GrpcService;

namespace MazeGame.Server
{
    public class GrpcGameServiceImplementation : GrpcGameService.GrpcGameServiceBase
    {
        private readonly IGameServerModel _gameServerModel;

        public GrpcGameServiceImplementation ([NotNull] IGameServerModel gameServerModel) => _gameServerModel = gameServerModel ?? throw new System.ArgumentNullException(nameof(gameServerModel));

        private Direction GrpcDirectionToServerDirection (DirectionState directionState) => directionState switch
        {
            DirectionState.None => Direction.None,
            DirectionState.Up => Direction.Up,
            DirectionState.Down => Direction.Down,
            DirectionState.Left => Direction.Left,
            DirectionState.Right => Direction.Right,
        };

        private GrpcService.BlockType BlockTypeToGrpcServiceBlockType (MazeGame.Server.BlockType blockType) => blockType switch
        {
            BlockType.Undefined => GrpcService.BlockType.BlockUndefined,
            BlockType.Wall => GrpcService.BlockType.BlockWall,
            BlockType.Empty => GrpcService.BlockType.BlockEmpty,
            BlockType.Exit => GrpcService.BlockType.BlockExit,
            _ => throw new NotImplementedException(),
        };


        private ExitMessage CreateExitAnswer (Task task) => new ExitMessage
        {
            RequestingGuidStatus = task.Exception switch
            {
                null => RequestingGuidStatus.Exists,
                _ => task.Exception.InnerException switch
                {
                    PlayerGuidNotFoundException => RequestingGuidStatus.NotExist,
                    _ => throw task.Exception.InnerException,
                }
            }
        };

        private RegistrationAnswer CreateRegistrationAnswer (Task task) => new RegistrationAnswer
        {
            Status = task.Exception switch
            {
                null => RegistrationStatus.RegistrationSuccessfull,
                _ => task.Exception.InnerException switch
                {
                    BadRegistrationDataException => RegistrationStatus.BadInput,
                    LoginAlreadyRegistredException => RegistrationStatus.LoginAlreadyExist,
                    _ => throw task.Exception.InnerException,
                }
            }
        };

        private AuthorizationAnswer CreateAuthorizationAnswer (Task<System.Guid> task) => new AuthorizationAnswer
        {
            PlayerGuid = new GrpcService.Guid() { Guid_ = task.Exception == null ? task.Result.ToString("D") : string.Empty },
            Status = task.Exception switch
            {
                null => AuthorizationStatus.AuthorizationSuccessfull,
                _ => task.Exception.InnerException switch
                {
                    LoginNotExistException or WrongPasswordException => AuthorizationStatus.WrongLoginOrPassword,
                    LastConnectionStillOpenException => AuthorizationStatus.AnotherConnectionActive,
                    _ => throw task.Exception.InnerException,
                }
            }
        };

        private RoomTableModificationAnswer CreateRoomTableModificationAnswer (Task<System.Guid> task) => new RoomTableModificationAnswer
        {
            RequestingGuidStatus = task.Exception?.InnerException is PlayerGuidNotFoundException ? RequestingGuidStatus.NotExist : RequestingGuidStatus.Exists,
            Status = task.Exception switch
            {
                null => RoomTableModificationStatus.RoomTableModificationSuccessfull,
                _ => task.Exception.InnerException switch
                {
                    RoomAlreadyCreatedException => RoomTableModificationStatus.YouAlreadyGotRoom,
                    UnplayableConfigException => RoomTableModificationStatus.UnplayableConfig,
                    ThereAreMoreBotsThanEmptySlots => RoomTableModificationStatus.LimitExceededForBotsForThisMap,
                    MaxPlayerMoreThenSpawnerException => RoomTableModificationStatus.MaxPlayerMoreThenSpawnerException,
                    MapNotFoundException => RoomTableModificationStatus.WrongMapGuid,
                    WrongBotTypeException => RoomTableModificationStatus.WrongBotsType,
                    _ => throw task.Exception.InnerException,
                }
            },
            RoomGuid = new()
            {
                Guid_ = task.Exception == null ? task.Result.ToString("D") : System.Guid.Empty.ToString("D")
            }
        };

        private PlayerStateAnswer CreatePlayerStateAnswer (Task<(System.Guid? createdRoomGuid, System.Guid? connectToRoomGuid, PlayerState playerState)> task) => new PlayerStateAnswer
        {
            RequestingGuidStatus = task.Exception?.InnerException is PlayerGuidNotFoundException ? RequestingGuidStatus.NotExist : RequestingGuidStatus.Exists,
            CreatedRoomGuid = new()
            {
                Guid_ = task.Exception == null && task.Result.createdRoomGuid !=null ? task.Result.createdRoomGuid.Value.ToString("D") : System.Guid.Empty.ToString("D")
            },

            ConnectToRoomGuid = new()
            {
                Guid_ = task.Exception == null && task.Result.connectToRoomGuid != null ? task.Result.connectToRoomGuid.Value.ToString("D") : System.Guid.Empty.ToString("D")
            },
            PlayerState = task.Exception == null ?
            task.Result.playerState switch
            {
                PlayerState.Authorized => GrpcService.PlayerState.Authorized,
                PlayerState.InLobby => GrpcService.PlayerState.InLobby,
                PlayerState.InGame => GrpcService.PlayerState.InGame,
                _ => GrpcService.PlayerState.Undefined,
            }
            : GrpcService.PlayerState.Undefined,
        };

        private DeleteRoomAnswer CreateDeleteRoomAnswer (Task<bool> task) => new DeleteRoomAnswer
        {
            RequestingGuidStatus = task.Exception?.InnerException is PlayerGuidNotFoundException ? RequestingGuidStatus.NotExist : RequestingGuidStatus.Exists,
            Status = task.Exception?.InnerException is null ? DeleteRoomStatus.DeleteRoomSuccessfull :
            task.Exception?.InnerException switch
            {
                null => task.Result ? DeleteRoomStatus.DeleteRoomSuccessfull : DeleteRoomStatus.CantDeleteRoomNotFound,
                NotYourRoomException => DeleteRoomStatus.CantDeleteNotYourRoom,
                _ => throw task.Exception.InnerException,
            }
        };

        private DisconnectFromRoomAnswer CreateDisconnectFromRoomAnswer (Task task) => new DisconnectFromRoomAnswer
        {
            RequestingGuidStatus = task.Exception?.InnerException is PlayerGuidNotFoundException ? RequestingGuidStatus.NotExist : RequestingGuidStatus.Exists,
            Status = task.Exception == null ? DisconnectFromRoomState.DisconnectSuccessfull : DisconnectFromRoomState.CantDisconnectRoomNotFound,
        };

        private PlayerKickAnswer CreatePlayerKickAnswer (Task task) => new PlayerKickAnswer
        {
            RequestingGuidStatus = task.Exception?.InnerException is PlayerGuidNotFoundException ? RequestingGuidStatus.NotExist : RequestingGuidStatus.Exists,
            Status = task.Exception switch
            {
                null => PlayerKickStatus.PlayerKickSuccessfull,
                _ => task.Exception.InnerException switch
                {
                    NotYourRoomException => PlayerKickStatus.YouNotOwnerOfThisRoom,
                    TargetGuidNotFoundException or PlayerNotInRoomException => PlayerKickStatus.KickPlayerNotFound,
                    RoomNotExistException => PlayerKickStatus.RoomNotFound,
                    _ => throw task.Exception.InnerException,
                }
            },
        };

        private StartGameAnswer CreateStartGameAnswer (Task task) => new StartGameAnswer
        {
            RequestingGuidStatus = task.Exception?.InnerException is PlayerGuidNotFoundException ? RequestingGuidStatus.NotExist : RequestingGuidStatus.Exists,
            GameStatus = task.Exception switch
            {
                null => StartGameStatus.StartSuccessfull,
                _ => task.Exception.InnerException switch
                {
                    NotYourRoomException => StartGameStatus.NotYourRoom,
                    RoomNotExistException => StartGameStatus.RoomNotFoundToStartGame,
                    GameAlreadyStartedException => StartGameStatus.GameAlreadyStarted,
                    ThereIsNoPlayersOrBotsInRoomException => StartGameStatus.NoPlayers,
                    _ => throw task.Exception.InnerException,
                }
            },
        };

        private SetDirectionAnswer CreateSetDirectionAnswer (Task task) => new SetDirectionAnswer
        {
            RequestingGuidStatus = task.Exception?.InnerException is PlayerGuidNotFoundException ? RequestingGuidStatus.NotExist : RequestingGuidStatus.Exists,
            SetDirectionState = task.Exception switch
            {
                null => SetDirectionState.SuccessfullSetDir,
                _ => task.Exception.InnerException switch
                {
                    RoomNotExistException => SetDirectionState.RoomNotFoundToSetDir,
                    GameNotStartedException => SetDirectionState.GameNotStrartedToSetDir,
                    WrongTurnException => SetDirectionState.WrongTurnSet,
                    PlayerNotFoundInGameExeception => SetDirectionState.PlayerNotFoundToSetDir
                }
            }
        };

        private StopSpectateGameAnswer CreateStopSpectateGameAnswer (Task task) => new StopSpectateGameAnswer
        {
            RequestingGuidStatus = task.Exception?.InnerException is PlayerGuidNotFoundException ? RequestingGuidStatus.NotExist : RequestingGuidStatus.Exists,
            StopSpectateGameState = task.Exception switch
            {
                null => StopSpectateGameState.SuccessfullStopSpectateGame,
                _ => task.Exception.InnerException switch
                {
                    RoomNotExistException => StopSpectateGameState.CantStopSpectateRoomNotExist,
                    GameNotStartedException => StopSpectateGameState.CantStopSpectateGameNotStarted,
                    SpectatingChannelNotFoundException => StopSpectateGameState.CantStopSpectateSpectatingChannelNotFound,
                }
            }
        };

        public override async Task<AuthorizationAnswer> LogIn (AuthorizationData request, ServerCallContext context) =>
            await _gameServerModel.AuthorizePlayer(request.UserData.Login, request.UserData.PasswordHash, request.ClearActiveConnection, context.Peer).ContinueWith(CreateAuthorizationAnswer);

        public override async Task<RegistrationAnswer> RegisterNewUser (UserData request, ServerCallContext context) =>
            await _gameServerModel.RegisterNewPlayer(request.Login, request.PasswordHash).ContinueWith(CreateRegistrationAnswer);

        public override async Task<ExitMessage> ClosePlayerConnection (GrpcService.Guid request, ServerCallContext context) =>
            await _gameServerModel.ClosePlayerConnection(new System.Guid(request.Guid_)).ContinueWith(CreateExitAnswer);

        public override async Task<RoomTableModificationAnswer> CreateRoom (OwnerRoomConfiguration request, ServerCallContext context) =>
            await _gameServerModel.CreateRoom(new System.Guid(request.OwnerGuid.Guid_),
                                              request.Properties.Name,
                                              request.Properties.Description,
                                              new System.Guid(request.Properties.MapGuid.Guid_),
                                              request.Properties.MaxPlayerCount,
                                              request.Properties.Password,
                                              request.Properties.HasPassword,
                                              request.Properties.TurnsCount,
                                              request.Properties.TurnDeley,
                                              request.Properties.BotTypes.ToArray()).ContinueWith(CreateRoomTableModificationAnswer);

        public override async Task<DeleteRoomAnswer> DeleteRoom (PlayerAndRoomGuids request, ServerCallContext context) =>
            await _gameServerModel.DeleteRoom(new System.Guid(request.PlayerGuid.Guid_), new System.Guid(request.RoomGuid.Guid_)).ContinueWith(CreateDeleteRoomAnswer);

        public override async Task<PlayerStateAnswer> GetPlayerState (GrpcService.Guid request, ServerCallContext context) =>
            await _gameServerModel.GetPlayerState(new System.Guid(request.Guid_)).ContinueWith(CreatePlayerStateAnswer);

        public override async Task ConnectToRoom (PlayerAndRoomGuids request, IServerStreamWriter<RoomPropertiesAnswer> responseStream, ServerCallContext context)
        {
            try
            {
                var buffer = await _gameServerModel.ConnectToRoom(new System.Guid(request.PlayerGuid.Guid_), new System.Guid(request.RoomGuid.Guid_), request.Password);
                try
                {
                    while (true)
                    {
                        var info = await buffer.AsyncRead();

                        var ans = new RoomPropertiesAnswer()
                        {
                            RequestingGuidStatus = RequestingGuidStatus.Exists,
                            Propertiesstatus = RoomPropertiesAnswerStatus.Successfull,
                            Properties = new RoomProperties()
                            {
                                Guid = new() { Guid_ = info.LobbyGuid.ToString("D") },
                                Name = info.RoomName,
                                Description = info.Description,
                                Status = info.RoomStatus switch
                                {
                                    RoomStatus.Lobby => GrpcService.RoomStatus.Lobby,
                                    RoomStatus.GameStrated => GrpcService.RoomStatus.GameStrated,
                                    RoomStatus.GameEnded => GrpcService.RoomStatus.GameEnded,
                                    RoomStatus.Destroyed => GrpcService.RoomStatus.Deleted,
                                    _ => throw new NotImplementedException(),
                                },
                                PlayersCount = (uint) info.PlayerNames.Count,
                                HasPassword = info.HasPassword,
                                MaxPlayerCount = info.MaxPlayerCount,
                                Owner = info.OwnerName,
                                MapGuid = new() { Guid_ = info.MapGuid.ToString("D") },
                                TurnsCount = info.TurnsCount,
                                TurnDeley = info.TurnDeley,
                            }
                        };

                        foreach (var playerName in info.PlayerNames)
                            ans.Properties.PlayerNames.Add(playerName);

                        foreach (var botType in info.BotTypes)
                            ans.Properties.BotTypes.Add(botType);

                        await responseStream.WriteAsync(ans);
                    }
                }
                catch (OperationCanceledException)
                {
                    await responseStream.WriteAsync(new RoomPropertiesAnswer()
                    {
                        RequestingGuidStatus = RequestingGuidStatus.Exists,
                        Propertiesstatus = RoomPropertiesAnswerStatus.OperationCanceled
                    });
                }
            }
            catch (PlayerGuidNotFoundException)
            {
                await responseStream.WriteAsync(new RoomPropertiesAnswer() { RequestingGuidStatus = RequestingGuidStatus.NotExist });
            }
            catch (WrongPasswordException)
            {
                await responseStream.WriteAsync(new RoomPropertiesAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    Propertiesstatus = RoomPropertiesAnswerStatus.WrongPassword
                });
            }
            catch (RoomNotExistException)
            {

                await responseStream.WriteAsync(new RoomPropertiesAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    Propertiesstatus = RoomPropertiesAnswerStatus.RoomNotExist
                });
            }
            catch (ChangeRoomException)
            {
                await responseStream.WriteAsync(new RoomPropertiesAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    Propertiesstatus = RoomPropertiesAnswerStatus.ChangeRoomException
                });
            }
            catch (PlayerAlreadyConnectedToThisRoomException)
            {
                await responseStream.WriteAsync(new RoomPropertiesAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    Propertiesstatus = RoomPropertiesAnswerStatus.PlayerAlreadyConnectedToThisRoom
                });
            }
            catch (CantConnectToStartedGameException)
            {
                await responseStream.WriteAsync(new RoomPropertiesAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    Propertiesstatus = RoomPropertiesAnswerStatus.CantConnectToStarted
                });
            }
            catch (PlayerLimitException)
            {
                await responseStream.WriteAsync(new RoomPropertiesAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    Propertiesstatus = RoomPropertiesAnswerStatus.RoomFull,
                });
            }
            catch (InvalidOperationException) { /*ignore*/}
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        public override async Task<DisconnectFromRoomAnswer> DisconnectFromRoom (GrpcService.Guid request, ServerCallContext context) =>
            await Task.Run(() => _gameServerModel.DisconnectFromRoom(new System.Guid(request.Guid_), true)).ContinueWith(CreateDisconnectFromRoomAnswer);

        public override Task<GetMapsAnswer> GetMaps (Empty request, ServerCallContext context)
        {
            GetMapsAnswer ans = new();
            foreach (var (name, guid, playerCount, size) in _gameServerModel.MapStorage.GetShotMapsInfo())
                ans.MapInfos.Add(new MapInfo { Guid = new() { Guid_ = guid.ToString("D") }, Name = name, Players = playerCount, W = (uint) size.X, H = (uint) size.Y });
            return Task.FromResult(ans);
        }

        public override Task<GetBotsAnswer> GetBots (Empty request, ServerCallContext context)
        {
            GetBotsAnswer ans = new();
            foreach (var type in _gameServerModel.BotFactory.GetBotTypeNames())
                ans.Types_.Add(type);
            return Task.FromResult(ans);
        }

        public override Task<RoomListAnswer> GetRoomList (Empty request, ServerCallContext context)
        {
            RoomListAnswer ans = new();
            foreach (var roomInfo in _gameServerModel.GetRoomList())
            {
                RoomProperties roomProperties = new()
                {
                    Guid = new() { Guid_ = roomInfo.LobbyGuid.ToString("D") },
                    Name = roomInfo.RoomName,
                    Description = roomInfo.Description,
                    Status = roomInfo.RoomStatus switch
                    {
                        RoomStatus.Lobby => GrpcService.RoomStatus.Lobby,
                        RoomStatus.GameStrated => GrpcService.RoomStatus.GameStrated,
                        RoomStatus.GameEnded => GrpcService.RoomStatus.GameEnded,
                        RoomStatus.Destroyed => GrpcService.RoomStatus.Deleted,
                        _ => throw new NotImplementedException(),
                    },
                    PlayersCount = (uint) roomInfo.PlayerNames.Count,
                    HasPassword = roomInfo.HasPassword,
                    MaxPlayerCount = roomInfo.MaxPlayerCount,
                    Owner = roomInfo.OwnerName,
                    MapGuid = new() { Guid_ = roomInfo.MapGuid.ToString("D") },
                    TurnsCount = roomInfo.TurnsCount,
                    TurnDeley = roomInfo.TurnDeley,
                };

                foreach (var playerName in roomInfo.PlayerNames)
                    roomProperties.PlayerNames.Add(playerName);

                foreach (var botType in roomInfo.BotTypes)
                    roomProperties.BotTypes.Add(botType);

                ans.RoomProperties.Add(roomProperties);
            }

            return Task.FromResult(ans);
        }

        public override async Task<PlayerKickAnswer> KickPlayer (KickMessage request, ServerCallContext context) =>
            await _gameServerModel.KickPlayer(new System.Guid(request.OwnerData.PlayerGuid.Guid_),
                                              new System.Guid(request.OwnerData.RoomGuid.Guid_),
                                              request.TargetLogin).ContinueWith(CreatePlayerKickAnswer);

        public override async Task<StartGameAnswer> StartGame (PlayerAndRoomGuids request, ServerCallContext context) =>
            await _gameServerModel.StartGame(new System.Guid(request.PlayerGuid.Guid_), new System.Guid(request.RoomGuid.Guid_)).ContinueWith(CreateStartGameAnswer);

        public override async Task<SetDirectionAnswer> SetDirection (PlayerDirection request, ServerCallContext context) =>
            await _gameServerModel.SetPlayerDirection(new System.Guid(request.PlayerAndRoomGuids.PlayerGuid.Guid_),
                                                      new System.Guid(request.PlayerAndRoomGuids.RoomGuid.Guid_),
                                                      GrpcDirectionToServerDirection(request.DirectionState),
                                                      request.Turn).ContinueWith(CreateSetDirectionAnswer);
        public override async Task SpectateGame (PlayerAndRoomGuids request, IServerStreamWriter<SpectateGameAnswer> responseStream, ServerCallContext context)
        {
            try
            {
                var buffer = await _gameServerModel.GetVisualData(new System.Guid(request.PlayerGuid.Guid_), new System.Guid(request.RoomGuid.Guid_), true);
                try
                {
                    while (true)
                    {
                        var info = await buffer.AsyncRead();
                        SpectateData spectateData = new()
                        {
                            Pos = new() { X = info.Position.X, Y = info.Position.Y },
                            Status = info.AvatarGameState switch
                            {
                                AvatarGameState.Running => AvatarState.AvatarRunnig,
                                AvatarGameState.Win => AvatarState.AvatarWin,
                                AvatarGameState.Lose => AvatarState.AvatarLose,
                                _ => throw new NotImplementedException(),
                            },
                            Turn = info.Turn,
                            MapSize = new() { X = info.MapSize.X, Y = info.MapSize.Y }
                        };

                        spectateData.PlayerInfos.AddRange(info.Players.Select(p => new PlayerInfo() { Name = p.name, Pos = new() { X = p.position.X, Y = p.position.Y } }));
                        spectateData.BlockInfos.AddRange(info.Blocks.Select(b => new BlockInfo() { BlockType = BlockTypeToGrpcServiceBlockType(b.type), Pos = new() { X = b.position.X, Y = b.position.Y } }));

                        var ans = new SpectateGameAnswer()
                        {
                            RequestingGuidStatus = RequestingGuidStatus.Exists,
                            SpectateGameStatus = SpectateGameStatus.OpenSpectateChannel,
                            SpectateData = spectateData,
                        };


                        await responseStream.WriteAsync(ans);
                    }
                }
                catch (OperationCanceledException)
                {
                    await responseStream.WriteAsync(new SpectateGameAnswer()
                    {
                        RequestingGuidStatus = RequestingGuidStatus.Exists,
                        SpectateGameStatus = SpectateGameStatus.ClosedSpectateChannel,
                    });
                }
            }
            catch (PlayerGuidNotFoundException)
            {
                await responseStream.WriteAsync(new SpectateGameAnswer() { RequestingGuidStatus = RequestingGuidStatus.NotExist });
            }
            catch (GameNotStartedException)
            {
                await responseStream.WriteAsync(new SpectateGameAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    SpectateGameStatus = SpectateGameStatus.GameNotStartedNothingSpectate
                });
            }
            catch (RoomNotExistException)
            {

                await responseStream.WriteAsync(new SpectateGameAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    SpectateGameStatus = SpectateGameStatus.WrongRoomToSpectate,
                });
            }
            catch (PlayerNotFoundInGameExeception)
            {
                await responseStream.WriteAsync(new SpectateGameAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    SpectateGameStatus = SpectateGameStatus.PlayerNotFoundToSpectete,
                });
            }
            catch (PlayerAlreadySpectedThisRoomException)
            {
                await responseStream.WriteAsync(new SpectateGameAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    SpectateGameStatus = SpectateGameStatus.PlayerAlreadySpectedThisRoom,
                });
            }
            catch (InvalidOperationException) { /*ignore*/}
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        public override async Task<StopSpectateGameAnswer> StopSpectateGame (PlayerAndRoomGuids request, ServerCallContext context) =>
            await _gameServerModel.StopSpectating(new System.Guid(request.PlayerGuid.Guid_), new System.Guid(request.RoomGuid.Guid_)).ContinueWith(CreateStopSpectateGameAnswer);
    }
}
