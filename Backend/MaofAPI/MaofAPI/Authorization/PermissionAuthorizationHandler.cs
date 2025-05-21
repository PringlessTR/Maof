using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;

namespace MaofAPI.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            // Check if user has the required permission from the claim
            var permissionClaims = context.User.Claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToList();

            // Check for the specific permission
            if (permissionClaims.Contains(requirement.Permission))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // If user doesn't have the specific permission, check if they have admin permissions
            if (permissionClaims.Contains(Permissions.ManageAllStores) || 
                permissionClaims.Contains(Permissions.ManageSystemSettings))
            {
                // Admin users have access to all permissions
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
