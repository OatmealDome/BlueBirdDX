using BlueBirdDX.Common.Media;
using BlueBirdDX.Api;

namespace BlueBirdDX.WebApp.Api;

public static class UploadedMediaApiExtensions
{
    public static UploadedMediaApi CreateApiFromCommon(UploadedMedia realMedia)
    {
        return new UploadedMediaApi()
        {
            Id = realMedia._id.ToString(),
            Name = realMedia.Name,
            AltText = realMedia.AltText
        };
    }

    public static void TransferApiToCommon(this UploadedMediaApi apiMedia, UploadedMedia realMedia)
    {
        realMedia.Name = apiMedia.Name;
        realMedia.AltText = apiMedia.AltText;
    }

}