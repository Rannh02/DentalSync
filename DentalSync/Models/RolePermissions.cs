namespace DentalSync.Models
{
    public class PermissionDefinition
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public class RolePermission
    {
        public int Id { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int PermissionDefinitionId { get; set; }
        public bool IsEnabled { get; set; }
        public PermissionDefinition PermissionDefinition { get; set; } = null!;
    }
}
