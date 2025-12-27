using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Data
{
    internal class LogBookContext : DbContext 
    {
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<PresetUserSettings> PresetUserSettings { get; set; }

        public LogBookContext(DbContextOptions<LogBookContext> options)
                : base(options)
        {
        }

        public async Task SubmitLoginEntry(Guid sessionId, string userName, DateTime startDate)
        {
            await UserSessions.AddAsync(new UserSession()
            {
                SessionID = sessionId,
                UserName = userName,
                SessionStart = startDate,
                
            });
            await this.SaveChangesAsync();
        }

        public async Task SubmitLogoutEntry(Guid sessionId, string json)
        {

            var logentry = UserSessions.Where(x => x.SessionID == sessionId).FirstOrDefault();
            if (logentry is null)
                return;
            logentry.SessionEnd = DateTime.Now;
            logentry.SettingsData = json;
            await this.SaveChangesAsync();
        }

    }

    public class UserSession
    {
        [Key]
        public Guid SessionID { get; set; } = Guid.NewGuid();

        public string? UserName { get; set; } = string.Empty;
        public DateTime SessionStart { get; set; }
        public DateTime SessionEnd { get; set; }
        public string? SettingsData { get; set; } = string.Empty;
    }

    public class PresetUserSettings
    {
        [Key]
        public Guid PresetUserSettingsID { get; set; } = Guid.NewGuid();   
        public string? UserName { get; set; }

        public string SettingsName { get; set; }
        public string? UserLog { get; set; }
    }
}
