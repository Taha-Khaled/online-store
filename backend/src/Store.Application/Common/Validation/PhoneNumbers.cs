namespace Store.Application.Common.Validation;

/// <summary>Phone rule shared by registration and checkout (002 FR-013, 003 FR-014).</summary>
public static class PhoneNumbers
{
    public const int MaxLength = 20;

    public const string InvalidMessage = "Phone must contain digits 0-9 only, with an optional leading +.";

    /// <summary>Optional leading + followed by ASCII digits 0-9 only (no digits from other scripts).</summary>
    public static bool IsValid(string phone)
    {
        var digits = phone.StartsWith('+') ? phone[1..] : phone;
        return digits.Length > 0 && digits.All(char.IsAsciiDigit);
    }
}
