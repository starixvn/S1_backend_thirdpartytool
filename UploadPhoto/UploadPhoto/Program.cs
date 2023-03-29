// See https://aka.ms/new-console-template for more information

using UploadPhoto;

Console.WriteLine("Enter Environment:");
var env = Console.ReadLine();

Console.WriteLine($"Environment: {env}");
Console.WriteLine("Enter CustId:");
var custIdStr = Console.ReadLine();
var custPhotoSvc = new CustPhotoService();
var lastId = custPhotoSvc.GetLastId();
var photoIdToS3 = new CustPhotoIdToS3("AKIA3CXJE5UP6NQXDBU3", "yydZo/0lsMW7Y7yk0Ud4VgCh8ATW4YLOT7G24Jaq", env);

if (string.IsNullOrEmpty(custIdStr))
{
    for (int i = 1; i < lastId; i++)
    {
        var photo = custPhotoSvc.GetCustPhotoId(i);
        if (photo?.PhotoIdPic == null || photo.PhotoIdPic.Length == 0) continue;
        photoIdToS3.Upload(photo);
    }
}
else 
{
    
    var custId = int.Parse(custIdStr);
    
    var photo = custPhotoSvc.GetCustPhotoId(custId);
    Console.WriteLine(photo.SQ);
    if (photo?.PhotoIdPic == null || photo.PhotoIdPic.Length == 0) return;

    photoIdToS3.Upload(photo);
}

