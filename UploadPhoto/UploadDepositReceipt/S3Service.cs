using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace UploadPhoto;

public class S3Service
{
    private readonly IAmazonS3 _s3Client;

    public S3Service(string accessKey, string secretKey)
    {
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.APSoutheast1
        };
        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task<PutObjectResponse> Upload(Amazon.S3.Model.PutObjectRequest request)
    {
        try
        {
            return await _s3Client.PutObjectAsync(request);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}