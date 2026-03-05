using System;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

public class TestHash {
    public static void Main() {
        string password = "Admin@123";
        string hash = "100000.66/ziKCror0LSOo7o1mXKg==.HEwwkXvTGHxdP4o/k9ZzQ/VaicmZpbZOJkEQtEM843k=";
        
        bool verified = VerifyPassword(hash, password);
        Console.WriteLine("Verified: " + verified);
    }

    public static bool VerifyPassword(string hash, string password) {
        var parts = hash.Split(new[] { '.' }, 3);
        var iterations = Convert.ToInt32(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var key = Convert.FromBase64String(parts[2]);

        using (var algorithm = new Rfc2898DeriveBytes(password, salt, iterations)) {
            var keyToCheck = algorithm.GetBytes(32);
            return keyToCheck.SequenceEqual(key);
        }
    }
}
