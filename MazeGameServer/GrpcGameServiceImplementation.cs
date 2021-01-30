#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using Grpc.Core;

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

        public override Task<RoomTableModificationAnswer> ChangeRoomProperties (OwnerRoomConfiguration request, ServerCallContext context) => base.ChangeRoomProperties(request, context);
        public override Task ConnectToRoom (PlayerAndRoomGuids request, IServerStreamWriter<RoomPropertiesAnswer> responseStream, ServerCallContext context) => base.ConnectToRoom(request, responseStream, context);
        public override Task<RoomTableModificationAnswer> CreateRoom (OwnerRoomConfiguration request, ServerCallContext context) => base.CreateRoom(request, context);
        public override Task<DeleteRoomAnswer> DeleteRoom (PlayerAndRoomGuids request, ServerCallContext context) => base.DeleteRoom(request, context);
        public override Task<DisconnectFromRoomAnswer> DisconnectFromRoom (PlayerAndRoomGuids request, ServerCallContext context) => base.DisconnectFromRoom(request, context);
        public override Task<RoomListAnswer> GetRoomList (GrpcService.Guid request, ServerCallContext context) => base.GetRoomList(request, context);
        public override Task<PlayerKickAnswer> KickPlayer (PlayerAndRoomGuids request, ServerCallContext context) => base.KickPlayer(request, context);

        public override async Task<AuthorizationAnswer> LogIn (AuthorizationData request, ServerCallContext context) =>
            await _gameServerModel.AuthorizePlayer(request.UserData.Login, request.UserData.PasswordHash, request.ClearActiveConnection, context.Peer).ContinueWith(CreateAuthorizationAnswer);

        public override async Task<RegistrationAnswer> RegisterNewUser (UserData request, ServerCallContext context) =>
            await _gameServerModel.RegisterNewPlayer(request.Login, request.PasswordHash).ContinueWith(CreateRegistrationAnswer);

        public override async Task<ExitMessage> ClosePlayerConnection (GrpcService.Guid request, ServerCallContext context) =>
            await _gameServerModel.ClosePlayerConnection(new System.Guid(request.Guid_)).ContinueWith(CreateExitAnswer);

        public override Task<PlayerStateAnswer> GetPlayerState (GrpcService.Guid request, ServerCallContext context) => base.GetPlayerState(request, context);
    }
}
