using System;

namespace MazeGame.Client
{
    class UpCommand : ClientCommand
    {
        public override void Execute ()
        {
            if (!Client.PlayerGuid.HasValue || Client.PlayerGuid.Value == Guid.Empty)
            {
                Console.WriteLine("You must login first");
                return;
            }

            if (!Client.ConnectedRoomGuid.HasValue || Client.ConnectedRoomGuid.Value == Guid.Empty)
            {
                Console.WriteLine("You must connect to room");
                return;
            }

            var result = Client.GrpcGameServiceClient.SetDirection(new GrpcService.PlayerDirection()
            {
                DirectionState = GrpcService.DirectionState.Down,
                Turn = Client.CurrentTurn,
                PlayerAndRoomGuids = new()
                {
                    PlayerGuid = new() { Guid_ = Client.PlayerGuid.Value.ToString("D") },
                    RoomGuid = new() { Guid_ = Client.ConnectedRoomGuid.Value.ToString("D") }
                }
            });

            Console.WriteLine(result.SetDirectionState);
        }
    }
}
