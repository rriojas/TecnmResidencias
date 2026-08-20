namespace TecNM.Residency.Documents;

public static class DocumentStatus
{
    public const string Uploaded = "uploaded";
    public const string UnderReview = "under_review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        Uploaded,
        UnderReview,
        Approved,
        Rejected
    };

    public static bool IsValid(string status) => ValidStatuses.Contains(status);
}
