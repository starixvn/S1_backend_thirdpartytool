namespace AMBSlot
{
    public class TpCustInfo
    {
        public int TpId { get; set; }
        public int CustId { get; set; }
        public string UserName { get; set; }
        public string TpUserName { get; set; }
        public string TpPwd { get; set; }
    }
    
    public class Configuration
    {
        public string DatabaseConnectionString { get; set; }
        public AmbSetting AmbSetting { get; set; }
    }

    public class AmbSetting
    {
        public string Agent { get; set; }
        public string ApiUrl { get; set; }
        public string SecretKey { get; set; }
    }
    
    public class CommonResponse
    {
        public Status status { get; set; }
    }
    public class Status
    {
        public int code { get; set; }
        public string message { get; set; }
    }
}