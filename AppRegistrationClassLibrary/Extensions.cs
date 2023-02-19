using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRegistrationClassLibrary;

internal static class Extensions
{
    public static string ObscureSecret(this string? secret)
    {
        if (string.IsNullOrEmpty(secret)) return "";
        return $"{secret[..5]}******************";
    }
}
