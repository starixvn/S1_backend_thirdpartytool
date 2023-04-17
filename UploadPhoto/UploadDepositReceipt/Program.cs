// See https://aka.ms/new-console-template for more information

//using UploadDepositReceipt;

// Console.WriteLine("Enter Environment:");
// var env = Console.ReadLine();
//
// Console.WriteLine($"Environment: {env}");
// Console.WriteLine("Enter CustId:");
// var custIdStr = Console.ReadLine();
// var custPhotoSvc = new CustPhotoService();
// var lastId = custPhotoSvc.GetLastId();
// var photoIdToS3 = new CustPhotoIdToS3("AKIA3CXJE5UP6NQXDBU3", "yydZo/0lsMW7Y7yk0Ud4VgCh8ATW4YLOT7G24Jaq", env);
//
// if (string.IsNullOrEmpty(custIdStr))
// {
//     for (int i = 1; i < lastId; i++)
//     {
//         var photo = custPhotoSvc.GetCustPhotoId(i);
//         if (photo?.PhotoIdPic == null || photo.PhotoIdPic.Length == 0) continue;
//         photoIdToS3.Upload(photo);
//     }
// }
// else 
// {
//     
//     var custId = int.Parse(custIdStr);
//     
//     var photo = custPhotoSvc.GetCustPhotoId(custId);
//     Console.WriteLine(photo.SQ);
//     if (photo?.PhotoIdPic == null || photo.PhotoIdPic.Length == 0) return;
//
//     photoIdToS3.Upload(photo);
// }

using DepositReceipt;

Console.WriteLine("Enter DepositId:");
var depositStr = Console.ReadLine();

var custPhotoSvc = new DepositReceiptService();
var photoIdToS3 = new DepositReceiptToS3();

if (!string.IsNullOrEmpty(depositStr))
{
    var depositId = int.Parse(depositStr);
    var depositReceiptList = custPhotoSvc.GetDepositReceiptById(depositId);

    foreach (var receipt in depositReceiptList)
    {
        if (receipt.DepositPic is not null)
        {
            photoIdToS3.UploadDepositReceipt(receipt);
        }
    }
    
    photoIdToS3.ReUploadFailedList();
}
else
{

    Console.WriteLine($"Start ... ");
    var startTime = new DateTime(2022, 07, 01);
    try
    {
        var endTime = startTime.AddDays(1);
        do
        {
            var depositReceiptList = custPhotoSvc.GetDepositReceiptByTime(startTime,endTime);
            Console.WriteLine($"Getting Data {startTime:yyyy-MM-dd} to {endTime:yyyy-MM-dd} ");
        
            if (depositReceiptList.Any())
            {
                Console.WriteLine($"Getting Data Done , Count:{depositReceiptList.Count()} ");
                Console.WriteLine($"Start Uploading ");

                var count = 0;
                foreach (var receipt in depositReceiptList)
                {
                    if (receipt.DepositPic is not null)
                    {
                        photoIdToS3.UploadDepositReceipt(receipt);
                        count += 1;
                    }
                }

                photoIdToS3.ReUploadFailedList();

                Console.WriteLine($"Finished DateRange.");
                Console.WriteLine($"");
            }

            startTime = endTime;
            endTime = endTime.AddDays(1);
        
        } while (startTime < DateTime.UtcNow) ;
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        throw;
    }
    

    Console.WriteLine($"Finished All.");
    Console.ReadLine();
}
