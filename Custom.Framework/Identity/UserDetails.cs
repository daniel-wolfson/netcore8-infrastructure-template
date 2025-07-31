namespace Custom.Framework.Identity
{
    public class SunClubUserDetails
    {
        public string? HebrewFirstName { get; set; }
        public string? HebrewLastName { get; set; }
        public string? EnglishFirstName { get; set; }
        public string? EnglishLastName { get; set; }
        public DateTime Birthday { get; set; }
        public string? StreetAddress { get; set; }
        public string? AdditionalStreetAddress { get; set; }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public string? PassportId { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public bool BlockSendingSms { get; set; }
        public DateTime? SpouseBirthday { get; set; }
        public DateTime? Anniverssary { get; set; }
        public string? CcToken { get; set; }
        public string? SpousePassportId { get; set; }
        public string? SpouseName { get; set; }
        public string RegisterKey { get; set; }
        public string? TestNlb { get; set; }
        public bool IsMobile { get; set; }
        public PaymentTransactionsDetails PaymentDetails { get; set; }
    }

    public class PaymentTransactionsDetails
    {
        public string? ChargeStatusCode { get; set; }
        public string ConfirmationKey { get; set; }
        public string CreditCardNumber { get; set; }
        public string? DebitApproveNumber { get; set; }
        public decimal Total { get; set; }
        public int PaymentsNumber { get; set; }
    }
}