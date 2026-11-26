using System.Globalization;
using System.Security.Cryptography;

namespace ClinicManagementSystem.Common.Security;

/// <summary>
/// Băm mật khẩu bằng PBKDF2-SHA256. Database KHÔNG BAO GIỜ lưu mật khẩu gốc.
/// Chuỗi lưu có dạng:  PBKDF2-SHA256$số_vòng_lặp$salt(base64)$hash(base64)
///  - salt: chuỗi ngẫu nhiên riêng cho từng tài khoản -> hai người cùng mật khẩu vẫn ra hash khác nhau.
///  - số vòng lặp lớn (210.000) -> kẻ tấn công dò mật khẩu sẽ rất chậm.
/// </summary>
public static class PasswordHasher
{
    private const string Algorithm = "PBKDF2-SHA256";
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return string.Join('$',
            Algorithm,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public static bool Verify(string password, string? storedHash)
    {
        var parts = storedHash?.Split('$');
        if (parts is not { Length: 4 } || parts[0] != Algorithm
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            // So sánh "thời gian hằng": không dừng sớm ở byte sai đầu tiên, tránh lộ thông tin qua thời gian phản hồi.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
