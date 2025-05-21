using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;

namespace MaofAPI.Authorization
{
    public class AdminPermissionHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            // Check if user has admin permissions
            var permissionClaims = context.User.Claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToList();

            // If user has admin permissions, we approve all requirements
            if (permissionClaims.Contains(Permissions.ManageAllStores) ||
                permissionClaims.Contains(Permissions.ManageSystemSettings))
            {
                foreach (var requirement in context.PendingRequirements.ToList())
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
