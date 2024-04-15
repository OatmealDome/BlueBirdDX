using Amazon.S3;
using Amazon.S3.Transfer;

namespace BlueBirdDX.Common.Storage;

public class RemoteStorage
{
    private string _serviceUrl;
    private string _bucketName;

    private readonly AmazonS3Client _client;
    private readonly TransferUtility _transferUtility;

    public RemoteStorage(string serviceUrl, string bucketName, string accessKey, string accessKeySecret)
    {
        _client = new AmazonS3Client(accessKey, accessKeySecret, new AmazonS3Config()
        {
            ServiceURL = serviceUrl
        });

        _transferUtility = new TransferUtility(_client);

        _bucketName = bucketName;
    }
    
    public void TransferFile(string name, byte[] data, string? contentType = null)
    {
        using MemoryStream memoryStream = new MemoryStream(data);
        TransferFile(name, memoryStream, contentType);
    }

    public void TransferFile(string name, Stream inputStream, string? contentType = null)
    {
        TransferUtilityUploadRequest request = new TransferUtilityUploadRequest()
        {
            BucketName = _bucketName,
            Key = name,
            InputStream = inputStream,
            CannedACL = S3CannedACL.Private,
            AutoResetStreamPosition = true,
            AutoCloseStream = false
        };

        if (contentType != null)
        {
            request.ContentType = contentType;
        }

        _transferUtility.Upload(request);
    }
}