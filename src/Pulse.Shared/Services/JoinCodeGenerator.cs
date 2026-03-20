using System.Security.Cryptography;

namespace Pulse.Shared.Services;

public interface IJoinCodeGenerator
{
    string Generate();
}

public class JoinCodeGenerator : IJoinCodeGenerator
{
    private const string Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int Length = 6;

    public string Generate()
    {
        var result = new char[Length];
        for (int i = 0; i < Length; i++)
            result[i] = Chars[RandomNumberGenerator.GetInt32(Chars.Length)];
        return new string(result);
    }
}
