using System.Net;
using Amazon;
using Amazon.S3;
using PutObjectRequest = Amazon.S3.Model.PutObjectRequest;

namespace DepositReceipt;

public class DepositReceiptToS3
{
    private readonly IAmazonS3 _s3Client;
    private List<DepositReceipt> _failedList;
   

    public DepositReceiptToS3()
    {
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.APSoutheast1
        };
        _s3Client = new AmazonS3Client("AKIA3CXJE5UP6NQXDBU3", "yydZo/0lsMW7Y7yk0Ud4VgCh8ATW4YLOT7G24Jaq" , config);
        _failedList = new List<DepositReceipt>();
    }

    public void UploadDepositReceipt(DepositReceipt receipt)
    {
        var stream = new MemoryStream(receipt.DepositPic);
        var key = $"deposit/{receipt.TransId}_{receipt.ReceiptNo}.png";
        var putRequest = new PutObjectRequest
        {
            BucketName = "ailab-payment",
            ContentType = "image/jpeg",
            InputStream = stream,
            Key = key
        };

        var isSuccess = false;
        try
        {
            var putResponse = _s3Client.PutObjectAsync(putRequest).Result;
            if (putResponse.HttpStatusCode == HttpStatusCode.OK)
            {
                Console.WriteLine($"upload {key} success");
                isSuccess = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"upload {key} failed , ex : {ex.Message}");
        }
        finally
        {
            if (!isSuccess)
            {
                Console.WriteLine($"Re upload {key} Start");
                var stream2 = new MemoryStream(receipt.DepositPic);
                var putRequest2 = new PutObjectRequest
                {
                    BucketName = "ailab-payment",
                    ContentType = "image/jpeg",
                    InputStream = stream2,
                    Key = key
                };
                var putResponse = _s3Client.PutObjectAsync(putRequest2).Result;
                if (putResponse.HttpStatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine($"Re upload {key} success");
                }
                else
                {
                    _failedList.Add(receipt);
                }
            }
        }
        
    }


    public void ReUploadFailedList()
    {
        if (_failedList.Any())
        {
            foreach (var failed in _failedList)
            {
                var stream = new MemoryStream(failed.DepositPic);
                var key = $"deposit/{failed.TransId}_{failed.ReceiptNo}.png";
                var putRequest = new PutObjectRequest
                {
                    BucketName = "ailab-payment",
                    ContentType = "image/jpeg",
                    InputStream = stream,
                    Key = key
                };
                
                var putResponse = _s3Client.PutObjectAsync(putRequest).Result;
                if (putResponse.HttpStatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine($"Re upload failedList , {putRequest.Key} success");
                }
                else
                {
                    Console.WriteLine($"Warning ! , {putRequest.Key} Failed");
                }
            }
        }
    }
    
}
