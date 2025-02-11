﻿using System.Security.Claims;

namespace Ethachat.Auth.Domain.Models.ModelExtensions
{
    public static class UserClaimExtensions
    {
        public static Claim ToClaim(this UserClaim uClaim)
        {
            return new Claim(uClaim.Type, uClaim.Value);
        }
    }
}
