using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace HashGen {
    public class Program {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100000;

        public static void Main(string[] args) {
            string password = args[0];
            using (var algorithm = new Rfc2898DeriveBytes(password, SaltSize, Iterations)) {
                var key = Convert.ToBase64String(algorithm.GetBytes(KeySize));
                var salt = Convert.ToBase64String(algorithm.Salt);
                Console.WriteLine(string.Format("{0}.{1}.{2}", Iterations, salt, key));
            }
        }
    }
}
