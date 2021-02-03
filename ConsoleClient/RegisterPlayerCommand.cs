using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MazeGame.GrpcService;

using static MazeGame.Client.MD5Hash;

namespace MazeGame.Client
{
    class RegisterPlayerCommand : ClientCommand
    {
        public override void Execute ()
        {
            Console.WriteLine("Enter your username");
            var login = Console.ReadLine().TrimStart().TrimEnd();

            Console.WriteLine("Enter your password");
            var password = Console.ReadLine().TrimStart().TrimEnd();

            var result = Client.GrpcGameServiceClient.RegisterNewUser(new UserData { Login = login, PasswordHash = GetHashString(password)});


            //switch (result.Status)
            //{
            //    case RegistrationStatus.RegistrationSuccessfull:
            //        break;
            //    case RegistrationStatus.LoginAlreadyExist:
            //        break;
            //    case RegistrationStatus.BadInput:
            //        break;
            //}

            Console.WriteLine(result.Status);
        }

    }
}
