namespace Custom.Framework.Identity
{
    public interface IAppUser
    {
        string FirstName { get; set; }
        string FullName { get; set; }
        string LastName { get; set; }
        string Password { get; set; }
        int RoleId { get; set; }
        string UnitGuid { get; }
        string UnitName { get; }
        string Units { get; set; }
        int UserId { get; set; }
        string UserTypeName { get; set; }

        string ToStringJson();
    }
}