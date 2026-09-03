using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FloatingTools.App.Services.OpenAI;

public sealed class WindowsDpapiApiKeyStore(string path) : ISecureApiKeyStore
{
    private const int CryptProtectUiForbidden = 0x1;

    public bool HasKey => File.Exists(path);

    public string? Load()
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var encrypted = File.ReadAllBytes(path);
        if (encrypted.Length == 0)
        {
            return null;
        }

        var decrypted = Unprotect(encrypted);
        var value = Encoding.UTF8.GetString(decrypted);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void Save(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllBytes(
                temporaryPath,
                Protect(Encoding.UTF8.GetBytes(apiKey.Trim())));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    public void Remove()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static byte[] Protect(byte[] value) => Transform(value, protect: true);

    private static byte[] Unprotect(byte[] value) => Transform(value, protect: false);

    private static byte[] Transform(byte[] value, bool protect)
    {
        var inputPointer = Marshal.AllocHGlobal(value.Length);
        try
        {
            Marshal.Copy(value, 0, inputPointer, value.Length);
            var input = new DataBlob(value.Length, inputPointer);
            DataBlob output;
            var succeeded = protect
                ? CryptProtectData(
                    ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, out output)
                : CryptUnprotectData(
                    ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, out output);
            if (!succeeded)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                var result = new byte[output.Length];
                Marshal.Copy(output.Data, result, 0, output.Length);
                return result;
            }
            finally
            {
                LocalFree(output.Data);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(inputPointer);
        }
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob(int length, IntPtr data)
    {
        public int Length = length;
        public IntPtr Data = data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
