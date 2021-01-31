#nullable enable

using System;

using Grpc.Core;

using MazeGame.GrpcService;

namespace MazeGame.Server
{
    internal class Server
    {
        private const int _port = 30051;

        public static void Main (string[] args)
        {
            global::Grpc.Core.Server server = new()
            {
                Services = { GrpcGameService.BindService(new GrpcGameServiceImplementation(new GameServerModel())) },
                Ports = { new ServerPort("localhost", _port, ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine("Greeter server listening on port " + _port);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();
        }
    }
}
