using SimpleExpenseTracker.Application;
using SimpleExpenseTracker.Domain;

namespace SimpleExpenseTracker.Infrastructure;
public static class FamilyFilter
{
    public static IQueryable<Transaction> Apply(IQueryable<Transaction> rows, OwnershipKind? ownership, int? ownerMemberId)
    {
        if ((ownership.HasValue && !Enum.IsDefined(ownership.Value)) || ownerMemberId <= 0 || (ownerMemberId.HasValue && ownership.HasValue && ownership != OwnershipKind.Personal))
            throw new AppException(400, "收支歸屬篩選不正確。");
        if (ownership.HasValue) rows = rows.Where(t => t.Ownership == ownership);
        if (ownerMemberId.HasValue) rows = rows.Where(t => t.Ownership == OwnershipKind.Personal && t.OwnerMemberId == ownerMemberId);
        return rows;
    }
}
