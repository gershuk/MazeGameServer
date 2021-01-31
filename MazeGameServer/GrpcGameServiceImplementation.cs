#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

using Grpc.Core;
using Grpc.Core.Utils;

using MazeGame.GrpcService;

namespace MazeGame.Server
{
    public class GrpcGameServiceImplementation : GrpcGameService.GrpcGameServiceBase
    {
        private readonly IGameServerModel _gameServerModel;

        public GrpcGameServiceImplementation ([NotNull] IGameServerModel gameServerModel) => _gameServerModel = gameServerModel ?? throw new System.ArgumentNullException(nameof(gameServerModel));

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
                null => RegistrationStatus.RegistrationSuccessful,
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
                null => AuthorizationStatus.AuthorizationSuccessful,
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
                null => RoomTableModificationStatus.RoomTableModificationSuccessful,
                _ => task.Exception.InnerException switch
                {
                    RoomAlreadyCreatedException => RoomTableModificationStatus.YouAlreadyGotRoom,
                    UnplayableConfigException => RoomTableModificationStatus.UnplayableConfig,
                    ThereAreMoreBotsThanEmptySlots => RoomTableModificationStatus.LimitExceededForBotsForThisMap,
                    MaxPlayerMoreThenSpawnerException => RoomTableModificationStatus.MaxPlayerMoreThenSpawnerException,
                    MapNotFoundException => RoomTableModificationStatus.WrongMapGuid,
                    _ => throw task.Exception.InnerException,
                }
            },
            RoomGuid = new()
            {
                Guid_ = task.Exception == null ? task.Result.ToString("D") : System.Guid.Empty.ToString("D")
            }
        };

        private PlayerStateAnswer CreatePlayerStateAnswer (Task<(System.Guid? roomGuid, PlayerState playerState)> task) => new PlayerStateAnswer
        {
            RequestingGuidStatus = task.Exception?.InnerException is PlayerGuidNotFoundException ? RequestingGuidStatus.NotExist : RequestingGuidStatus.Exists,
            RoomGuid = new()
            {
                Guid_ = task.Exception == null ? task.Result.roomGuid?.ToString("D") : System.Guid.Empty.ToString("D")
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
            Status = task.Exception?.InnerException is null ? DeleteRoomStatus.DeleteRoomSuccessful :
            task.Exception?.InnerException switch
            {
                null => task.Result ? DeleteRoomStatus.DeleteRoomSuccessful : DeleteRoomStatus.CantDeleteRoomNotFound,
                NotYourRoomException => DeleteRoomStatus.CantDeleteNotYourRoom,
                _ => throw task.Exception.InnerException,
            }
        };

        private DisconnectFromRoomAnswer CreateDisconnectFromRoomAnswer (Task task) => new DisconnectFromRoomAnswer
        {
            RequestingGuidStatus = task.Exception?.InnerException is PlayerGuidNotFoundException ? RequestingGuidStatus.NotExist : RequestingGuidStatus.Exists,
            Status = task.Exception == null ? DisconnectFromRoomState.DisconnectSuccessful : DisconnectFromRoomState.CantDisconnectRoomNotFound,
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
                            Propertiesstatus = RoomPropertiesAnswerStatus.Successful,
                            Properties = new RoomProperties()
                            {
                                Guid = new() { Guid_ = info.Guid.ToString("D") },
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
                                MapGuid = new() { Guid_ = info.Guid.ToString("D") },
                            }
                        };

                        foreach (var playerName in info.PlayerNames)
                            ans.Properties.PlayerNames.Add(playerName);

                        foreach (var botType in info.BotTypes)
                            ans.Properties.PlayerNames.Add(botType);

                        await responseStream.WriteAsync(ans);
                    }
                }
                catch (OperationCanceledException)
                {
                    await responseStream.WriteAllAsync(new List<RoomPropertiesAnswer>()
                        {
                            new RoomPropertiesAnswer()
                            {
                                RequestingGuidStatus = RequestingGuidStatus.NotExist,
                                Propertiesstatus = RoomPropertiesAnswerStatus.OperationCanceled
                            }
                        });
                }
            }
            catch (PlayerGuidNotFoundException)
            {
                await responseStream.WriteAllAsync(new List<RoomPropertiesAnswer>() { new RoomPropertiesAnswer() { RequestingGuidStatus = RequestingGuidStatus.NotExist } });
            }
            catch (WrongPasswordException)
            {
                new RoomPropertiesAnswer()
                {
                    RequestingGuidStatus = RequestingGuidStatus.Exists,
                    Propertiesstatus = RoomPropertiesAnswerStatus.WrongPassword
                };
            }
            catch (RoomNotExistException)
            {

                await responseStream.WriteAllAsync(new List<RoomPropertiesAnswer>()
                {
                    new RoomPropertiesAnswer()
                    {
                        RequestingGuidStatus = RequestingGuidStatus.Exists,
                        Propertiesstatus = RoomPropertiesAnswerStatus.RoomNotExist
                    }
                });
            }
            catch (ChangeRoomException)
            {
                await responseStream.WriteAllAsync(new List<RoomPropertiesAnswer>()
                {
                    new RoomPropertiesAnswer()
                    {
                        RequestingGuidStatus = RequestingGuidStatus.Exists,
                        Propertiesstatus = RoomPropertiesAnswerStatus.ChangeRoom
                    }
                });
            }
            catch (PlayerAlreadyConnectedToThisRoomException)
            {
                await responseStream.WriteAllAsync(new List<RoomPropertiesAnswer>()
                {
                    new RoomPropertiesAnswer()
                    {
                        RequestingGuidStatus = RequestingGuidStatus.Exists,
                        Propertiesstatus = RoomPropertiesAnswerStatus.PlayerAlreadyConnectedToThisRoom
                    }
                });
            }
            catch (CantConnectToStartedGameException)
            {
                await responseStream.WriteAllAsync(new List<RoomPropertiesAnswer>()
                {
                    new RoomPropertiesAnswer()
                    {
                        RequestingGuidStatus = RequestingGuidStatus.Exists,
                        Propertiesstatus = RoomPropertiesAnswerStatus.CantConnectToStarted
                    }
                });
            }
            catch (InvalidOperationException) { /*ignore*/}
            catch (Exception e) { Console.WriteLine(e.Message); }
        }

        public override async Task<DisconnectFromRoomAnswer> DisconnectFromRoom (GrpcService.Guid request, ServerCallContext context) =>
            await Task.Run(() => _gameServerModel.DisconnectFromRoom(new System.Guid(request.Guid_))).ContinueWith(CreateDisconnectFromRoomAnswer);

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
    }
}
