using System;
using System.Threading;
using System.Threading.Tasks;

using MazeGame.GrpcService;

namespace MazeGame.Client
{
    class SpectateGameCommand : ClientCommand
    {
        public override void Execute ()
        {
            if (Client.State == ClientState.Started)
            {
                Console.WriteLine("Login before spectate room");
                return;
            }

            //if (Client.SpectatingGuid != null && Client.SpectatingGuid.Value != System.Guid.Empty)
            //{
            //    Console.WriteLine("You alredy spectating");
            //    return;
            //}

            Console.WriteLine("Enter room guid");
            var roomGuid = Console.ReadLine();
            Console.WriteLine("Enter room password");
            var password = Console.ReadLine();

            var result = Client.GrpcGameServiceClient.SpectateGame(new() { PlayerGuid = new() { Guid_ = Client.PlayerGuid.Value.ToString("D") }, RoomGuid = new() { Guid_ = roomGuid }, Password = password });
            ShowGameInfo(result, Client.ConnectedRoomGuid != null && new System.Guid(roomGuid) == Client.ConnectedRoomGuid);
        }

        private Vec2Int AddVect2Int (Vec2Int a, Vec2Int b) =>
            new()
            {
                X = a.X + b.X,
                Y = a.Y + b.Y,
            };

        public async Task ShowGameInfo (Grpc.Core.AsyncServerStreamingCall<GrpcService.SpectateGameAnswer> stream, bool isPlayer)
        {
            var isSetSize = false;
            BlockType[,] blockTypes = default;
            Vec2Int size = default;
            CancellationTokenSource cancellationTokenSource = new();
            var token = cancellationTokenSource.Token;

            while (await stream.ResponseStream.MoveNext(token))
            {
                Console.Clear();
                var result = stream.ResponseStream.Current;
                switch (result.SpectateGameStatus)
                {
                    case GrpcService.SpectateGameStatus.GameNotStartedNothingSpectate or GrpcService.SpectateGameStatus.ClosedSpectateChannel
                    or GrpcService.SpectateGameStatus.PlayerNotFoundToSpectete or GrpcService.SpectateGameStatus.WrongRoomToSpectate
                    or GrpcService.SpectateGameStatus.PlayerAlreadySpectedThisRoom:
                        Console.Clear();
                        Console.WriteLine(result.SpectateGameStatus);
                        return;
                    case GrpcService.SpectateGameStatus.OpenSpectateChannel:
                        switch (result.SpectateData.Status)
                        {
                            case AvatarState.AvatarRunnig:
                                if (isPlayer)
                                {
                                    if (!isSetSize)
                                    {
                                        blockTypes = new BlockType[result.SpectateData.MapSize.X, result.SpectateData.MapSize.Y];
                                        size = new() { X = result.SpectateData.MapSize.X, Y = result.SpectateData.MapSize.Y };
                                        isSetSize = true;
                                        //Console.SetWindowSize(size.X, size.Y);
                                    }
                                    foreach (var block in result.SpectateData.BlockInfos)
                                    {
                                        blockTypes[block.Pos.X, block.Pos.Y] = block.BlockType;
                                    }
                                }
                                else
                                {
                                    if (!isSetSize)
                                    {
                                        blockTypes = new BlockType[result.SpectateData.MapSize.X, result.SpectateData.MapSize.Y];
                                        size = new() { X = result.SpectateData.MapSize.X, Y = result.SpectateData.MapSize.Y };
                                        foreach (var block in result.SpectateData.BlockInfos)
                                        {
                                            blockTypes[block.Pos.X, block.Pos.Y] = block.BlockType;
                                        }
                                        isSetSize = true;
                                        //Console.SetWindowSize(size.X, size.Y);
                                    }
                                }

                                Console.Clear();

                                for (var i = 0; i < size.Y; ++i)
                                {
                                    for (var j = 0; j < size.X; ++j)
                                    {
                                        switch (blockTypes[j, i])
                                        {
                                            case BlockType.BlockUndefined:
                                                Console.Write("@");
                                                break;
                                            case BlockType.BlockWall:
                                                Console.Write("#");
                                                break;
                                            case BlockType.BlockEmpty:
                                                Console.Write("·");
                                                break;
                                            case BlockType.BlockExit:
                                                Console.Write("n");
                                                break;
                                        }
                                    }

                                    Console.WriteLine();
                                }

                                foreach (var enemy in result.SpectateData.PlayerInfos)
                                {
                                    Console.SetCursorPosition(enemy.Pos.X, enemy.Pos.Y);
                                    Console.Write("E");
                                }

                                if (isPlayer)
                                {
                                    Console.SetCursorPosition(result.SpectateData.Pos.X, result.SpectateData.Pos.Y);
                                    Console.WriteLine("P");
                                }

                                Console.SetCursorPosition(0, size.Y);
                                Console.WriteLine($"Turn = {result.SpectateData.Turn}");
                                if (isPlayer)
                                    Client.CurrentTurn = result.SpectateData.Turn;
                                break;
                            case AvatarState.AvatarWin:
                                Console.Clear();
                                Console.WriteLine("You escape");
                                Client.SyncState();
                                break;
                            case AvatarState.AvatarLose:
                                Console.Clear();
                                Console.WriteLine("You die");
                                Client.SyncState();
                                break;
                        }
                        break;
                }
            }
            Client.SyncState();
        }
    }
}