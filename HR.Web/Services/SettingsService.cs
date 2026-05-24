using System;
using System.Linq;
using System.Collections.Generic;
using HR.Web.Data;
using HR.Web.Models;
using HR.Web.Helpers;

namespace HR.Web.Services
{
    public interface ISettingsService
    {
        string GetSetting(string key, string defaultValue = null);
        void SetSetting(string key, string value, string description = null, bool encrypted = false);
        T GetSetting<T>(string key, T defaultValue = default(T));
    }

    public class SettingsService : ISettingsService
    {
        private readonly HrContext _context;
        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
        private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

        public SettingsService()
        {
            _context = new HrContext();
        }

        public string GetSetting(string key, string defaultValue = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            try
            {
                // Simple cache logic
                if (DateTime.Now - _lastCacheUpdate < _cacheDuration && _cache.ContainsKey(key))
                {
                    return _cache[key];
                }

                var setting = _context.SystemSettings.Find(key);
                if (setting != null)
                {
                    string value = setting.SettingValue;
                    
                    // Decrypt if necessary
                    if (setting.IsEncrypted)
                    {
                        value = EncryptionHelper.Decrypt(value);
                    }

                    // Update cache
                    _cache[key] = value;
                    _lastCacheUpdate = DateTime.Now;
                    
                    return value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error fetching setting '" + key + "': " + ex.Message);
            }

            return defaultValue;
        }

        public T GetSetting<T>(string key, T defaultValue = default(T))
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            string value = GetSetting(key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        public void SetSetting(string key, string value, string description = null, bool encrypted = false)
        {
            var setting = _context.SystemSettings.Find(key);
            if (setting == null)
            {
                setting = new SystemSetting { SettingKey = key };
                _context.SystemSettings.Add(setting);
            }

            setting.SettingValue = encrypted ? EncryptionHelper.Encrypt(value) : value;
            setting.Description = description ?? setting.Description;
            setting.IsEncrypted = encrypted;

            _context.SaveChanges();
            
            // Invalidate cache
            _cache.Remove(key);
            _lastCacheUpdate = DateTime.MinValue;
        }
    }
}
