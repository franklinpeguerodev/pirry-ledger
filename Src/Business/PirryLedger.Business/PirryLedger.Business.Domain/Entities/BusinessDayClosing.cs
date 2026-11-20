namespace PirryLedger.Business.Domain.Entities;

public sealed record BusinessDayClosing(
    decimal Sold,
    decimal Cash,
    decimal ConfirmedTransfers,
    decimal PendingTransfers,
    decimal Debt,
    decimal PendingCancellation,
    decimal Cancelled,
    decimal NotDelivered);
