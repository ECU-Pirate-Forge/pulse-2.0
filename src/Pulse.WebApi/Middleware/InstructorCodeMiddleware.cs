using System.Security.Cryptography;
using System.Text;

namespace Pulse.WebApi.Middleware;

public sealed class InstructorCodeMiddleware
{
    public const string HeaderName = "InstructorCode";

    public static bool IsInstructorCodeValid(string? instructorCode, string? configuredInstructorCode)
    {
        if (string.IsNullOrWhiteSpace(instructorCode) || string.IsNullOrWhiteSpace(configuredInstructorCode))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(instructorCode),
            Encoding.UTF8.GetBytes(configuredInstructorCode));
    }
}
