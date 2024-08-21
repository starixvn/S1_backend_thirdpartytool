namespace PaymentTool.Core.Models.Enums;

public enum DepositStatusEnum
{
    Draft = 0,
    Pending = 1,
    Approved = 3,
    Rejected = 4,
    Canceled = 5,
    Expired = 6,
    OnHold = 7,
    Error = 99
}