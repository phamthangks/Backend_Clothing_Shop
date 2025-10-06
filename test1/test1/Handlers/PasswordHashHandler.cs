using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;

namespace test1.Handlers
{
    public class PasswordHashHandler
    {
        private static int iterationCount = 100000;
        private static RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();

        public static string HashPassword(string password)
        {
            int saltSize = 128 / 8;
            var salt = new byte[saltSize];
            randomNumberGenerator.GetBytes(salt);

            var subkey = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA512, iterationCount, 256 / 8);

            var outputBytes = new byte[13 + salt.Length + subkey.Length];
            outputBytes[0] = 0x01;

            WriteNetworkByteOrder(outputBytes, 1, (uint)KeyDerivationPrf.HMACSHA512);
            WriteNetworkByteOrder(outputBytes, 5, (uint)iterationCount);
            WriteNetworkByteOrder(outputBytes, 9, (uint)saltSize);

            Buffer.BlockCopy(salt, 0, outputBytes, 13, salt.Length);
            Buffer.BlockCopy(subkey, 0, outputBytes, 13 + salt.Length, subkey.Length);

            return Convert.ToBase64String(outputBytes);
        }
        private static void WriteNetworkByteOrder(byte[] outputBytes, int offset, uint value)
        {
            outputBytes[offset] = (byte)(value >> 24);
            outputBytes[offset + 1] = (byte)(value >> 16);
            outputBytes[offset + 2] = (byte)(value >> 8);
            outputBytes[offset + 3] = (byte)(value);
        }
        public static bool VerifyPassword(string password, string hash)
        {
            try
            {
                // Chuyển đổi hash từ Base64 về dạng byte array
                var hashedPassword = Convert.FromBase64String(hash);

                // Đọc các giá trị từ hash
                var keyDerivationPrf = (KeyDerivationPrf)ReadNetworkByteOrder(hashedPassword, 1);
                var iterationCount = (int)ReadNetworkByteOrder(hashedPassword, 5);
                var saltLength = (int)ReadNetworkByteOrder(hashedPassword, 9);

                // Kiểm tra độ dài salt hợp lệ
                if (saltLength < 128 / 8) return false;

                var salt = new byte[saltLength];
                Buffer.BlockCopy(hashedPassword, 13, salt, 0, salt.Length);

                var subkeyLength = hashedPassword.Length - 13 - salt.Length;
                var subkey = new byte[subkeyLength];
                Buffer.BlockCopy(hashedPassword, 13 + salt.Length, subkey, 0, subkey.Length);

                // Tính toán subkey từ mật khẩu và salt
                var computedSubkey = KeyDerivation.Pbkdf2(password, salt, keyDerivationPrf, iterationCount, subkey.Length);

                // So sánh subkey tính toán với subkey đã lưu trong hash
                return subkey.SequenceEqual(computedSubkey);
            }
            catch
            {
                return false;
            }
        }

        private static uint ReadNetworkByteOrder(byte[] data, int offset)
        {
            return (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
        }
    }
}
