using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MazeGame.GrpcService;

namespace MazeGame.Client
{
    public class ConnectToRoomCommand : ClientCommand
    {
        public override void Execute ()
        {
            if (Client.State == ClientState.Started)
            {
                Console.WriteLine("Login before connect to room");
                return;
            }

            Console.WriteLine("Enter room guid");
            var roomGuid = Console.ReadLine();
            Console.WriteLine("Enter room password");
            var password = Console.ReadLine();

            var result = Client.GrpcGameServiceClient.ConnectToRoom(new() { PlayerGuid = new() { Guid_ = Client.PlayerGuid.Value.ToString("D") }, RoomGuid = new() { Guid_ = roomGuid }, Password = password });

            ShowLobbyInfo(result);
        }

        private enum LobbyInfoStatus
        {
            Error,
            Closed,
            GameStrated,
            Successfull
        }

        async Task ShowLobbyInfo (Grpc.Core.AsyncServerStreamingCall<RoomPropertiesAnswer> stream)
        {
            CancellationTokenSource cancellationTokenSource = new();
            var token = cancellationTokenSource.Token;
            var status = LobbyInfoStatus.Successfull;

            while (await stream.ResponseStream.MoveNext(token) && status == LobbyInfoStatus.Successfull)
            {
                var result = stream.ResponseStream.Current;
                if (result.RequestingGuidStatus == RequestingGuidStatus.NotExist)
                {
                    Console.WriteLine(result.RequestingGuidStatus);
                    Client.ConnectedRoomGuid = System.Guid.Empty;
                }
                else
                {
                    switch (result.Propertiesstatus)
                    {
                        case RoomPropertiesAnswerStatus.PlayerGuidNotFound or RoomPropertiesAnswerStatus.RoomNotExist or
                            RoomPropertiesAnswerStatus.PlayerAlreadyConnectedToThisRoom or RoomPropertiesAnswerStatus.ChangeRoomException or
                            RoomPropertiesAnswerStatus.CantConnectToStarted or RoomPropertiesAnswerStatus.RoomFull or RoomPropertiesAnswerStatus.WrongPassword:
                            status = LobbyInfoStatus.Error;
                            Console.WriteLine(result.Propertiesstatus);
                            Client.SyncState();
                            break;
                        case RoomPropertiesAnswerStatus.OperationCanceled:
                            status = LobbyInfoStatus.Closed;
                            Console.WriteLine("Server has terminated connection with room.");
                            Client.SyncState();
                            break;
                        case RoomPropertiesAnswerStatus.Successfull:
                            status = LobbyInfoStatus.Successfull;
                            Client.ConnectedRoomGuid = new(result.Properties.Guid.Guid_);

                            Console.WriteLine("=============================================================");
                            Console.WriteLine($"Room guid = {result.Properties.Guid.Guid_}");
                            Console.WriteLine($"{result.Properties.PlayerNames.Count + result.Properties.BotTypes.Count}/{result.Properties.MaxPlayerCount}");
                            Console.WriteLine($"Players {result.Properties.PlayerNames}");
                            Console.WriteLine($"Bots {result.Properties.BotTypes}");
                            Console.WriteLine($"Turns = {result.Properties.TurnsCount}");
                            Console.WriteLine($"Deley = {result.Properties.TurnDeley}");
                            Console.WriteLine($"Map = {result.Properties.MapGuid}");
                            Console.WriteLine($"Status = {result.Properties.Status}");
                            Console.WriteLine("=============================================================");

                            switch (result.Properties.Status)
                            {
                                case RoomStatus.GameStrated:
                                    status = LobbyInfoStatus.GameStrated;
                                    var spectateGameCommand = Client.GetCommandByType<SpectateGameCommand>();

                                    spectateGameCommand?.ShowGameInfo(Client.GrpcGameServiceClient.SpectateGame(new()
                                    {
                                        PlayerGuid = new() { Guid_ = Client.PlayerGuid.Value.ToString("D") },
                                        RoomGuid = new() { Guid_ = result.Properties.Guid.Guid_ },
                                        Password = ""
                                    }), true);
                                    break;
                                case RoomStatus.Deleted or RoomStatus.GameEnded:
                                    status = LobbyInfoStatus.Closed;
                                    Client.ConnectedRoomGuid = System.Guid.Empty;
                                    break;
                            }
                            break;
                    }
                }
            }

            //warn!
            cancellationTokenSource.Dispose();
            stream.Dispose();
            Client.SyncState();
        }
    }
}
