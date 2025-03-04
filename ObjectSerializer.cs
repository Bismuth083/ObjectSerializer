using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;

namespace ObjectSerializer
{
  /// <summary>
  /// オブジェクトをJson形式でシリアライズ/デシリアライズするクラス。<br/>
  /// パスワードを設定することで、暗号化/復号化も行うことができます。
  /// </summary>
  /// <remarks>使用可能な型などについては、System.Text.Jsonの情報を参照してください。<br/><seealso cref="https://learn.microsoft.com/ja-jp/dotnet/standard/serialization/system-text-json/how-to"/></remarks>
  public static class Serializer
  {
    /// <summary>
    /// オブジェクトをJson形式でシリアライズします。<br/>暗号化する場合はパスワードを指定してください。
    /// </summary>
    /// <typeparam name="T">シリアライズする型。<br/>標準的な組み込み型、コレクション、配列、レコード、単純なクラスなどが有効です。</typeparam>
    /// <param name="obj">シリアライズするオブジェクト。</param>
    /// <param name="password">既定ではnullです。<br/>パスワードを設定すると暗号化されます。</param>
    /// <returns>規定では、暗号化されていないJSON文字列が返されます。<br/>パスワードを設定した場合、暗号化されたBase64文字列が返されます。</returns>
    public static string Serialize<T>(T obj, string? password = null)
    {
      string serialized = JsonSerializer.Serialize(obj, Option);

      // Compress then Encrypt the json if password is not null
      if (password != null)
      {
        serialized = Encrypt(Compress(serialized), password);
      }

      return serialized;
    }

    /// <summary>
    /// Json形式でシリアライズされた文字列をデシリアライズします。<br/>シリアライズ時にパスワードを設定した場合、同じパスワードを指定してください。
    /// </summary>
    /// <typeparam name="T">デシリアライズする型。<br/>標準的な組み込み型、コレクション、配列、レコード、単純なクラスなどが有効です。</typeparam>
    /// <param name="deserializeText">デシリアライズする文字列。<br/>ObjectSerializer.Serializeでシリアライズしたものを利用してください。</param>
    /// <param name="password">既定ではnullです。<br/>シリアライズ時にパスワードを設定したパスワードと同じパスワードを指定してください。</param>
    /// <returns>与えられたJSONファイルから作成されたオブジェクトを返します。</returns>
    public static T Deserialize<T>(string serializedText, string? password = null)
    {
      string json = serializedText;

      // Decrypt then Decompress the json if password is not null
      if (password != null)
      {
        json = Decompress(Decrypt(json, password));
      }

      T obj = JsonSerializer.Deserialize<T>(json, Option) ?? throw new JsonException("Failed to deserialize object");
      return obj;
    }

    /// <summary>
    /// カスタムシリアライザを追加します。<br/>カスタムシリアライザの作成方法は、System.Text.Jsonの情報を参照してください。<br/><seealso cref="https://learn.microsoft.com/ja-jp/dotnet/standard/serialization/system-text-json/how-to#how-to-write-a-custom-converter"/>
    /// </summary>
    /// <typeparam name="T">変換したい型。</typeparam>
    /// <param name="converter">カスタムコンバーター。作成方法はSystem.Text.Jsonの情報を参照してください。</param>
    public static void AddCustomSerializer<T>(JsonConverter<T> converter)
    {
      Option.Converters.Add(converter);
    }

    /*
     * ここから内部の実装。
     * 
     * シリアライズ:
     * オブジェクト -> <シリアライズ> -> JSON文字列 -> <圧縮> -> 圧縮Byte[] -> <暗号化> -> Base64文字列
     * 
     * デシリアライズ:
     * Base64文字列 -> <復号化> -> 圧縮Byte[] -> <解凍> -> JSON文字列 -> <デシリアライズ> -> オブジェクト
     * 
     * 暗号化/復号化にはAESを使用し、パスワードから256bitの鍵を生成する。
     * IVは先頭の128bitを使用する。これはEncryptにてランダムに設定する。暗号文はそれ以降の部分になる。
     * 暗号化方式はCBC、パディングはPKCS7を使用する。
     * 
     */

    private static string Encrypt(byte[] compressed, string password)
    {
      // Generate IV
      var rng = RandomNumberGenerator.Create();
      byte[] iv = new byte[16];
      rng.GetBytes(iv);

      // Set up AES
      using Aes aes = Aes.Create();
      aes.KeySize = 256;
      aes.Key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password));
      aes.BlockSize = 128;
      aes.IV = iv;
      aes.Mode = CipherMode.CBC;
      aes.Padding = PaddingMode.PKCS7;

      // Encrypt and return cipher Base64 string
      var encryptor = aes.CreateEncryptor();
      using var ms = new MemoryStream();
      using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
      ms.Write(aes.IV);
      cs.Write(compressed, 0, compressed.Length);
      cs.FlushFinalBlock();
      return Convert.ToBase64String(ms.ToArray());
    }

    private static byte[] Decrypt(string cipher, string password)
    {
      // Set up AES(This setup must be the same as for Encrypt.)
      var cipherBytes = Convert.FromBase64String(cipher);
      using Aes aes = Aes.Create();
      aes.KeySize = 256;
      aes.Key = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password));
      aes.BlockSize = 128;
      aes.IV = cipherBytes.Take(aes.BlockSize / 8).ToArray(); // Get IV from cipher
      aes.Mode = CipherMode.CBC;
      aes.Padding = PaddingMode.PKCS7;

      // Decrypt and return compressed byte[]
      var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
      using var ms = new MemoryStream();
      using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
        cs.Write(cipherBytes, aes.BlockSize / 8, cipherBytes.Length - aes.BlockSize / 8);
      return ms.ToArray();
    }

    private static byte[] Compress(string text)
    {
      // plaintext -> compressed byte[]
      var bytes = Encoding.UTF8.GetBytes(text);

      // Compress and return compressed byte[]
      using var ms = new MemoryStream();
      using (GZipStream gs = new(ms, CompressionLevel.Optimal, true))
      {
        gs.Write(bytes, 0, bytes.Length);
      }
      return ms.ToArray();
    }

    private static string Decompress(byte[] bytes)
    {
      // compressed byte[] -> plaintext
      using var ms = new MemoryStream(bytes);
      using var gs = new GZipStream(ms, CompressionMode.Decompress);
      using var sr = new StreamReader(gs, Encoding.UTF8);
      return sr.ReadToEnd();
    }

    private static readonly JsonSerializerOptions Option = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
      WriteIndented = true,
      IncludeFields = true,
    };
  }
}