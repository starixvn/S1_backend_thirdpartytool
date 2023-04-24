using System;
using System.Collections.Generic;
using System.Text;
using ThirdPartyTool.Core.Models;

namespace ThirdPartyTool.Core.ThirdParty.Interfaces
{
    public interface ITpHelper
    {
        int TpId { get; }

        IEnumerable<int> ParallelKickUsers(IEnumerable<TpCustInfoModel> todoList, int taskCount);

        (IEnumerable<TransferBackFailedRecord> FailedRecords, IEnumerable<TransferRemainRecord> RemainRecords)
            ParallelTransferBack(IEnumerable<TpCustInfoModel> todoList, int taskCount);

        (IEnumerable<TransferBackFailedRecord> FailedRecords, IEnumerable<TransferRemainRecord> RemainRecords)
            ParallelTransferRetry(IEnumerable<TransferBackRetryModel> todoList, int taskCount);

        (IEnumerable<TransferBackFailedRecord> FailedRecords, IEnumerable<TransferRemainRecord> RemainRecords)
            ParallelKickUserAndTransferBack(IEnumerable<TpCustInfoModel> todoList, int taskCount);

        IEnumerable<UserBalance> GetBalance(IEnumerable<TpCustInfoModel> todoList, int taskCount);
    }
}
