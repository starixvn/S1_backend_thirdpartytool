using System.ComponentModel;
using System.Net;
using Amazon;
using Amazon.S3;
using PutObjectRequest = Amazon.S3.Model.PutObjectRequest;

namespace UploadPhoto;

public class CustPhotoIdToS3
{
    private readonly string _environment;
    private readonly IAmazonS3 _s3Client;
    public CustPhotoIdToS3(string accessKey, string secretKey, string environment)
    {
        _environment = environment;
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.APSoutheast1
        };
        _s3Client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public int Upload(CustPhotoId photo)
    {
        var site = (SiteEnum)photo.SiteId;
        var stream = new MemoryStream(photo.PhotoIdPic);
        var key = $"{_environment}/{site}/CustPhotoId/{photo.CustId}/{photo.SQ}.jpeg";
        Console.WriteLine($"upload {key} success");
        var putRequest = new PutObjectRequest
        {
            BucketName = "ailab-backoffice",
            ContentType = "image/jpeg",
            InputStream = stream,
            Key = key
        };
        try
        {
            var a = _s3Client.PutObjectAsync(putRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return 0;
        }
                
        Console.WriteLine($"upload {key} success");
        return 1;
    }
}

public enum SiteEnum
{
    [Description("Nova88")]
    NOVA88 = 1,
    [Description("Goal123")]
    GOAL123 = 2,
    [Description("Bwing")]
    BWING = 3
}

public static class EnumExtension
{
    public static bool IsSuccess(this HttpStatusCode statusCode)
    {
        return ((int)statusCode >= 200) && ((int)statusCode <= 299); 
    }
}