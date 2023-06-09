using ThirdPartyTool.Core.Models.Enums;
using ThirdPartyTool.Core.Models.Saba;

namespace ThirdPartyTool.Core.Models.PT;

internal class PTTransferResponse
{
    public PTTransferResponse(PTErrorResponse errorResult, PTWithdrawResponse successResult)
    {
        ErrorResult = errorResult;
        SuccessResult = successResult;
    }

    public PTErrorResponse ErrorResult { get; set; }

    public PTWithdrawResponse SuccessResult { get; set; }
}

internal class PTCheckFTransferResponse
{
    public PTCheckFTransferResponse(PTErrorResponse errorResult, PTCheckTransactionResponse successResult)
    {
        ErrorResult = errorResult;
        SuccessResult = successResult;
    }

    public PTErrorResponse ErrorResult { get; set; }

    public PTCheckTransactionResponse SuccessResult { get; set; }
}

internal class PTDefinition
{

}
