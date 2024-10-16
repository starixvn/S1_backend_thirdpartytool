using PaymentTool.Core.Models.Enums;

namespace PaymentTool.Core.Models;

public class DepositInfo
{
    private DateTimeOffset _createTime { get; set; }
    private DateTimeOffset _updateTime { get; set; }
    private DateTimeOffset _flowEndTime { get; set; }
    private DateTimeOffset _statementTime { get; set; }
    private DateTimeOffset? _depositTime { get; set; }
    private DateTimeOffset? _callbackTime { get; set; }
    private DateTimeOffset? _logTime { get; set; }
    private DateTimeOffset? _taskTime { get; set; }
    
    public string TransId { get; set; }
    public int DepositId { get; set; }
    public int CustId { get; set; }
    public DepositStatusEnum Status { get; set; }
    public DateTimeOffset CreateTime
    {
        get => _createTime;
        set => _createTime = new DateTimeOffset(value.DateTime, TimeSpan.Zero);
    }
    public DateTimeOffset UpdateTime
    {
        get => _updateTime;
        set => _updateTime = new DateTimeOffset(value.DateTime, TimeSpan.Zero);
    }
    public DateTimeOffset FlowEndTime
    {
        get => _flowEndTime;
        set => _flowEndTime = new DateTimeOffset(value.DateTime, TimeSpan.Zero);
    }
    public DateTimeOffset StatementTime
    {
        get => _statementTime;
        set => _statementTime = new DateTimeOffset(value.DateTime, TimeSpan.Zero);
    }
    public DateTimeOffset? DepositTime
    {
        get => _depositTime;
        set => _depositTime = value == null ? null
            : new DateTimeOffset(value.Value.DateTime, TimeSpan.Zero);
    }
    public DateTimeOffset? CallbackTime
    {
        get => _callbackTime;
        set => _callbackTime = value == null ? null
            : new DateTimeOffset(value.Value.DateTime, TimeSpan.Zero);
    }
    public bool FirstDeposit { get; set; }
    public DepositStatusEnum? Action { get; set; }
    public DateTimeOffset? LogTime
    {
        get => _logTime;
        set => _logTime = value == null ? null :
            new DateTimeOffset(value.Value.DateTime, TimeSpan.Zero);
    }
    public DateTimeOffset? TaskTime
    {
        get => _taskTime;
        set => _taskTime = value == null ? null :
            new DateTimeOffset(value.Value.DateTime, TimeSpan.Zero);
    }
}