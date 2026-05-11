using System.Security.Cryptography;

namespace BlueBirdDX.WebApp.Services;

public static class AuthorizationUtil
{
    public static string GenerateRandomString(int length)
    {
        const string characterPool = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return RandomNumberGenerator.GetString(characterPool, length);
    }
}
