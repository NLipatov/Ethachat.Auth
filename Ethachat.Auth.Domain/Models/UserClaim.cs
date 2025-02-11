﻿#nullable disable

namespace Ethachat.Auth.Domain.Models
{
    public class UserClaim
    {
        public Guid Id { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public string Value { get; set; } = string.Empty;
    }
}
