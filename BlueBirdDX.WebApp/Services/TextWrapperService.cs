using BlueBirdDX.Common.Util.TextWrapper;
using BlueBirdDX.WebApp.Models;
using Microsoft.Extensions.Options;

namespace BlueBirdDX.WebApp.Services;

public class TextWrapperService
{
    public readonly TextWrapperClient Client;

    public TextWrapperService(IOptions<TextWrapperSettings> settings)
    {
        Client = new TextWrapperClient(settings.Value.Server);
    }
}
