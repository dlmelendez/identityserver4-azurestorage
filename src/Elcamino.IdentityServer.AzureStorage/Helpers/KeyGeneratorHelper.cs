// Copyright (c) David Melendez. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;
using System.Security.Cryptography;
using System.Text;
using Azure;

namespace ElCamino.IdentityServer.AzureStorage.Helpers
{
    public static class KeyGeneratorHelper
    {

        public static readonly ETag ETagWildCard = ETag.All;
        public const string MinSha1Hash = "0000000000000000000000000000000000000000";
        public const string MaxSha1Hash = "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ";
        /// <summary>
        /// As is before hash is calculated
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        private static string ConvertToCaseSensitveHash(string plainText)
        {
            plainText ??= string.Empty;
            return ConvertKeyToHash<SHA1>(SHA1.Create(), plainText);
        }

        public static string GenerateDateTimeDecendingId(DateTime dt)
        {
            //Maybe validate UTC somewhere else?
            //if (dt.Kind != DateTimeKind.Utc)
            //    throw new ArgumentException("DateTime must be UTC kind.", nameof(dt));
            TimeSpan ts = DateTime.MaxValue.Subtract(dt);
            return ts.TotalMilliseconds.ToString("000000000000000");
        }

        public static ReadOnlySpan<char> GenerateHashValue(ReadOnlySpan<char> plainText)
        {
            ReadOnlySpan<char> trim = plainText.Trim();
            Span<char> lower = stackalloc char[trim.Length];
            int lowerCount = trim.ToLower(lower, System.Globalization.CultureInfo.InvariantCulture);
            Span<byte> encodedBytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(lowerCount)];
            int encodedByteCount = Encoding.UTF8.GetBytes([.. lower.Slice(0, lowerCount)], encodedBytes);
            Span<byte> hashedBytes = stackalloc byte[SHA1.HashSizeInBytes];
            int hashedByteCount = SHA1.HashData(encodedBytes.Slice(0, encodedByteCount), hashedBytes);
            return Convert.ToHexString(hashedBytes.Slice(0, hashedByteCount)).ToLowerInvariant();
        }

        /// <summary>
        /// Used only for backward compatibility testing, don't use in production
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string GenerateHashValue_Deprecated(string plainText)
        {
            return ConvertToCaseSensitveHash(plainText);
        }


        private static string ConvertKeyToHash<T>(T hash, string input) where T : HashAlgorithm
        {
            using (hash)
            {
                return GetHash<T>(hash, input.Trim().ToLower());
            }
        }

        private static string GetHash<T>(T hash, string input) where T : HashAlgorithm
        {
            // Convert the input string to a byte array and compute the hash. 
            byte[] data = hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes 
            // and create a string.
            StringBuilder sBuilder = new StringBuilder(64);

            // Loop through each byte of the hashed data  
            // and format each one as a hexadecimal string. 
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string. 
            return sBuilder.ToString();
        }
       
    }
}
