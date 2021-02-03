#nullable enable

using System;

namespace MazeGame.Client
{
    class CloseConnectionCommand : ClientCommand
    {
        public override void Execute ()
        {
            if (Client.State == ClientState.Started || Client.State == ClientState.Undefined)
            {
                Console.WriteLine("You are not logged in");
            }

            var result = Client.GrpcGameServiceClient.ClosePlayerConnection(new MazeGame.GrpcService.Guid { Guid_ = Client.PlayerGuid.Value.ToString("D") });
            Client.ClearData();

            Console.WriteLine($"Guid status {result.RequestingGuidStatus}");
            switch (result.RequestingGuidStatus)
            {
                case MazeGame.GrpcService.RequestingGuidStatus.Exists:
                    Console.WriteLine("Disconnect from server");
                    break;
                case MazeGame.GrpcService.RequestingGuidStatus.NotExist:
                    Console.WriteLine("Undefined error");
                    break;
            }
        }
    }
}