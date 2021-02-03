#nullable enable

using System.Security.Cryptography;
using System.Text;

namespace MazeGame.Client
{
    public static class MD5Hash
    {
        public static string GetHashString (string s)
        {
            var bytes = Encoding.Unicode.GetBytes(s);

            MD5CryptoServiceProvider CSP = new();

            var byteHash = CSP.ComputeHash(bytes);

            var hash = string.Empty;

            foreach (var b in byteHash)
                hash += string.Format("{0:x2}", b);

            return hash;
        }
    }
}
