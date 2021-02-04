#nullable enable

using System;

using Grpc.Core;

using MazeGame.GrpcService;

namespace MazeGame.Server
{
    internal class Server
    {
        public static void Main (string[] args)
        {

            Console.WriteLine("Enter ip");
            var ip = Console.ReadLine();
            if (ip == null || ip == string.Empty)
            {
                ip = "localhost";
                Console.WriteLine(ip);
            }
            Console.WriteLine("Enter port");
            var port = Console.ReadLine();
            if (port == null || port == string.Empty)
            {
                port = "30051";
                Console.WriteLine(port);
            }

            Grpc.Core.Server server = new()
            {
                Services = { GrpcGameService.BindService(new GrpcGameServiceImplementation(new GameServerModel())) },
                Ports = { new ServerPort("localhost", Convert.ToInt32(port), ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine("Server listening on port " + port);
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();
        }
    }
}
