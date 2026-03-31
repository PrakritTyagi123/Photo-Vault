using System.Security.Cryptography;
using System.Text;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class VaultService
{
    private readonly DatabaseService _db; private readonly LogService _log; private readonly string _vaultDir;
    private byte[]? _key; private bool _isUnlocked;
    public bool IsUnlocked => _isUnlocked;
    public VaultService(DatabaseService db, LogService log, string vaultDir) { _db = db; _log = log; _vaultDir = vaultDir; Directory.CreateDirectory(vaultDir); }

    public bool HasPassword() { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT value FROM settings WHERE key='vault_hash'"; var r = cmd.ExecuteScalar(); return r != null && r != DBNull.Value; }

    public bool SetPassword(string pwd)
    {
        using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "INSERT OR REPLACE INTO settings(key,value) VALUES('vault_hash',@h)";
        cmd.Parameters.AddWithValue("@h", HashPwd(pwd)); cmd.ExecuteNonQuery();
        _key = DeriveKey(pwd); _isUnlocked = true; _log.Info("Vault", "Password set"); return true;
    }

    public bool Unlock(string pwd)
    {
        using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT value FROM settings WHERE key='vault_hash'";
        var stored = cmd.ExecuteScalar()?.ToString(); if (stored == null) return false;
        if (HashPwd(pwd) == stored) { _key = DeriveKey(pwd); _isUnlocked = true; _log.Info("Vault", "Unlocked"); return true; }
        _log.Warn("Vault", "Wrong password"); return false;
    }

    public void Lock() { _key = null; _isUnlocked = false; _log.Info("Vault", "Locked"); }

    public bool MoveToVault(long mediaId)
    {
        if (!_isUnlocked || _key == null) return false;
        using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT file_path,file_name FROM media WHERE id=@id"; cmd.Parameters.AddWithValue("@id", mediaId);
        using var r = cmd.ExecuteReader(); if (!r.Read()) return false;
        var path = r.GetString(0); var name = r.GetString(1); if (!File.Exists(path)) return false;
        try
        {
            var enc = Path.Combine(_vaultDir, $"{mediaId}.vault"); Encrypt(path, enc, _key); File.Delete(path);
            using var u = _db.Connection.CreateCommand(); u.CommandText = "UPDATE media SET in_vault=1, file_path=@p WHERE id=@id"; u.Parameters.AddWithValue("@p", enc); u.Parameters.AddWithValue("@id", mediaId); u.ExecuteNonQuery();
            _log.Info("Vault", $"Vaulted: {name}"); return true;
        } catch (Exception ex) { _log.Error("Vault", $"Vault fail: {ex.Message}"); return false; }
    }

    public bool RestoreFromVault(long mediaId, string restorePath)
    {
        if (!_isUnlocked || _key == null) return false;
        using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT file_path,file_name FROM media WHERE id=@id AND in_vault=1"; cmd.Parameters.AddWithValue("@id", mediaId);
        using var r = cmd.ExecuteReader(); if (!r.Read()) return false;
        var enc = r.GetString(0); var name = r.GetString(1); var dec = Path.Combine(restorePath, name);
        try
        {
            Decrypt(enc, dec, _key); File.Delete(enc);
            using var u = _db.Connection.CreateCommand(); u.CommandText = "UPDATE media SET in_vault=0, file_path=@p WHERE id=@id"; u.Parameters.AddWithValue("@p", dec); u.Parameters.AddWithValue("@id", mediaId); u.ExecuteNonQuery();
            _log.Info("Vault", $"Restored: {name}"); return true;
        } catch (Exception ex) { _log.Error("Vault", $"Restore fail: {ex.Message}"); return false; }
    }

    public List<MediaItem> GetVaultedItems()
    {
        var items = new List<MediaItem>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id,file_path,file_name,file_extension,file_size,media_type,thumbnail_small,has_thumbnail FROM media WHERE in_vault=1 ORDER BY file_name";
        using var r = cmd.ExecuteReader();
        while (r.Read()) { Enum.TryParse<MediaType>(r.IsDBNull(5) ? "Photo" : r.GetString(5), out var mt); items.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2), FileExtension = r.GetString(3), FileSize = r.GetInt64(4), MediaType = mt, ThumbnailSmall = r.IsDBNull(6) ? null : r.GetString(6), HasThumbnail = !r.IsDBNull(7) && r.GetInt32(7) == 1, InVault = true }); }
        return items;
    }

    private static void Encrypt(string inp, string outp, byte[] key) { using var aes = Aes.Create(); aes.Key = key; aes.GenerateIV(); using var o = File.Create(outp); o.Write(aes.IV, 0, aes.IV.Length); using var enc = aes.CreateEncryptor(); using var cs = new CryptoStream(o, enc, CryptoStreamMode.Write); using var i = File.OpenRead(inp); i.CopyTo(cs); }
    private static void Decrypt(string inp, string outp, byte[] key) { using var i = File.OpenRead(inp); var iv = new byte[16]; i.Read(iv, 0, 16); using var aes = Aes.Create(); aes.Key = key; aes.IV = iv; using var dec = aes.CreateDecryptor(); using var cs = new CryptoStream(i, dec, CryptoStreamMode.Read); using var o = File.Create(outp); cs.CopyTo(o); }
    private static byte[] DeriveKey(string pwd) => SHA256.HashData(Encoding.UTF8.GetBytes(pwd + "PhotoVaultSalt2024"));
    private static string HashPwd(string pwd) => BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes(pwd + "PhotoVaultPwdSalt"))).Replace("-", "").ToLowerInvariant();
}
