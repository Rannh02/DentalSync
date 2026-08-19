using DentalSync.ViewModels;

namespace DentalSync.ViewModels
{
    public class PermissionRowViewModel
    {
        public int PermissionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, bool> RoleAccess { get; set; } = new();
    }

    public class RolePermissionViewModel
    {
        public string[] Roles { get; } = UserRoles.All;
        public IReadOnlyList<PermissionRowViewModel> Permissions { get; set; } = Array.Empty<PermissionRowViewModel>();
    }
}
