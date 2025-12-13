using System;
using System.IO;
using System.Security.Cryptography;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service for cryptographic operations, particularly RSA key generation and PEM export
/// </summary>
public class CryptographyService
{
    public void ExportPublicKeyPem(RSACryptoServiceProvider csp, TextWriter outputStream)
    {
        var keyPub = Convert.ToBase64String(csp.ExportRSAPublicKey()).ToCharArray();

        outputStream.WriteLine("-----BEGIN RSA PUBLIC KEY-----");
        for (var i = 0; i < keyPub.Length; i += 64)
        {
            outputStream.WriteLine(keyPub, i, Math.Min(64, keyPub.Length - i));
        }
        outputStream.WriteLine("-----END RSA PUBLIC KEY-----");
    }

    public void ExportPrivateKeyPem(RSACryptoServiceProvider csp, TextWriter outputStream)
    {
        var keyPrv = Convert.ToBase64String(csp.ExportPkcs8PrivateKey()).ToCharArray();
        outputStream.WriteLine("-----BEGIN PRIVATE KEY-----");
        for (var i = 0; i < keyPrv.Length; i += 64)
        {
            outputStream.WriteLine(keyPrv, i, Math.Min(64, keyPrv.Length - i));
        }
        outputStream.WriteLine("-----END PRIVATE KEY-----");
    }

    //https://stackoverflow.com/questions/23734792/c-sharp-export-private-public-rsa-key-from-rsacryptoserviceprovider-to-pem-strin/23739932#23739932
    //https://stackoverflow.com/questions/28406888/c-sharp-rsa-public-key-output-not-correct/28407693#28407693
    #region KeyExport
    public void ExportPublicKey(RSACryptoServiceProvider csp, TextWriter outputStream)
    {
        var parameters = csp.ExportParameters(false);
        using (var stream = new MemoryStream())
        {
            var writer = new BinaryWriter(stream);
            writer.Write((byte)0x30); // SEQUENCE
            using (var innerStream = new MemoryStream())
            {
                var innerWriter = new BinaryWriter(innerStream);
                innerWriter.Write((byte)0x30); // SEQUENCE
                EncodeLength(innerWriter, 13);
                innerWriter.Write((byte)0x06); // OBJECT IDENTIFIER
                var rsaEncryptionOid = new byte[] { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01 };
                EncodeLength(innerWriter, rsaEncryptionOid.Length);
                innerWriter.Write(rsaEncryptionOid);
                innerWriter.Write((byte)0x05); // NULL
                EncodeLength(innerWriter, 0);
                innerWriter.Write((byte)0x03); // BIT STRING
                using (var bitStringStream = new MemoryStream())
                {
                    var bitStringWriter = new BinaryWriter(bitStringStream);
                    bitStringWriter.Write((byte)0x00); // # of unused bits
                    bitStringWriter.Write((byte)0x30); // SEQUENCE
                    using (var paramsStream = new MemoryStream())
                    {
                        var paramsWriter = new BinaryWriter(paramsStream);
                        EncodeIntegerBigEndian(paramsWriter, parameters.Modulus!); // Modulus
                        EncodeIntegerBigEndian(paramsWriter, parameters.Exponent!); // Exponent
                        var paramsLength = (int)paramsStream.Length;
                        EncodeLength(bitStringWriter, paramsLength);
                        bitStringWriter.Write(paramsStream.GetBuffer(), 0, paramsLength);
                    }
                    var bitStringLength = (int)bitStringStream.Length;
                    EncodeLength(innerWriter, bitStringLength);
                    innerWriter.Write(bitStringStream.GetBuffer(), 0, bitStringLength);
                }
                var length = (int)innerStream.Length;
                EncodeLength(writer, length);
                writer.Write(innerStream.GetBuffer(), 0, length);
            }

            var base64 = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length).ToCharArray();
            outputStream.WriteLine("-----BEGIN PUBLIC KEY-----");
            for (var i = 0; i < base64.Length; i += 64)
            {
                outputStream.WriteLine(base64, i, Math.Min(64, base64.Length - i));
            }
            outputStream.WriteLine("-----END PUBLIC KEY-----");
        }
    }
    
    public void ExportPrivateKey(RSACryptoServiceProvider csp, TextWriter outputStream)
    {
        if (csp.PublicOnly) throw new ArgumentException("CSP does not contain a private key", "csp");
        var parameters = csp.ExportParameters(true);
        using (var stream = new MemoryStream())
        {
            var writer = new BinaryWriter(stream);
            writer.Write((byte)0x30); // SEQUENCE
            using (var innerStream = new MemoryStream())
            {
                var innerWriter = new BinaryWriter(innerStream);
                EncodeIntegerBigEndian(innerWriter, new byte[] { 0x00 }); // Version
                EncodeIntegerBigEndian(innerWriter, parameters.Modulus!);
                EncodeIntegerBigEndian(innerWriter, parameters.Exponent!);
                EncodeIntegerBigEndian(innerWriter, parameters.D!);
                EncodeIntegerBigEndian(innerWriter, parameters.P!);
                EncodeIntegerBigEndian(innerWriter, parameters.Q!);
                EncodeIntegerBigEndian(innerWriter, parameters.DP!);
                EncodeIntegerBigEndian(innerWriter, parameters.DQ!);
                EncodeIntegerBigEndian(innerWriter, parameters.InverseQ!);
                var length = (int)innerStream.Length;
                EncodeLength(writer, length);
                writer.Write(innerStream.GetBuffer(), 0, length);
            }

            var base64 = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length).ToCharArray();
            outputStream.WriteLine("-----BEGIN RSA PRIVATE KEY-----");
            // Output as Base64 with lines chopped at 64 characters
            for (var i = 0; i < base64.Length; i += 64)
            {
                outputStream.WriteLine(base64, i, Math.Min(64, base64.Length - i));
            }
            outputStream.WriteLine("-----END RSA PRIVATE KEY-----");
        }
    }
    
    public void EncodeLength(BinaryWriter stream, int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException("length", "Length must be non-negative");
        if (length < 0x80)
        {
            // Short form
            stream.Write((byte)length);
        }
        else
        {
            // Long form
            var temp = length;
            var bytesRequired = 0;
            while (temp > 0)
            {
                temp >>= 8;
                bytesRequired++;
            }
            stream.Write((byte)(bytesRequired | 0x80));
            for (var i = bytesRequired - 1; i >= 0; i--)
            {
                stream.Write((byte)(length >> (8 * i) & 0xff));
            }
        }
    }
    
    public void EncodeIntegerBigEndian(BinaryWriter stream, byte[] value, bool forceUnsigned = true)
    {
        stream.Write((byte)0x02); // INTEGER
        var prefixZeros = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != 0) break;
            prefixZeros++;
        }
        if (value.Length - prefixZeros == 0)
        {
            EncodeLength(stream, 1);
            stream.Write((byte)0);
        }
        else
        {
            if (forceUnsigned && value[prefixZeros] > 0x7f)
            {
                // Add a prefix zero to force unsigned if the MSB is 1
                EncodeLength(stream, value.Length - prefixZeros + 1);
                stream.Write((byte)0);
            }
            else
            {
                EncodeLength(stream, value.Length - prefixZeros);
            }
            for (var i = prefixZeros; i < value.Length; i++)
            {
                stream.Write(value[i]);
            }
        }
    }
    #endregion
}
