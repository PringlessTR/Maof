using Microsoft.AspNetCore.Authorization;

namespace MaofAPI.Authorization
{
    public class PermissionAttribute : AuthorizeAttribute
    {
        public PermissionAttribute(string permission) : base(policy: permission)
        {
        }
    }
}
