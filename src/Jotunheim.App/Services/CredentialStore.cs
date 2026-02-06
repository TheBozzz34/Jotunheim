using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Windows.Security.Credentials;

namespace Jotunheim.App.Services;

internal sealed class CredentialStore
{
    private const string ResourceName = "Jotunheim.SpaceTrack";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Jotunheim.SpaceTrack.v1");

    public bool TryLoad(out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        try
        {
            var vault = new PasswordVault();
            var creds = vault.FindAllByResource(ResourceName);
            var credential = creds?.FirstOrDefault();
            if (credential is null)
            {
                return TryLoadFromFile(out username, out password);
            }

            credential = vault.Retrieve(ResourceName, credential.UserName);
            credential.RetrievePassword();
            username = credential.UserName;
            password = credential.Password ?? string.Empty;
            return !string.IsNullOrEmpty(username);
        }
        catch
        {
            return TryLoadFromFile(out username, out password);
        }
    }

    public void Save(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        Clear();
        try
        {
            var vault = new PasswordVault();
            vault.Add(new PasswordCredential(ResourceName, username, password));
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"PasswordVault save failed, using DPAPI fallback: {ex.Message}");
            SaveToFile(username, password);
        }
    }

    public void Clear()
    {
        try
        {
            var vault = new PasswordVault();
            var creds = vault.FindAllByResource(ResourceName);
            if (creds is null)
            {
                DeleteFileFallback();
                return;
            }

            foreach (var cred in creds)
            {
                vault.Remove(cred);
            }
        }
        catch
        {
        }

        DeleteFileFallback();
    }

    private static bool TryLoadFromFile(out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        try
        {
            if (!File.Exists(AppStorage.CredentialsPath))
            {
                return false;
            }

            var data = File.ReadAllBytes(AppStorage.CredentialsPath);
            var decrypted = ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decrypted);
            var record = JsonSerializer.Deserialize<CredentialRecord>(json);
            if (record is null || string.IsNullOrWhiteSpace(record.Username))
            {
                return false;
            }

            username = record.Username;
            password = record.Password ?? string.Empty;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveToFile(string username, string password)
    {
        try
        {
            var record = new CredentialRecord
            {
                Username = username,
                Password = password
            };
            var json = JsonSerializer.Serialize(record);
            var data = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(AppStorage.CredentialsPath, encrypted);
        }
        catch
        {
        }
    }

    private static void DeleteFileFallback()
    {
        try
        {
            if (File.Exists(AppStorage.CredentialsPath))
            {
                File.Delete(AppStorage.CredentialsPath);
            }
        }
        catch
        {
        }
    }

    private sealed class CredentialRecord
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
