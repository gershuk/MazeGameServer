#nullable enable

using System;

using MazeGame.GrpcService;

using static MazeGame.Client.MD5Hash;

namespace MazeGame.Client
{
    public class LogInCommand : ClientCommand
    {
        public override void Execute ()
        {
            if (Client.State != ClientState.Started)
            {
                Console.WriteLine("Already authorized");
                return;
            }

            Console.WriteLine("Enter your username");
            var login = Console.ReadLine().TrimStart().TrimEnd();

            Console.WriteLine("Enter your password");
            var password = Console.ReadLine().TrimStart().TrimEnd();

            bool? isReconnect = null;
            while (isReconnect == null)
            {
                Console.WriteLine("Close last connection (Y/N)");
                var yesOrNo = Console.ReadLine();
                isReconnect = yesOrNo switch
                {
                    "Y" => true,
                    "N" => false,
                    _ => null,
                };

            }

            var result = Client.GrpcGameServiceClient.LogIn(new AuthorizationData
            {
                UserData = new UserData { Login = login, PasswordHash = GetHashString(password) },
                ClearActiveConnection = isReconnect.Value
            });


            switch (result.Status)
            {
                case AuthorizationStatus.AuthorizationSuccessfull:
                    Client.PlayerGuid = new(result.PlayerGuid.Guid_);
                    Client.SyncState();
                    break;
                case AuthorizationStatus.WrongLoginOrPassword:
                    break;
                case AuthorizationStatus.AnotherConnectionActive:
                    break;
            }

            Console.WriteLine(result.Status);
        }
    }
}
