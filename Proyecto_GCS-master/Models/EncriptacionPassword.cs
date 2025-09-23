using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Proyecto_GCS.Models
{
    public static class EncriptacionPassword
    {
        private const int SaltSize = 16;        // 128 bits
        private const int KeySize = 64;        // 512 bits
        private static readonly int Iterations =
            int.TryParse(ConfigurationManager.AppSettings["HashIterations"], out var it) ? it : 350000;

        // Para verificar hashes antiguos (con salt fijo). Asegúrate de que tenga ≥ 8 bytes.
        private static readonly string LegacySalt =
            ConfigurationManager.AppSettings["HashSalt"] ?? "uN7pQ9zK3sR1tV5x";

        // Nuevo formato: iterations.base64Salt.base64Hash
        public static string Hashear(string password)
        {
            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA512))
            {
                var key = pbkdf2.GetBytes(KeySize);
                return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
            }
        }

        public static bool Verificar(string password, string hashAlmacenado)
        {
            // Nuevo formato
            var parts = hashAlmacenado.Split('.');
            if (parts.Length == 3 && int.TryParse(parts[0], out var iters))
            {
                try
                {
                    var salt = Convert.FromBase64String(parts[1]);
                    var hash = Convert.FromBase64String(parts[2]);

                    using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iters, HashAlgorithmName.SHA512))
                    {
                        var keyToCheck = pbkdf2.GetBytes(hash.Length);
                        return ConstantTimeEquals(keyToCheck, hash);
                    }
                }
                catch { return false; }
            }

            // LEGADO: hashes antiguos con salt fijo
            var legacySaltBytes = Encoding.UTF8.GetBytes(LegacySalt); // ¡≥ 8 bytes!
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, legacySaltBytes, Iterations, HashAlgorithmName.SHA512))
            {
                var key = pbkdf2.GetBytes(KeySize);
                try
                {
                    var stored = Convert.FromBase64String(hashAlmacenado);
                    return ConstantTimeEquals(key, stored);
                }
                catch { return false; }
            }
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            uint diff = (uint)a.Length ^ (uint)b.Length;
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
                diff |= (uint)(a[i] ^ b[i]);
            return diff == 0 && a.Length == b.Length;
        }
    }
}
