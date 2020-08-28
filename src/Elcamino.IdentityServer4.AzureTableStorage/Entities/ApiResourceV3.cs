using System;
using System.Collections.Generic;
using System.Text;

namespace ElCamino.IdentityServer4.AzureStorage.Entities
{
    /// <summary>
    /// Used for migration only
    /// </summary>
    public class ApiResourceV3
    {
        public int Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public List<ApiResourceSecret> Secrets { get; set; } = new List<ApiResourceSecret>();
        public List<ApiScope> Scopes { get; set; } = new List<ApiScope>();
        public List<ApiResourceClaim> UserClaims { get; set; } = new List<ApiResourceClaim>();
        public List<ApiResourceProperty> Properties { get; set; } = new List<ApiResourceProperty>();
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Updated { get; set; }
        public DateTime? LastAccessed { get; set; }
        public bool NonEditable { get; set; }
    }
}
