using HealthScore.Core;
using HealthScore.Integration.DTOs;
using HealthScore.Integration.Interfaces.Microservices;
using HealthScore.Processor.Domain.Interfaces.Configurations;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;

namespace HealthScore.Processor.Domain.Helpers
{
    public static class IdentityHelper
    {
        public static async Task<IPrincipal> GetServicePrincipalUserAsync(IServiceScopeFactory scopeFactory)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var appSettings = scope.ServiceProvider.GetService<IAppSettings>()!;
            var identityMicroservice = scope.ServiceProvider.GetService<IIdentityMicroservice>()!;
            var loginResponse = await identityMicroservice.LoginAsync(appSettings.HealthScoreSystem.Identity.Token);
            if (loginResponse == null) return null;
            var claims = new List<Claim>
            {
                new(Constants.Auth.Claims.UserId, loginResponse.User.Id.ToString()),
                new(Constants.Auth.Claims.ClientId, loginResponse.User.ClientId.ToString()),
                new(Constants.Auth.Claims.PatientId, string.Empty)
            };
            var identity = new ClaimsIdentity(claims, "bearer");
            var principal = new ClaimsPrincipal(identity);
            var worker = new WorkerPrincipal(principal)
            {
                AccessToken = loginResponse.AccessToken
            };
            return worker;
        }
    }
}