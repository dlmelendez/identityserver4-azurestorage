using System.Collections.Generic;
using System.Text.Json;

namespace ElCamino.IdentityServer.AzureStorage.Mappers;

internal static class PropertiesConverter
{
    public static string Convert(Dictionary<string, string> sourceMember) => JsonSerializer.Serialize(sourceMember);

    public static Dictionary<string, string> Convert(string sourceMember)
    {
        if (string.IsNullOrWhiteSpace(sourceMember))
        {
            return [];
        }
        return JsonSerializer.Deserialize<Dictionary<string, string>>(sourceMember);
    }
}

